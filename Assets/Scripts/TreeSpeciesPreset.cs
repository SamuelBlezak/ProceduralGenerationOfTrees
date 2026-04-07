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
    public float RadiusDecay = 0.75f;
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
        generator.RadiusDecay = RadiusDecay;
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
        RadiusDecay = generator.RadiusDecay;
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
        p.TrunkRadius = 0.4f;
        p.RadiusDecay = 0.72f;
        p.LengthDecay = 0.8f;
        p.AngleVariation = 8f;
        p.DivergenceAngle = 137.5f;
        p.Gravitropism = 0.06f;
        p.Phototropism = 0.05f;
        return p;
    }

    public static TreeSpeciesPreset CreateSprucePreset()
    {
        var p = CreateInstance<TreeSpeciesPreset>();
        p.SpeciesName = "Smrek";
        p.Description = "Ihličnan s kuželovitou korunou";
        p.BaseSpecies = TreeSpecies.Conifer;
        p.Iterations = 5;
        p.BranchAngle = 35f;
        p.SegmentLength = 0.6f;
        p.TrunkRadius = 0.25f;
        p.RadiusDecay = 0.68f;
        p.LengthDecay = 0.75f;
        p.AngleVariation = 3f;
        p.DivergenceAngle = 137.5f;
        p.Gravitropism = 0.12f;
        p.Phototropism = 0.02f;
        return p;
    }

    public static TreeSpeciesPreset CreateWillowPreset()
    {
        var p = CreateInstance<TreeSpeciesPreset>();
        p.SpeciesName = "Vŕba";
        p.Description = "Strom s prevísajúcimi vetvami";
        p.BaseSpecies = TreeSpecies.Willow;
        p.Iterations = 4;
        p.BranchAngle = 20f;
        p.SegmentLength = 1.2f;
        p.TrunkRadius = 0.35f;
        p.RadiusDecay = 0.8f;
        p.LengthDecay = 0.9f;
        p.AngleVariation = 6f;
        p.DivergenceAngle = 137.5f;
        p.Gravitropism = 0.25f;
        p.Phototropism = 0.08f;
        return p;
    }
}