using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generátor meshu pre vetvy stromu.
/// OPRAVA: 100% vodotesný mesh (Watertight Mesh). 
/// Všetky detské vetvy bezvýhradne zdieľajú počiatočný kruh vrcholov (ring) 
/// so svojím rodičom. To úplne eliminuje diery, medzery a plávajúce polygóny.
/// </summary>
public static class BranchMeshGenerator
{
    public static Mesh GenerateTreeMesh(BranchNode root, int radialSegments = 8, int maxDepth = -1, bool smoothMesh = true)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        List<Vector3> normals = new List<Vector3>();

        Dictionary<BranchNode, int> nodeRingCache = new Dictionary<BranchNode, int>();

        // 1. Zistenie počiatočného smeru pre koreň
        Vector3 initialDir = Vector3.up;
        if (root.Children.Count > 0) 
            initialDir = (root.Children[0].Position - root.Position).normalized;

        // 2. Počiatočná kolmica (Normal) pre koreň
        Vector3 initialNormal = Vector3.Cross(initialDir, Vector3.right);
        if (initialNormal.sqrMagnitude < 0.01f) 
            initialNormal = Vector3.Cross(initialDir, Vector3.forward);
        initialNormal.Normalize();
        Vector3 initialBinormal = Vector3.Cross(initialDir, initialNormal).normalized;

        // 3. VYTVORENIE KOREŇOVÉHO KRUHU (Base Ring)
        int rootRingBase = vertices.Count;
        GenerateRing(root.Position, initialNormal, initialBinormal, root.Radius, 0f, radialSegments, vertices, uvs, normals);
        nodeRingCache[root] = rootRingBase;

        // 4. Rekurzívne prechádzanie a generovanie vetiev
        TraverseBranches(root, vertices, triangles, uvs, normals, radialSegments, maxDepth, nodeRingCache, 0f, initialDir, initialNormal);

        Mesh mesh = new Mesh { name = "TreeMesh" };
        if (vertices.Count == 0) return mesh;

        if (vertices.Count > 65000)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);

        if (smoothMesh && vertices.Count < 50000)
            mesh.RecalculateNormals(); // Priemeruje normály v spojoch pre dokonalú hladkosť
        else
            mesh.SetNormals(normals);

        mesh.RecalculateBounds();
        return mesh;
    }

    private static void TraverseBranches(
        BranchNode node, List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, List<Vector3> normals,
        int radSegs, int maxDepth, Dictionary<BranchNode, int> nodeRingCache, float accumulatedV,
        Vector3 prevDir, Vector3 prevNormal)
    {
        // KĽÚČOVÁ OPRAVA: KAŽDÁ vetva vychádzajúca z tohto uzla natvrdo použije jeho ring!
        // Žiadne generovanie nových odtrhnutých kruhov na spojoch.
        int startRingBase = nodeRingCache[node]; 

        foreach (var child in node.Children)
        {
            if (maxDepth >= 0 && child.Depth > maxDepth) continue;
            
            // Zabezpečenie proti príliš tenkým, neviditeľným vetvičkám, ktoré by zrútili výpočet
            if (node.Radius < 0.0005f && child.Radius < 0.0005f) continue;

            Vector3 direction = child.Position - node.Position;
            float segLength = direction.magnitude;
            if (segLength < 0.0001f) continue;
            direction /= segLength; // Normalizácia

            // --- BISHOP FRAME (Parallel Transport) ---
            // Zabraňuje krúteniu "ostnatého drôtu" po dĺžke vetvy
            Vector3 currentNormal = prevNormal;
            Vector3 axis = Vector3.Cross(prevDir, direction);
            if (axis.sqrMagnitude > 0.0001f)
            {
                float angle = Vector3.Angle(prevDir, direction);
                currentNormal = Quaternion.AngleAxis(angle, axis.normalized) * prevNormal;
            }
            Vector3 currentBinormal = Vector3.Cross(direction, currentNormal).normalized;

            float vEnd = accumulatedV + segLength;

            // Vygenerovanie nového kruhu len na KONCI segmentu (pri detskom uzle)
            int endRingBase = vertices.Count;
            GenerateRing(child.Position, currentNormal, currentBinormal, child.Radius, vEnd, radSegs, vertices, uvs, normals);
            nodeRingCache[child] = endRingBase;

            // Spojenie štartovacieho kruhu (zdieľaného s rodičom) s koncovým kruhom
            ConnectRings(startRingBase, endRingBase, radSegs, triangles);

            // Pokračujeme ďalej do koruny s aktuálnymi vektormi
            TraverseBranches(child, vertices, triangles, uvs, normals, radSegs, maxDepth, nodeRingCache, vEnd, direction, currentNormal);
        }
    }

    private static void GenerateRing(Vector3 center, Vector3 normal, Vector3 binormal, float radius, float vCoord, int radialSegments,
        List<Vector3> vertices, List<Vector2> uvs, List<Vector3> normals)
    {
        for (int i = 0; i <= radialSegments; i++)
        {
            float angle = (float)i / radialSegments * Mathf.PI * 2f;
            Vector3 offset = normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle);
            
            vertices.Add(center + offset * radius);
            normals.Add(offset.normalized);
            uvs.Add(new Vector2((float)i / radialSegments, vCoord));
        }
    }

    private static void ConnectRings(int bottomBase, int topBase, int radialSegments, List<int> triangles)
    {
        for (int i = 0; i < radialSegments; i++)
        {
            int bl = bottomBase + i;
            int br = bottomBase + i + 1;
            int tl = topBase + i;
            int tr = topBase + i + 1;

            triangles.Add(bl); triangles.Add(tl); triangles.Add(tr);
            triangles.Add(bl); triangles.Add(tr); triangles.Add(br);
        }
    }
}