using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// StrandSystem — jadro strand-based volumetrického modelovania.
/// Opravená architektúra na striktný Bottom-Up prístup.
/// </summary>
public class StrandSystem
{
    public List<Strand> Strands { get; private set; } = new List<Strand>();
    public float StrandRadius = 0.008f;
    public int StrandsPerEndNode = 3;
    public int PBDIterations = 5;
    public int PBDSteps = 300;
    public float MedialAxisAttraction = 0.1f;
    public int Seed = 42;
    public int MaxTotalStrands = 200;

    private System.Random _rng;
    private Dictionary<BranchNode, List<int>> _nodeStrands = new Dictionary<BranchNode, List<int>>();

    public void Build(BranchNode root)
    {
        _rng = new System.Random(Seed);
        Strands.Clear();
        _nodeStrands.Clear();

        List<BranchNode> leaves = new List<BranchNode>();
        CollectLeaves(root, leaves);
        ShuffleList(leaves);

        int strandId = 0;
        int maxStrands = Mathf.Min(MaxTotalStrands, leaves.Count * StrandsPerEndNode);

        foreach (var leaf in leaves)
        {
            if (strandId >= maxStrands) break;

            for (int i = 0; i < StrandsPerEndNode; i++)
            {
                if (strandId >= maxStrands) break;

                Strand strand = new Strand(strandId++, StrandRadius);
                strand.BuildPath(leaf);
                Strands.Add(strand);

                foreach (var node in strand.Path)
                {
                    if (!_nodeStrands.ContainsKey(node))
                        _nodeStrands[node] = new List<int>();
                    _nodeStrands[node].Add(strand.Id);
                }
            }
        }

        // 1. Spustenie Bottom-Up procesovania (Inicializácia + PBD okamžite)
        ProcessNodesBottomUp(root);

        // 2. Výpočet 3D pozícií pre finálny mesh
        ComputeWorldPositions(root);
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            T tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    private void ProcessNodesBottomUp(BranchNode node)
    {
        // Najprv spracuj všetky deti (Bottom-Up)
        foreach (var child in node.Children)
        {
            ProcessNodesBottomUp(child);
        }

        if (!_nodeStrands.ContainsKey(node)) return;
        List<int> strandIds = _nodeStrands[node];

        // 1. Inicializácia pozícií na tomto uzle
        if (node.IsLeaf)
        {
            foreach (int sid in strandIds)
            {
                float angle = (float)_rng.NextDouble() * Mathf.PI * 2f;
                float dist = (float)_rng.NextDouble() * StrandRadius * 2f;
                Strands[sid].ParticlePositions[node] = new Vector2(
                    Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);
            }
        }
        else if (node.Children.Count == 1)
        {
            // Priamy prenos zo spracovaného a už zbaleného dieťaťa
            BranchNode child = node.Children[0];
            foreach (int sid in strandIds)
            {
                if (Strands[sid].ParticlePositions.ContainsKey(child))
                    Strands[sid].ParticlePositions[node] = Strands[sid].ParticlePositions[child];
                else
                    Strands[sid].ParticlePositions[node] = Vector2.zero;
            }
        }
        else
        {
            // Spájanie viacerých vetiev
            MergeStrandsAtBranching(node);
        }

        // 2. PBD Packing HNEĎ teraz, na každom vetvení
        if (node.IsBranching && strandIds.Count > 1)
        {
            RunPBDForNode(node, strandIds);
        }
    }

    private void MergeStrandsAtBranching(BranchNode node)
    {
        if (node.Children.Count == 0) return;

        var childrenByStrandCount = node.Children
            .OrderByDescending(c => _nodeStrands.ContainsKey(c) ? _nodeStrands[c].Count : 0)
            .ToList();

        BranchNode mainChild = childrenByStrandCount[0];

        // Hlavná vetva ostáva v strede
        if (_nodeStrands.ContainsKey(mainChild))
        {
            foreach (int sid in _nodeStrands[mainChild])
            {
                if (Strands[sid].ParticlePositions.ContainsKey(mainChild))
                    Strands[sid].ParticlePositions[node] = Strands[sid].ParticlePositions[mainChild];
                else
                    Strands[sid].ParticlePositions[node] = Vector2.zero;
            }
        }

        // Pridávanie menších vetiev
        for (int c = 1; c < childrenByStrandCount.Count; c++)
        {
            BranchNode child = childrenByStrandCount[c];
            if (!_nodeStrands.ContainsKey(child)) continue;

            Vector3 childEdge = (child.Position - node.Position).normalized;
            Vector2 displacement2D = ProjectOntoBackplane(childEdge, node);

            if (displacement2D.sqrMagnitude < 0.001f)
                displacement2D = new Vector2(1f, 0f); 
            displacement2D = displacement2D.normalized;

            Vector2 childCenter = Vector2.zero;
            int count = 0;
            foreach (int sid in _nodeStrands[child])
            {
                if (Strands[sid].ParticlePositions.ContainsKey(child))
                {
                    childCenter += Strands[sid].ParticlePositions[child];
                    count++;
                }
            }
            if (count > 0) childCenter /= count;

            float childExtent = ComputeMaxExtent(_nodeStrands[child], child);
            float mainExtent = ComputeMaxExtent(_nodeStrands.ContainsKey(mainChild) ? _nodeStrands[mainChild] : null, node);
            
            float displaceDist = mainExtent + childExtent + StrandRadius;

            foreach (int sid in _nodeStrands[child])
            {
                Vector2 originalPos = Vector2.zero;
                if (Strands[sid].ParticlePositions.ContainsKey(child))
                    originalPos = Strands[sid].ParticlePositions[child];

                Vector2 localPos = originalPos - childCenter;
                Strands[sid].ParticlePositions[node] = localPos + displacement2D * displaceDist;
            }
        }
    }

    private void RunPBDForNode(BranchNode node, List<int> strandIds)
    {
        float requiredRadius = Mathf.Sqrt(strandIds.Count) * StrandRadius * 1.1f; 
        float profileRadius = Mathf.Max(node.Radius, requiredRadius);

        int maxSteps = Mathf.Min(PBDSteps, 30);
        float cellSize = StrandRadius * 2f;

        for (int step = 0; step < maxSteps; step++)
        {
            float maxCorrection = 0f;

            for (int iter = 0; iter < PBDIterations; iter++)
            {
                // 1. Boundary constraint
                foreach (int sid in strandIds)
                {
                    Vector2 pos = Strands[sid].ParticlePositions[node];
                    float dist = pos.magnitude;

                    if (dist + StrandRadius > profileRadius)
                    {
                        Vector2 closest = pos.normalized * (profileRadius - StrandRadius);
                        Vector2 correction = (closest - pos) * 0.5f;
                        pos += correction;
                        Strands[sid].ParticlePositions[node] = pos;
                    }
                    else
                    {
                        pos *= (1f - MedialAxisAttraction * 0.02f);
                        Strands[sid].ParticlePositions[node] = pos;
                    }
                }

                // 2. Collision constraint
                Dictionary<Vector2Int, List<int>> spatialGrid = new Dictionary<Vector2Int, List<int>>();

                foreach (int sid in strandIds)
                {
                    Vector2 pos = Strands[sid].ParticlePositions[node];
                    Vector2Int cell = new Vector2Int(Mathf.FloorToInt(pos.x / cellSize), Mathf.FloorToInt(pos.y / cellSize));
                    
                    if (!spatialGrid.ContainsKey(cell)) spatialGrid[cell] = new List<int>();
                    spatialGrid[cell].Add(sid);
                }

                foreach (int sidA in strandIds)
                {
                    Vector2 posA = Strands[sidA].ParticlePositions[node];
                    Vector2Int cellA = new Vector2Int(Mathf.FloorToInt(posA.x / cellSize), Mathf.FloorToInt(posA.y / cellSize));

                    for (int x = -1; x <= 1; x++)
                    {
                        for (int y = -1; y <= 1; y++)
                        {
                            Vector2Int neighborCell = new Vector2Int(cellA.x + x, cellA.y + y);
                            if (spatialGrid.ContainsKey(neighborCell))
                            {
                                foreach (int sidB in spatialGrid[neighborCell])
                                {
                                    if (sidA >= sidB) continue; 

                                    Vector2 posB = Strands[sidB].ParticlePositions[node];
                                    Vector2 diff = posA - posB;
                                    float distSq = diff.sqrMagnitude;
                                    float minDist = StrandRadius * 2f;

                                    if (distSq < minDist * minDist && distSq > 0.000001f)
                                    {
                                        float dist2 = Mathf.Sqrt(distSq);
                                        float overlap = minDist - dist2;
                                        Vector2 correction = diff / dist2 * overlap * 0.5f;

                                        posA += correction;
                                        Strands[sidA].ParticlePositions[node] = posA;
                                        
                                        Strands[sidB].ParticlePositions[node] = posB - correction;
                                        maxCorrection = Mathf.Max(maxCorrection, overlap);
                                    }
                                    else if (distSq <= 0.000001f)
                                    {
                                        float angle = (float)_rng.NextDouble() * Mathf.PI * 2f;
                                        Vector2 nudge = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * StrandRadius * 0.2f;
                                        posA += nudge;
                                        Strands[sidA].ParticlePositions[node] = posA;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (maxCorrection < StrandRadius * 0.01f)
                break;
        }
    }

    public void ApplyTwist(BranchNode node, float twistAngleDegrees)
    {
        if (!_nodeStrands.ContainsKey(node)) return;

        float twistRad = twistAngleDegrees * Mathf.Deg2Rad;

        foreach (int sid in _nodeStrands[node])
        {
            if (Strands[sid].ParticlePositions.ContainsKey(node))
            {
                Vector2 pos = Strands[sid].ParticlePositions[node];
                float r = pos.magnitude;
                float theta = Mathf.Atan2(pos.y, pos.x) + twistRad;
                
                Strands[sid].ParticlePositions[node] = new Vector2(
                    r * Mathf.Cos(theta), 
                    r * Mathf.Sin(theta)
                );
            }
        }
    }

    private void ComputeWorldPositions(BranchNode root)
    {
        bool doSmoothing = Strands.Count < 500; 

        foreach (var strand in Strands)
        {
            strand.WorldPositions.Clear();

            for (int i = 0; i < strand.Path.Count; i++)
            {
                BranchNode node = strand.Path[i];
                if (!strand.ParticlePositions.ContainsKey(node)) continue;

                Vector2 localPos2D = strand.ParticlePositions[node];
                Vector3 worldPos = LocalToWorld(node, localPos2D);
                strand.WorldPositions.Add(worldPos);
            }

            if (doSmoothing && strand.WorldPositions.Count >= 4)
                strand.WorldPositions = SmoothWithCatmullRom(strand.WorldPositions, 2);
        }
    }

    private Vector3 LocalToWorld(BranchNode node, Vector2 localPos)
    {
        Vector3 tangent;
        if (node.Parent != null)
            tangent = (node.Position - node.Parent.Position).normalized;
        else if (node.Children.Count > 0)
            tangent = (node.Children[0].Position - node.Position).normalized;
        else
            tangent = Vector3.up;

        Vector3 right, up;
        ComputeLocalFrame(tangent, node.Orientation, out right, out up);

        return node.Position + right * localPos.x + up * localPos.y;
    }

    private void ComputeLocalFrame(Vector3 tangent, Quaternion orientation, out Vector3 right, out Vector3 up)
    {
        if (orientation != default)
        {
            right = orientation * Vector3.right;
            up = orientation * Vector3.forward;

            right = Vector3.ProjectOnPlane(right, tangent).normalized;
            up = Vector3.Cross(tangent, right).normalized;
        }
        else
        {
            if (Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) < 0.99f) right = Vector3.Cross(tangent, Vector3.up).normalized;
            else right = Vector3.Cross(tangent, Vector3.right).normalized;
            up = Vector3.Cross(tangent, right).normalized;
        }
    }

    private Vector2 ProjectOntoBackplane(Vector3 worldDir, BranchNode node)
    {
        Vector3 tangent = (node.Parent != null) ? (node.Position - node.Parent.Position).normalized : Vector3.up;
        Vector3 right, up;
        ComputeLocalFrame(tangent, node.Orientation, out right, out up);
        return new Vector2(Vector3.Dot(worldDir, right), Vector3.Dot(worldDir, up));
    }

    private float ComputeMaxExtent(List<int> strandIds, BranchNode node)
    {
        if (strandIds == null || strandIds.Count == 0) return 0f;
        float maxDistSq = 0f;
        foreach (int sid in strandIds)
        {
            if (Strands[sid].ParticlePositions.ContainsKey(node))
            {
                float sqrMag = Strands[sid].ParticlePositions[node].sqrMagnitude;
                if (sqrMag > maxDistSq) maxDistSq = sqrMag;
            }
        }
        return Mathf.Sqrt(maxDistSq);
    }

    private List<Vector3> SmoothWithCatmullRom(List<Vector3> points, int subdivisions)
    {
        if (points.Count < 2) return points;
        List<Vector3> smoothed = new List<Vector3>();

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 p0 = i > 0 ? points[i - 1] : points[i];
            Vector3 p1 = points[i];
            Vector3 p2 = points[i + 1];
            Vector3 p3 = i + 2 < points.Count ? points[i + 2] : points[i + 1];

            for (int s = 0; s < subdivisions; s++)
            {
                float t = (float)s / subdivisions;
                float t2 = t * t; float t3 = t2 * t;
                smoothed.Add(0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
            }
        }
        smoothed.Add(points[points.Count - 1]);
        return smoothed;
    }

    private void CollectLeaves(BranchNode node, List<BranchNode> leaves)
    {
        if (node.IsLeaf) { leaves.Add(node); return; }
        foreach (var child in node.Children) CollectLeaves(child, leaves);
    }

    // --- TIETO METÓDY CHÝBALI A SPÔSOBILI TVOJ ERROR ---

    public List<int> GetStrandsAtNode(BranchNode node)
    {
        if (_nodeStrands.ContainsKey(node))
            return _nodeStrands[node];
        return new List<int>();
    }

    public List<Vector3> GetStrandWorldPositionsAtNode(BranchNode node)
    {
        List<Vector3> positions = new List<Vector3>();
        if (!_nodeStrands.ContainsKey(node)) return positions;

        foreach (int sid in _nodeStrands[node])
        {
            if (Strands[sid].ParticlePositions.ContainsKey(node))
            {
                Vector3 worldPos = LocalToWorld(node, Strands[sid].ParticlePositions[node]);
                positions.Add(worldPos);
            }
        }
        return positions;
    }
}