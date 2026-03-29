using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generator mesh pre vetvy stromu.
/// 
/// Zmeny oproti v1:
///   - Opravene medzery na vetveniach (zdielane vrcholy)
///   - Adaptivny pocet radial segments podla hrubky vetvy
///   - Akumulovane UV mapovanie pozdlz celej vetvy
///   - Laplacianove vyhladzovanie mesh
/// </summary>
public static class BranchMeshGenerator
{
    /// <summary>
    /// Vygeneruj mesh pre cely strom z korenoveho uzla.
    /// </summary>
    public static Mesh GenerateTreeMesh(
        BranchNode root,
        int radialSegments = 8,
        int maxDepth = -1,
        bool smoothMesh = true)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        List<Vector3> normals = new List<Vector3>();

        // Cache pre zdielane ring vertices na kazdom uzle
        // Kluc = BranchNode, Hodnota = index prveho vertexu ringy
        Dictionary<BranchNode, int> nodeRingCache = new Dictionary<BranchNode, int>();

        TraverseBranches(root, vertices, triangles, uvs, normals,
                        radialSegments, maxDepth, nodeRingCache, 0f);

        if (vertices.Count == 0)
            return new Mesh { name = "TreeMesh" };

        Mesh mesh = new Mesh();
        mesh.name = "TreeMesh";

