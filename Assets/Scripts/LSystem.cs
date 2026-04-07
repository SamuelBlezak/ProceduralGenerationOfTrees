using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// L-System engine — prepisovaci system pre generovanie vetviacich struktur.
/// Podporuje deterministicke aj stochasticke pravidla.
/// </summary>
public class LSystem
{
    public string Axiom { get; private set; } /// pociatocny retazec

    /// slovnik s pravidlami
    /// kluc = znak, hodnota = zoznam pravidiel pre tento znak
    public Dictionary<char, List<LSystemRule>> Rules { get; private set; }

    /// seed pre nahodny generator
    /// rovnaky seed = rovnaky strom
    public int Seed { get; set; }

    /// max dlzka retazca (ochrana pred zamrznutim pri vysokych iteraciach)
    public int MaxStringLength { get; set; } = 500000;

    /// flag ak True tak posledne generovanie bolo obmedzene limitom (priliz dlhy string)
    public bool WasTruncated { get; private set; }

    /// <summary>
    /// Vytvorenie L-systému.
    /// Po inicializácii sa normalizujú pravdepodobnosti pravidiel,
    /// aby ich súčet bol približne 1.
    /// </summary>
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
        System.Random rng = new System.Random(Seed); // nahodny generator so zadanym seedom
        string current = Axiom;
        WasTruncated = false;

        /// opakovanie pre dany pocet iteracii
        for (int i = 0; i < iterations; i++)
        {
            /// predpoklad ze vysledok je dlhsi nez vstup
            StringBuilder next = new StringBuilder(current.Length * 2);


            /// prechadzanie kazdym znakom
            foreach (char c in current)
            {
                /// ak ma znak definovane pravidlo, pouzije ho
                if (Rules.ContainsKey(c))
                    next.Append(SelectRule(Rules[c], rng));
                else
                    next.Append(c);

                /// bezpecnostny limit proti prilis dlhemu retazcu
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

            /// novy retazec sa stane aktualnym
            current = next.ToString();

            /// ak sa prekrocil limit tak koniec generovania
            if (WasTruncated)
                break;
        }

        return current;
    }

    /// <summary>
    /// vyberie jedno pravidlo zo zoznamu.
    /// ak je len jedno pravidlo, vráti sa priamo.
    /// pri viacerych pravidlach sa vybera podla pravdepodobnosti
    /// </summary>
    private string SelectRule(List<LSystemRule> rules, System.Random rng)
    {
        if (rules.Count == 1)
            return rules[0].Successor;

        /// nahodne cislo medzi 0 a 1
        float roll = (float)rng.NextDouble();
        float cumulative = 0f;

        /// prechadzame cez pravidla, hladame prve, ktore spada do daneho intervalu
        foreach (var rule in rules)
        {
            cumulative += rule.Probability;
            if (roll <= cumulative)
                return rule.Successor;
        }
        /// zaloha ak by sa nenaslo 
        return rules[rules.Count - 1].Successor;
    }

    /// <summary>
    /// normalizuje pravdepodobnosti pravidiel tak, aby ich súčet bol 1
    /// </summary>
    private void NormalizeProbabilities(List<LSystemRule> rules)
    {
        float total = 0f;
        /// spocitaju sa vsetky pravdepodobnosti
        foreach (var rule in rules)
            total += rule.Probability;

        /// ak sucet nie je 1 a je kladny, prepocitaju sa pravdepodobnosti
        /// Mathf.Abs(total - 1f) > 0.001f -> ak je moc velky rozdiel v sucte pravdepodobnosti od 1 tak treba opravit
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
