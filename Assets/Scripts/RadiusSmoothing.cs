using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Vyhladzovanie polomerov vetiev.
/// 
/// Problem: Na bodoch vetvenia polomer skoci z hrubky rodicaka
/// na tenku detsku vetvu. V prirode su tieto prechody hladke.
/// 
/// Riesenie: Laplacianove vyhladzovanie polomerov pozdlz vetiev
/// s ohladom na pipe model (zachovanie suctu prierezov).
/// Oprava: Pouzitie vazeneho priemeru pre eliminaciu "diamantovych" deformacii.
/// </summary>
public static class RadiusSmoothing
{
    /// <summary>
    /// Vyhlaď polomery celého stromu.
    /// </summary>
    /// <param name="root">Koreňový uzol</param>
    /// <param name="iterations">Počet vyhladovacích iterácií (1-5)</param>
    /// <param name="factor">Sila vyhladzenia (0-1, 0.3 = mierna)</param>
    public static void Smooth(BranchNode root, int iterations = 2, float factor = 0.3f)
    {
        // Zbieraj vsetky uzly
        List<BranchNode> allNodes = new List<BranchNode>();
        CollectAll(root, allNodes);

        // Uloz povodne radii (pipe model) ako cielove hodnoty
        Dictionary<BranchNode, float> targetRadii = new Dictionary<BranchNode, float>();
        foreach (var node in allNodes)
            targetRadii[node] = node.Radius;

        for (int iter = 0; iter < iterations; iter++)
        {
            foreach (var node in allNodes)
            {
                if (node.IsRoot && node.IsLeaf) continue;

                // Vazeny priemer susedov (hrubsie vetvy maju vacsiu vahu)
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

                    // Blend medzi aktualnym polomerom a priemerom susedov
                    float smoothed = Mathf.Lerp(node.Radius, neighborAvg, factor);

                    // Neprekroc pipe model cielovu hodnotu smerom nahor
                    // (nechceme aby vetvy boli hrubsie nez co pipe model predpisuje)
                    smoothed = Mathf.Min(smoothed, targetRadii[node] * 1.2f);

                    // Zachovaj minimalny polomer
                    smoothed = Mathf.Max(smoothed, targetRadii[node] * 0.5f);

                    node.Radius = smoothed;
                }
            }
        }
    }

    /// <summary>
    /// Pridaj taper (postupne zuzenie) pozdlz kazdej vetvy.
    /// Namiesto skokovej zmeny na konci segmentu sa polomer
    /// postupne znizuje od rodicaky k dietatu.
    /// </summary>
    /// <param name="root">Koreňový uzol</param>
    /// <param name="taperStrength">Sila zúženia (0-1)</param>
    public static void ApplyTaper(BranchNode root, float taperStrength = 0.1f)
    {
        ApplyTaperRecursive(root, taperStrength);
    }

    private static void ApplyTaperRecursive(BranchNode node, float taperStrength)
    {
        foreach (var child in node.Children)
        {
            // Polomer sa znizi pozdlz segmentu
            float parentR = node.Radius;
            float childR = child.Radius;

            // Taper: child radius sa mierne znizi smerom od rodicaky
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