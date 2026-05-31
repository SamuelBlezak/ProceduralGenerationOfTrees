using System.Collections.Generic;
using UnityEngine;

public static class BranchMeshGenerator
{

    public static Mesh GenerateTreeMesh(BranchNode root, int radialSegments = 8, int maxDepth = -1, bool smoothMesh = true)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        List<Vector3> normals = new List<Vector3>();

        Vector3 initialDir = Vector3.up;
        if (root.Children.Count > 0)
            initialDir = (root.Children[0].Position - root.Position).normalized;

        Vector3 initialNormal = Vector3.Cross(initialDir, Vector3.right);
        if (initialNormal.sqrMagnitude < 0.01f)
            initialNormal = Vector3.Cross(initialDir, Vector3.forward);
        initialNormal.Normalize();
        Vector3 initialBinormal = Vector3.Cross(initialDir, initialNormal).normalized;

        int rootSegs = AdaptiveSegments(root.Radius, radialSegments);

        int rootRingBase = vertices.Count;
        GenerateRing(root.Position, initialNormal, initialBinormal,
                    root.Radius, 0f, rootSegs, root.Depth,
                    vertices, uvs, normals);

        TraverseBranches(root, rootRingBase, rootSegs,
                        vertices, triangles, uvs, normals,
                        radialSegments, maxDepth, 0f,
                        initialDir, initialNormal);

        Mesh mesh = new Mesh { name = "TreeMesh" };
        if (vertices.Count == 0) return mesh;

        if (vertices.Count > 65000)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);

        if (smoothMesh && vertices.Count < 50000)
            mesh.RecalculateNormals();
        else
            mesh.SetNormals(normals);

        mesh.RecalculateBounds();
        return mesh;
    }

    private static void TraverseBranches(
        BranchNode node, int nodeRingBase, int nodeRadSegs,
        List<Vector3> vertices, List<int> triangles,
        List<Vector2> uvs, List<Vector3> normals,
        int baseRadSegs, int maxDepth, float accumulatedV,
        Vector3 prevDir, Vector3 prevNormal)
    {
        foreach (var child in node.Children)
        {
            if (maxDepth >= 0 && child.Depth > maxDepth) continue;
            if (node.Radius < 0.0005f && child.Radius < 0.0005f) continue;

            Vector3 direction = child.Position - node.Position;
            float segLength = direction.magnitude;
            if (segLength < 0.0001f) continue;
            direction /= segLength;

            Vector3 currentNormal = prevNormal;
            float angleChange = Vector3.Angle(prevDir, direction);
            if (angleChange > 0.5f)
            {
                Vector3 axis = Vector3.Cross(prevDir, direction);
                if (axis.sqrMagnitude > 0.0001f)
                    currentNormal = Quaternion.AngleAxis(angleChange, axis.normalized) * prevNormal;
            }
            Vector3 currentBinormal = Vector3.Cross(direction, currentNormal).normalized;


            int childRadSegs;
            if (node.Children.Count == 1)
            {
                childRadSegs = nodeRadSegs;
            }
            else
            {
                childRadSegs = AdaptiveSegments(child.Radius, baseRadSegs);
            }

            int startRingBase;
            int startRadSegs;

            if (node.Children.Count == 1 && nodeRadSegs == childRadSegs)
            {
                startRingBase = nodeRingBase;
                startRadSegs = nodeRadSegs;
            }
            else
            {
                startRadSegs = childRadSegs;
                startRingBase = vertices.Count;
                GenerateRing(node.Position, currentNormal, currentBinormal,
                            node.Radius, accumulatedV, startRadSegs, node.Depth,
                            vertices, uvs, normals);
            }

            float vEnd = accumulatedV + segLength;

            int endRingBase = vertices.Count;
            GenerateRing(child.Position, currentNormal, currentBinormal,
                        child.Radius, vEnd, childRadSegs, child.Depth,
                        vertices, uvs, normals);

            ConnectRings(startRingBase, endRingBase, childRadSegs, triangles);

            TraverseBranches(child, endRingBase, childRadSegs,
                           vertices, triangles, uvs, normals,
                           baseRadSegs, maxDepth, vEnd,
                           direction, currentNormal);
        }
    }

    private static int AdaptiveSegments(float radius, int baseSegments)
    {
        if (radius > 0.15f) return Mathf.Max(baseSegments, 12);
        if (radius > 0.08f) return Mathf.Max(baseSegments, 10);
        if (radius > 0.03f) return baseSegments;
        if (radius > 0.01f) return Mathf.Max(4, baseSegments / 2);
        return 4; 
    }

    private static void GenerateRing(
        Vector3 center, Vector3 normal, Vector3 binormal,
        float radius, float vCoord, int radialSegments, int depth,
        List<Vector3> vertices, List<Vector2> uvs, List<Vector3> normals)
    {
        float vScale = 2.0f;

        for (int i = 0; i <= radialSegments; i++)
        {
            int idx = i % radialSegments;
            float t = (float)idx / radialSegments;
            float angle = t * Mathf.PI * 2f;

            Vector3 offset = (normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle)) * radius;

            vertices.Add(center + offset);
            normals.Add(offset.magnitude > 0.0001f ? offset.normalized : normal);

            uvs.Add(new Vector2(t, vCoord * vScale));
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