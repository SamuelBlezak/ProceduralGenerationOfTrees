using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewTreeSpecies", menuName = "Tree/Species Preset")]
public class TreeSpeciesPreset : ScriptableObject
{
    [Header("=== Základné ===")]
    public string SpeciesName = "Nový Druh";
    [TextArea(2, 4)]
    public string Description = "";
    public TreeSpecies BaseSpecies = TreeSpecies.Deciduous;

    [Header("=== L-System ===")]
    [Range(2, 6)]
    public int Iterations = 4;

    [Header("=== Geometria ===")]
    [Range(10f, 60f)]
    public float BranchAngle = 25f;
    [Range(0.1f, 3f)]
    public float SegmentLength = 1f;
    [Range(0.05f, 2f)]
    public float TrunkRadius = 0.3f;
    [Range(0.5f, 0.95f)]
    public float LengthDecay = 0.85f;
    [Range(0f, 20f)]
    public float AngleVariation = 5f;
    [Range(30f, 180f)]
    public float DivergenceAngle = 137.5f;
    [Range(0f, 0.5f)]
    public float Gravitropism = 0.08f;
    [Range(0f, 0.5f)]
    public float Phototropism = 0.05f;

    public void ApplyTo(TreeGenerator generator)
    {
        generator.Species = BaseSpecies;
        generator.Iterations = Iterations;
        generator.BranchAngle = BranchAngle;
        generator.SegmentLength = SegmentLength;
        generator.TrunkRadius = TrunkRadius;
        generator.LengthDecay = LengthDecay;
        generator.AngleVariation = AngleVariation;
        generator.DivergenceAngle = DivergenceAngle;
        generator.Gravitropism = Gravitropism;
        generator.Phototropism = Phototropism;
    }

    public void SaveFrom(TreeGenerator generator)
    {
        BaseSpecies = generator.Species;
        Iterations = generator.Iterations;
        BranchAngle = generator.BranchAngle;
        SegmentLength = generator.SegmentLength;
        TrunkRadius = generator.TrunkRadius;
        LengthDecay = generator.LengthDecay;
        AngleVariation = generator.AngleVariation;
        DivergenceAngle = generator.DivergenceAngle;
        Gravitropism = generator.Gravitropism;
        Phototropism = generator.Phototropism;
    }

    public static TreeSpeciesPreset CreateOakPreset()
    {
        var p = CreateInstance<TreeSpeciesPreset>();
        p.SpeciesName = "Dub";
        p.Description = "Mohutny listnatý strom so sirokymi vetvami";
        p.BaseSpecies = TreeSpecies.Deciduous;
        p.Iterations = 4;
        p.BranchAngle = 30f;
        p.SegmentLength = 0.8f;
        // Mohutný kmeň — typický znak duba
        p.TrunkRadius = 0.55f;
        p.LengthDecay = 0.8f;
        p.AngleVariation = 8f;
        p.DivergenceAngle = 137.5f;
        p.Gravitropism = 0.06f;
        p.Phototropism = 0.05f;
        return p;
    }

    public static TreeSpeciesPreset CreateWillowPreset()
    {
        var p = CreateInstance<TreeSpeciesPreset>();
        p.SpeciesName = "Vŕba";
        p.Description = "Strom s dlhými prevísajúcimi vetvami";
        p.BaseSpecies = TreeSpecies.Willow;
        // Vyššie iterations pre vyšší kmeň a dlhšie prevísajúce vetvy
        p.Iterations = 5;
        p.BranchAngle = 18f;
        // Výrazne dlhšie segmenty - vŕba je vysoký strom
        p.SegmentLength = 1.5f;
        p.TrunkRadius = 0.42f;
        // Pomalé skracovanie — vŕba má dlhé vetvy
        p.LengthDecay = 0.88f;
        p.AngleVariation = 5f;
        p.DivergenceAngle = 90f;
        // Silný gravitropizmus = typické prevísanie
        p.Gravitropism = 0.35f;
        p.Phototropism = 0.0f;
        return p;
    }
}