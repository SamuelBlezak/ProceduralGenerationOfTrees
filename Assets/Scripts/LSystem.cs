using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// L-System engine — prepisovaci system pre generovanie vetviacich struktur.
/// Podporuje deterministicke aj stochasticke pravidla.
/// 
/// Zmeny oproti v1:
///   - Bezpecnostny limit na maximalnu dlzku retazca
///   - Varovanie pri prekroceni limitu
/// </summary>
public class LSystem
{
    public string Axiom { get; private set; }
    public Dictionary<char, List<LSystemRule>> Rules { get; private set; }
    public int Seed { get; set; }

    /// <summary>Maximalna dlzka retazca. Ochrana pred zamrznutim pri vysokych iteraciach.</summary>
    public int MaxStringLength { get; set; } = 500000;

    /// <summary>True ak posledne generovanie bolo obmedzene limitom.</summary>
    public bool WasTruncated { get; private set; }

    public LSystem(string axiom, Dictionary<char, List<LSystemRule>> rules, int seed = 42)
    {
        Axiom = axiom;
        Rules = rules;
        Seed = seed;

        foreach (var kvp in Rules)
            NormalizeProbabilities(kvp.Value);
    }

    /// <summary>
    /// Vygeneruj L-system retazec po zadanom pocte iteracii.
    /// </summary>
    public string Generate(int iterations)
    {
        System.Random rng = new System.Random(Seed);
        string current = Axiom;
        WasTruncated = false;

        for (int i = 0; i < iterations; i++)
        {
            StringBuilder next = new StringBuilder(current.Length * 2);

            foreach (char c in current)
            {
                if (Rules.ContainsKey(c))
                    next.Append(SelectRule(Rules[c], rng));
                else
                    next.Append(c);

                // Bezpecnostny limit
                if (next.Length > MaxStringLength)
                {
                    WasTruncated = true;
                    Debug.LogWarning(
                        $"[LSystem] Retazec prekrocil limit {MaxStringLength} znakov pri iteracii {i+1}. " +
                        $"Zniz pocet iteracii alebo zjednodus pravidla."
                    );
                    break;
                }
            }

            current = next.ToString();

            if (WasTruncated)
                break;
        }

        return current;
    }

    private string SelectRule(List<LSystemRule> rules, System.Random rng)
    {
        if (rules.Count == 1)
            return rules[0].Successor;

        float roll = (float)rng.NextDouble();
        float cumulative = 0f;

        foreach (var rule in rules)
        {
            cumulative += rule.Probability;
            if (roll <= cumulative)
                return rule.Successor;
        }

        return rules[rules.Count - 1].Successor;
    }

    private void NormalizeProbabilities(List<LSystemRule> rules)
    {
        float total = 0f;
        foreach (var rule in rules)
            total += rule.Probability;

        if (total > 0f && Mathf.Abs(total - 1f) > 0.001f)
        {
            for (int i = 0; i < rules.Count; i++)
                rules[i] = new LSystemRule(rules[i].Successor, rules[i].Probability / total);
        }
    }
}

[System.Serializable]
public struct LSystemRule
{
    public string Successor;
    public float Probability;

    public LSystemRule(string successor, float probability = 1f)
    {
        Successor = successor;
        Probability = probability;
    }
}
