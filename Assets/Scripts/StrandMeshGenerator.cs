using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generátor meshov z strand systému.
/// Využíva Konvexný Obal (Convex Hull) pre adaptáciu hrúbky uzla tak,
/// aby zodpovedala reálnemu fyzickému rozloženiu PBD častíc v priestore
/// a simulovala objemové modelovanie podľa článku Li et al.
/// </summary>
public static class StrandMeshGenerator
{
    public static Mesh GenerateStrandMesh(
        BranchNode root,
        StrandSystem strandSystem,
        int interpolationSteps = 3,
        int radialSegments = 8)
    {
        // 1. Aplikuj skutočné polomery na základe fyzického priestoru strandov
        ApplyOrganicHullRadii(root, strandSystem);

        // 2. Použi overený cylindrický mesh s adaptovanými polomermi
        return BranchMeshGenerator.GenerateTreeMesh(root, radialSegments, -1, true);
    }

    private static void ApplyOrganicHullRadii(BranchNode root, StrandSystem sys)
    {
        ApplyOrganicHullRadiiRecursive(root, sys);
    }

    private static void ApplyOrganicHullRadiiRecursive(BranchNode node, StrandSystem sys)
    {
        var strandIds = sys.GetStrandsAtNode(node);
        int count = strandIds.Count;

        if (count > 0)
        {
            // Nájdi skutočný maximálny dosah (rozmer) strandov na Backplane pomocou obalu
            float maxPhysicalExtent = ComputeHullRadius(strandIds, node, sys);
            
            // Zabezpeč minimálny viditeľný polomer, aby konáre nezmizli
            node.Radius = Mathf.Max(maxPhysicalExtent, sys.StrandRadius);
        }
        else
        {
            node.Radius = sys.StrandRadius * 0.5f;
        }

        foreach (var child in node.Children)
            ApplyOrganicHullRadiiRecursive(child, sys);
    }

    /// <summary>
    /// Vypočíta Konvexný Obal (Convex Hull) bodov pomocou Jarvis March (Gift Wrapping)
    /// a vráti maximálny polomer, ktorý tento obal dosahuje od stredu.
    /// Toto zabezpečí, že tvar rešpektuje asymetrické rozloženie strandov po PBD kolíziách.
    /// </summary>
    private static float ComputeHullRadius(List<int> strandIds, BranchNode node, StrandSystem sys)
    {
        List<Vector2> points = new List<Vector2>();
        foreach (int sid in strandIds)
        {
            if (sys.Strands[sid].ParticlePositions.ContainsKey(node))
                points.Add(sys.Strands[sid].ParticlePositions[node]);
        }

        if (points.Count == 0) return 0f;
        if (points.Count <= 2) return sys.StrandRadius * 1.5f;

        // Jarvis March algoritmus pre nájdenie hranice zväzku (Boundary)
        List<Vector2> hull = new List<Vector2>();
        Vector2 leftmost = points[0];
        foreach (var p in points) { if (p.x < leftmost.x) leftmost = p; }

        Vector2 currentPoint = leftmost;
        Vector2 nextPoint;

        do
        {
            hull.Add(currentPoint);
            nextPoint = points[0];

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 p = points[i];
                if (p == currentPoint) continue;
                
                // Cross product na zistenie najľavejšieho otočenia
                float cross = (nextPoint.x - currentPoint.x) * (p.y - currentPoint.y) - 
                              (nextPoint.y - currentPoint.y) * (p.x - currentPoint.x);
                              
                if (nextPoint == currentPoint || cross < 0)
                    nextPoint = p;
            }
            currentPoint = nextPoint;

        // Ochrana pred zacyklením a kontrola dokončenia obalu
        } while (currentPoint != leftmost && hull.Count < points.Count);

        // Zistíme maximálnu vzdialenosť bodu na obale od stredu (origin)
        float maxDistSq = 0f;
        foreach (var p in hull)
        {
            if (p.sqrMagnitude > maxDistSq)
                maxDistSq = p.sqrMagnitude;
        }

        // Vrátime najväčšiu vzdialenosť plus rezervu pre hrúbku samotného strandu
        return Mathf.Sqrt(maxDistSq) + sys.StrandRadius;
    }
}