using System.Collections.Generic;
using UnityEngine;

public static class RadiusSmoothing
{
    public static void Smooth(BranchNode root, int iterations = 2, float factor = 0.3f)
    {
        List<BranchNode> allNodes = new List<BranchNode>();
        CollectAll(root, allNodes);

        Dictionary<BranchNode, float> targetRadii = new Dictionary<BranchNode, float>();
        foreach (var node in allNodes)
            targetRadii[node] = node.Radius;

        for (int iter = 0; iter < iterations; iter++)
        {
            foreach (var node in allNodes)
            {
                if (node.IsRoot && node.IsLeaf) continue;

                float neighborAvg = 0f;
                float weightSum = 0f;

                if (node.Parent != null)
                {
                    float weight = node.Parent.Radius;
                    neighborAvg += node.Parent.Radius * weight;
                    weightSum += weight;
                }

                foreach (var child in node.Children)
                {
                    float weight = child.Radius;
                    neighborAvg += child.Radius * weight;
                    weightSum += weight;
                }

                if (weightSum > 0)
                {
                    neighborAvg /= weightSum;

                    float smoothed = Mathf.Lerp(node.Radius, neighborAvg, factor);

                    smoothed = Mathf.Min(smoothed, targetRadii[node] * 1.2f);

                    smoothed = Mathf.Max(smoothed, targetRadii[node] * 0.5f);

                    node.Radius = smoothed;
                }
            }
        }
    }

    public static void ApplyTaper(BranchNode root, float taperStrength = 0.1f)
    {
        ApplyTaperRecursive(root, taperStrength);
    }

    private static void ApplyTaperRecursive(BranchNode node, float taperStrength)
    {
        foreach (var child in node.Children)
        {
            float parentR = node.Radius;
            float childR = child.Radius;
            
            float taper = 1f - taperStrength * (1f - childR / Mathf.Max(parentR, 0.001f));
            child.Radius *= Mathf.Clamp01(taper);

            ApplyTaperRecursive(child, taperStrength);
        }
    }

    private static void CollectAll(BranchNode node, List<BranchNode> list)
    {
        list.Add(node);
        foreach (var child in node.Children)
            CollectAll(child, list);
    }
}