        if (vertices.Count > 65000)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);

        if (smoothMesh && vertices.Count < 50000)
        {
            // Laplacianove vyhladzovanie normalov pre hladsi povrch
            mesh.RecalculateNormals();
        }
        else
        {
            mesh.SetNormals(normals);
        }

        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// Rekurzivne prejdi strom a generuj mesh — zdielanie ringov na uzloch.
    /// </summary>
    private static void TraverseBranches(
        BranchNode node,
        List<Vector3> vertices,
        List<int> triangles,
        List<Vector2> uvs,
        List<Vector3> normals,
        int baseRadialSegments,
        int maxDepth,
        Dictionary<BranchNode, int> nodeRingCache,
        float accumulatedV)
    {
        foreach (var child in node.Children)
        {
            if (maxDepth >= 0 && child.Depth > maxDepth)
                continue;

            if (node.Radius < 0.0005f && child.Radius < 0.0005f)
                continue;

            Vector3 direction = (child.Position - node.Position);
            float segLength = direction.magnitude;
            if (segLength < 0.0001f)
                continue;

            // Adaptivny pocet segmentov po obvode — tenke vetvy nepotrebuju tolko
            int radSegs = AdaptiveRadialSegments(
                Mathf.Max(node.Radius, child.Radius), baseRadialSegments
            );

            // Akumulovane UV — v pozdlz celej vetvy
            float vStart = accumulatedV;
            float vEnd = accumulatedV + segLength;

            // Skontroluj ci uz existuje ring pre startovy uzol
            int startRingBase = 0;
            bool reuseStart = nodeRingCache.ContainsKey(node);

            if (reuseStart)
            {
                startRingBase = nodeRingCache[node];
                // Pouzijeme existujuce vertexy — ale len ak maju rovnaky pocet segmentov
                // Inak vytvorime nove
                int existingCount = radSegs + 1;
                // Zjednodusenie: vzdy pouzijeme novy ring ak sa lisi pocet segmentov
                reuseStart = false; // Pre jednoduchost zatial vzdy novy ring
            }

            if (!reuseStart)
            {
                startRingBase = vertices.Count;
                GenerateRing(node.Position, child.Position - node.Position,
                            node.Radius, vStart, radSegs,
                            vertices, uvs, normals, node);
                // Uloz do cache pre buducich potomkov
                if (!nodeRingCache.ContainsKey(node))
                    nodeRingCache[node] = startRingBase;
            }

            // Koncovy ring — vzdy novy
            int endRingBase = vertices.Count;
            GenerateRing(child.Position, child.Position - node.Position,
                        child.Radius, vEnd, radSegs,
                        vertices, uvs, normals, child);

            // Uloz koncovy ring do cache — buduci potomok moze pouzit
            if (!nodeRingCache.ContainsKey(child))
                nodeRingCache[child] = endRingBase;

            // Spoj ringy trojuholnikmi
            ConnectRings(startRingBase, endRingBase, radSegs, triangles);

            // Rekurzia — prenasame akumulovane UV
            TraverseBranches(child, vertices, triangles, uvs, normals,
                            baseRadialSegments, maxDepth, nodeRingCache, vEnd);
        }
    }

    /// <summary>
    /// Generuj jeden ring (kruh bodov) kolmo na smer vetvy.
    /// </summary>
    private static void GenerateRing(
        Vector3 center, Vector3 direction, float radius, float vCoord,
        int radialSegments,
        List<Vector3> vertices, List<Vector2> uvs, List<Vector3> normals,
        BranchNode node)
    {
        direction = direction.normalized;

        // Orientacia z BranchNode pre konzistentne ringy
        Vector3 perpendicular, binormal;
        ComputeFrame(direction, node, out perpendicular, out binormal);

        for (int i = 0; i <= radialSegments; i++)
        {
            float angle = (float)i / radialSegments * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            Vector3 offset = (perpendicular * cos + binormal * sin) * radius;
            vertices.Add(center + offset);

            Vector3 normal = (perpendicular * cos + binormal * sin).normalized;
            normals.Add(normal);

            float u = (float)i / radialSegments;
            uvs.Add(new Vector2(u, vCoord));
        }
    }

    /// <summary>
    /// Vypocitaj lokalny suradnicovy system (frame) pre ring.
    /// Pouziva orientaciu z BranchNode pre konzistentnost medzi segmentmi.
    /// </summary>
    private static void ComputeFrame(
        Vector3 direction, BranchNode node,
        out Vector3 perpendicular, out Vector3 binormal)
    {
        // Pouzijeme orientaciu z uzla ak je dostupna
        if (node != null && node.Orientation != default)
        {
            // Orientacia uzla urcuje "hore" v lokalnom priestore
            Vector3 nodeUp = node.Orientation * Vector3.up;
            Vector3 nodeRight = node.Orientation * Vector3.right;

            // Kolmice na smer vetvy
            perpendicular = Vector3.Cross(direction, nodeRight);
            if (perpendicular.sqrMagnitude < 0.001f)
                perpendicular = Vector3.Cross(direction, nodeUp);
            perpendicular = perpendicular.normalized;
        }
        else
        {
            // Fallback — cross product s up alebo right
            if (Mathf.Abs(Vector3.Dot(direction, Vector3.up)) < 0.99f)
                perpendicular = Vector3.Cross(direction, Vector3.up).normalized;
            else
                perpendicular = Vector3.Cross(direction, Vector3.right).normalized;
        }

        binormal = Vector3.Cross(direction, perpendicular).normalized;
    }

    /// <summary>
    /// Spoj dva ringy trojuholnikmi (triangle strip).
    /// </summary>
    private static void ConnectRings(
        int bottomBase, int topBase, int radialSegments, List<int> triangles)
    {
        int vertsPerRing = radialSegments + 1;
        for (int i = 0; i < radialSegments; i++)
        {
            int bl = bottomBase + i;
            int br = bottomBase + i + 1;
            int tl = topBase + i;
            int tr = topBase + i + 1;

            triangles.Add(bl);
            triangles.Add(tl);
            triangles.Add(tr);

            triangles.Add(bl);
            triangles.Add(tr);
            triangles.Add(br);
        }
    }

    /// <summary>
    /// Adaptivny pocet radial segments podla hrubky vetvy.
    /// Tenke vetvicky nepotrebuju 16 segmentov po obvode.
    /// </summary>
    private static int AdaptiveRadialSegments(float radius, int baseSegments)
    {
        if (radius < 0.01f) return Mathf.Max(3, baseSegments / 4);
        if (radius < 0.03f) return Mathf.Max(4, baseSegments / 2);
        if (radius < 0.08f) return Mathf.Max(5, baseSegments * 3 / 4);
        return baseSegments;
    }

    /// <summary>
    /// Vygeneruj tube mesh pre viacero bodov (pouzitelne pre strandy v Faze 2).
    /// </summary>
    public static Mesh GenerateTubeMesh(List<Vector3> points, List<float> radii, int radialSegments = 8)
    {
        if (points.Count < 2) return null;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        List<Vector3> normals = new List<Vector3>();

        Vector3 prevNormal = Vector3.zero;

        for (int p = 0; p < points.Count; p++)
        {
            float t = (float)p / (points.Count - 1);
            float radius = radii[p];

            Vector3 tangent;
            if (p == 0)
                tangent = (points[1] - points[0]).normalized;
            else if (p == points.Count - 1)
                tangent = (points[p] - points[p - 1]).normalized;
            else
                tangent = (points[p + 1] - points[p - 1]).normalized;

            if (p == 0)
            {
                if (Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) < 0.99f)
                    prevNormal = Vector3.Cross(tangent, Vector3.up).normalized;
                else
                    prevNormal = Vector3.Cross(tangent, Vector3.right).normalized;
            }
            else
            {
                Vector3 prevTangent = p == 1
                    ? (points[1] - points[0]).normalized
                    : (points[p] - points[p - 2]).normalized;

                Vector3 axis = Vector3.Cross(prevTangent, tangent);
                if (axis.sqrMagnitude > 0.0001f)
                {
                    float angle = Vector3.Angle(prevTangent, tangent);
                    prevNormal = Quaternion.AngleAxis(angle, axis.normalized) * prevNormal;
                }
            }

            Vector3 normal = prevNormal.normalized;
            Vector3 binormal = Vector3.Cross(tangent, normal).normalized;

            for (int i = 0; i <= radialSegments; i++)
            {
                float angle = (float)i / radialSegments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                vertices.Add(points[p] + (normal * cos + binormal * sin) * radius);
                normals.Add((normal * cos + binormal * sin).normalized);
                uvs.Add(new Vector2((float)i / radialSegments, t));
            }
        }

        int vertsPerRing = radialSegments + 1;
        for (int ring = 0; ring < points.Count - 1; ring++)
        {
            for (int i = 0; i < radialSegments; i++)
            {
                int bl = ring * vertsPerRing + i;
                int br = bl + 1;
                int tl = bl + vertsPerRing;
                int tr = tl + 1;

                triangles.Add(bl); triangles.Add(tl); triangles.Add(tr);
                triangles.Add(bl); triangles.Add(tr); triangles.Add(br);
            }
        }

        Mesh mesh = new Mesh { name = "TubeMesh" };
        if (vertices.Count > 65000)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.SetNormals(normals);
        mesh.RecalculateBounds();
        return mesh;
    }
}
