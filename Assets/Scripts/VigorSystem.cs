using System.Collections.Generic;
using UnityEngine;

public static class VigorSystem
{
    private const float ThicknessFactor = 0.015f;

    public static void InjectVigor(BranchNode node, float amount, float lambda = 0.6f)
    {
        BranchNode current = node;
        float thicknessSpread = amount * ThicknessFactor;

        while (current != null && thicknessSpread > 0.001f)
        {
            current.Radius += thicknessSpread;
            thicknessSpread *= 0.9f;
            current = current.Parent;
        }

        node.Vigor += amount * 0.8f;

        if (node.Children.Count > 0)
        {
            DistributeVigorDown(node, amount * 0.2f, lambda);
        }
    }

    private static void DistributeVigorDown(BranchNode node, float vigor, float lambda)
    {
        if (node.Children.Count == 0 || vigor < 0.01f) return;

        if (node.Children.Count == 1)
        {
            node.Children[0].Vigor += vigor * 0.9f;
            DistributeVigorDown(node.Children[0], vigor * 0.9f, lambda);
        }
        else
        {
            BranchNode mainChild = node.Children[0];
            float maxRadius = 0f;
            foreach (var child in node.Children)
            {
                if (child.Radius > maxRadius)
                {
                    maxRadius = child.Radius;
                    mainChild = child;
                }
            }

            float totalLateralVigor = vigor * (1f - lambda);
            int lateralCount = node.Children.Count - 1;

            foreach (var child in node.Children)
            {
                if (child == mainChild)
                {
                    child.Vigor += vigor * lambda;
                    DistributeVigorDown(child, vigor * lambda, lambda);
                }
                else
                {
                    float lateralVigor = lateralCount > 0 ? totalLateralVigor / lateralCount : 0f;
                    child.Vigor += lateralVigor;
                    DistributeVigorDown(child, lateralVigor, lambda);
                }
            }
        }
    }

    public static List<BranchNode> FindGrowthCandidates(BranchNode root, float threshold = 2f)
    {
        List<BranchNode> candidates = new List<BranchNode>();
        FindCandidatesRecursive(root, candidates, threshold);
        return candidates;
    }

    private static void FindCandidatesRecursive(BranchNode node, List<BranchNode> candidates, float threshold)
    {
        if (node.Vigor >= threshold && node.Children.Count < 4)
        {
            candidates.Add(node);
        }
        foreach (var child in node.Children)
            FindCandidatesRecursive(child, candidates, threshold);
    }

    public static int GrowFromVigor(
        BranchNode targetNode, float threshold = 2f,
        float branchLength = 0.3f, float branchAngle = 30f,
        int seed = 42)
    {
        BranchNode treeRoot = targetNode;
        while (treeRoot.Parent != null) treeRoot = treeRoot.Parent;

        var candidates = FindGrowthCandidates(targetNode, threshold);
        if (candidates.Count == 0) return 0;

        System.Random rng = new System.Random(seed);
        int newNodeCount = 0;
        int globalIndex = CountNodes(treeRoot);

        foreach (var node in candidates)
        {
            int newBranches = Mathf.Clamp(Mathf.FloorToInt(node.Vigor / threshold), 1, 2);
            float vigorPerBranch = node.Vigor / newBranches;

            int existingChildrenCount = node.Children.Count;
            float angleStep = 360f / newBranches;

            for (int i = 0; i < newBranches; i++)
            {
                Vector3 parentDir = Vector3.up;
                if (node.Parent != null)
                    parentDir = (node.Position - node.Parent.Position).normalized;

                float divergence = (137.5f * existingChildrenCount) + (angleStep * i) + (float)(rng.NextDouble() * 20f - 10f);
                float pitchAngle = branchAngle + (float)(rng.NextDouble() * 10f - 5f);

                Vector3 ortho = Vector3.Cross(parentDir, Vector3.up);
                if (ortho.sqrMagnitude < 0.001f)
                    ortho = Vector3.Cross(parentDir, Vector3.right);
                ortho.Normalize();

                Quaternion rotation = Quaternion.AngleAxis(divergence, parentDir) * Quaternion.AngleAxis(pitchAngle, ortho);
                Vector3 newDir = (rotation * parentDir).normalized;

                float length = branchLength * Mathf.Sqrt(vigorPerBranch);
                Vector3 newPos = node.Position + newDir * length;

                float baseTipRadius = 0.015f;
                float vigorBonus = Mathf.Clamp(vigorPerBranch * 0.005f, 0f, 0.02f);
                float newRadius = baseTipRadius + vigorBonus;

                newRadius = Mathf.Min(newRadius, node.Radius * 0.5f);

                Quaternion newOrientation = Quaternion.LookRotation(newDir, Vector3.up);
                if (newDir == Vector3.up || newDir == Vector3.down)
                    newOrientation = node.Orientation;

                BranchNode newNode = new BranchNode(
                    newPos, newOrientation, newRadius,
                    node.Depth + 1, node
                );
                newNode.Index = globalIndex++;
                newNode.Vigor = vigorPerBranch * 0.3f;

                newNodeCount++;
            }

            if (node.Vigor > threshold * newBranches)
            {
                float excessVigor = node.Vigor - (threshold * newBranches);
                node.Radius += excessVigor * ThicknessFactor;
            }

            node.Vigor *= 0.1f;
            UpdateRadiiToRoot(node);
        }

        return newNodeCount;
    }

    private static void UpdateRadiiToRoot(BranchNode node)
    {
        BranchNode current = node;
        while (current != null)
        {
            if (current.Children.Count > 0)
            {
                float sumSq = 0f;
                foreach (var child in current.Children)
                    sumSq += child.Radius * child.Radius;

                float newRadius = Mathf.Sqrt(sumSq);
                current.Radius = Mathf.Max(current.Radius, newRadius);
            }
            current = current.Parent;
        }
    }

    public static int Prune(BranchNode node)
    {
        if (node.IsRoot) return 0;

        int removedCount = CountNodes(node);
        BranchNode parent = node.Parent;

        parent.Children.Remove(node);
        node.Parent = null;

        BranchNode current = parent;
        while (current != null)
        {
            if (current.Children.Count > 0)
            {
                float sumSq = 0f;
                foreach (var child in current.Children)
                    sumSq += child.Radius * child.Radius;
                current.Radius = Mathf.Sqrt(sumSq);
            }
            else
            {
                current.Radius = 0.005f;
            }
            current = current.Parent;
        }

        return removedCount;
    }

    public static void ResetVigor(BranchNode root, float defaultVigor = 1f)
    {
        root.Vigor = defaultVigor;
        foreach (var child in root.Children)
            ResetVigor(child, defaultVigor);
    }

    private static int CountNodes(BranchNode node)
    {
        int count = 1;
        foreach (var child in node.Children)
            count += CountNodes(child);
        return count;
    }
}