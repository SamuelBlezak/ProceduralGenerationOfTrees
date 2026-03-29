using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Hlavny komponent pre proceduralne generovanie stromov.
/// 
/// Zmeny oproti v1:
///   - Opraveny memory leak (stare meshe sa uvolnuju)
///   - Pridana gravitropia
///   - Meranie casu generovania
///   - Bezpecnostny limit pre L-system
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[ExecuteAlways]
public class TreeGenerator : MonoBehaviour
{
    [Header("=== L-System Nastavenia ===")]
    [Tooltip("Pocet iteracii L-systemu (3-6). Viac = zlozitejsi strom.")]
    [Range(1, 7)]
    public int Iterations = 4;

    [Tooltip("Seed pre nahodne generovanie.")]
    public int Seed = 42;

    [Tooltip("Prednastaveny druh stromu")]
    public TreeSpecies Species = TreeSpecies.Deciduous;

    [Header("=== Geometria Vetiev ===")]
    [Tooltip("Uhol vetvenia v stupnoch")]
    [Range(10f, 60f)]
    public float BranchAngle = 25f;

    [Tooltip("Dlzka jedneho segmentu vetvy")]
    [Range(0.1f, 3f)]
    public float SegmentLength = 1.0f;

    [Tooltip("Polomer kmena")]
    [Range(0.05f, 2f)]
    public float TrunkRadius = 0.3f;

    [Tooltip("Faktor zmensenia polomeru pri vetveni")]
    [Range(0.5f, 0.95f)]
    public float RadiusDecay = 0.75f;

    [Tooltip("Faktor zmensenia dlzky pri vetveni")]
    [Range(0.5f, 0.95f)]
    public float LengthDecay = 0.85f;

    [Tooltip("Nahodna variacia uhla pre prirodzenejsi vzhled")]
    [Range(0f, 20f)]
    public float AngleVariation = 5f;

    [Tooltip("Divergentny uhol — rozlozenie vetiev okolo kmena (137.5 = zlaty uhol)")]
    [Range(30f, 180f)]
    public float DivergenceAngle = 137.5f;

    [Tooltip("Gravitropia — ohyb vetiev smerom nadol (0=ziadna, 0.08=mierna, 0.3=silna)")]
    [Range(0f, 0.5f)]
    public float Gravitropism = 0.08f;

    [Header("=== Mesh Nastavenia ===")]
    [Tooltip("Pocet bodov po obvode prierezu (kvalita mesh)")]
    [Range(4, 16)]
    public int RadialSegments = 8;

    [Tooltip("Maximalna hlbka vetvenia pre mesh (-1 = vsetky)")]
    [Range(-1, 10)]
    public int MaxMeshDepth = -1;

    [Tooltip("Laplacianove vyhladzovanie normalov")]
    public bool SmoothNormals = true;

    [Header("=== Vizualizacia ===")]
    [Tooltip("Zobrazit skeletalny graf (Debug)")]
    public bool ShowSkeleton = false;

    [Tooltip("Farba kory")]
    public Color BarkColor = new Color(0.4f, 0.28f, 0.15f);

    [Tooltip("Automaticka regeneracia pri zmene parametrov")]
    public bool AutoRegenerate = true;

    // Interne premenne
    private BranchNode _rootNode;
    private List<BranchSegment> _segments;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private int _lastHash;

    // Statistiky — pristupne z Editora
    [HideInInspector] public float LastGenerationTimeMs;
    [HideInInspector] public int LastNodeCount;
    [HideInInspector] public int LastLSystemLength;
    [HideInInspector] public bool LastWasTruncated;

    public BranchNode RootNode => _rootNode;

    void OnEnable()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();

        if (_meshRenderer.sharedMaterial == null)
            _meshRenderer.sharedMaterial = CreateDefaultMaterial();

        GenerateTree();
    }

    void OnDisable()
    {
        // Uvolni mesh pri deaktivacii
        CleanupMesh();
    }

    void Update()
    {
        if (AutoRegenerate)
        {
            int hash = ComputeParameterHash();
            if (hash != _lastHash)
            {
                GenerateTree();
                _lastHash = hash;
            }
        }
    }

    /// <summary>
    /// Hlavna metoda — vygeneruj cely strom.
    /// </summary>
    public void GenerateTree()
    {
        Stopwatch sw = Stopwatch.StartNew();

        // Uvolni stary mesh
        CleanupMesh();

        // 1. L-system
        var rules = GetRulesForSpecies(Species);
        LSystem lSystem = new LSystem(GetAxiomForSpecies(Species), rules, Seed);
        string lSystemString = lSystem.Generate(Iterations);
        LastLSystemLength = lSystemString.Length;
        LastWasTruncated = lSystem.WasTruncated;

        // 2. Turtle interpreter
        TurtleInterpreter turtle = new TurtleInterpreter
        {
            Angle = BranchAngle,
            StepLength = SegmentLength,
            InitialRadius = TrunkRadius,
            RadiusDecay = RadiusDecay,
            LengthDecay = LengthDecay,
            AngleVariation = AngleVariation,
            DivergenceAngle = DivergenceAngle,
            Gravitropism = Gravitropism,
            Seed = Seed
        };

        (_rootNode, _segments) = turtle.Interpret(lSystemString);
        LastNodeCount = CountNodes(_rootNode);

        // 3. Mesh
        Mesh mesh = BranchMeshGenerator.GenerateTreeMesh(
            _rootNode, RadialSegments, MaxMeshDepth, SmoothNormals
        );
        _meshFilter.sharedMesh = mesh;

        sw.Stop();
        LastGenerationTimeMs = (float)sw.Elapsed.TotalMilliseconds;
        _lastHash = ComputeParameterHash();
    }

    /// <summary>
    /// Uvolni stary mesh aby nedochadzalo k memory leakom.
    /// </summary>
    private void CleanupMesh()
    {
        if (_meshFilter == null) return;

        Mesh oldMesh = _meshFilter.sharedMesh;
        if (oldMesh != null)
        {
            _meshFilter.sharedMesh = null;
            #if UNITY_EDITOR
            if (!UnityEditor.AssetDatabase.Contains(oldMesh))
                DestroyImmediate(oldMesh);
            #else
            Destroy(oldMesh);
            #endif
        }
    }

    private int CountNodes(BranchNode node)
    {
        int count = 1;
        foreach (var child in node.Children)
            count += CountNodes(child);
        return count;
    }

    private Dictionary<char, List<LSystemRule>> GetRulesForSpecies(TreeSpecies species)
    {
        switch (species)
        {
            case TreeSpecies.Deciduous:
                return new Dictionary<char, List<LSystemRule>>
                {
                    { 'F', new List<LSystemRule>
                        {
                            new LSystemRule("FF/[&+F-F-F]\\[-F+F+F]", 0.33f),
                            new LSystemRule("FF\\[&-F+F]//[^+F-F]", 0.33f),
                            new LSystemRule("FF/[^F-F+F]\\[&F+F-F]", 0.34f)
                        }
                    }
                };

            case TreeSpecies.Conifer:
                return new Dictionary<char, List<LSystemRule>>
                {
                    { 'F', new List<LSystemRule> { new LSystemRule("FF", 1f) } },
                    { 'A', new List<LSystemRule>
                        {
                            new LSystemRule("F[&+A]/F[&-A]//+A", 0.5f),
                            new LSystemRule("F[&A]\\F[&+A]//-A", 0.5f)
                        }
                    }
                };

            case TreeSpecies.Willow:
                return new Dictionary<char, List<LSystemRule>>
                {
                    { 'F', new List<LSystemRule> { new LSystemRule("FF", 1f) } },
                    { 'A', new List<LSystemRule>
                        {
                            new LSystemRule("F[&&&+A]/F[&&&-A]\\F[&&A]", 0.5f),
                            new LSystemRule("F[&&+A]//F[&&&-A]\\[&&A]", 0.5f)
                        }
                    }
                };

            case TreeSpecies.Bush:
                return new Dictionary<char, List<LSystemRule>>
                {
                    { 'F', new List<LSystemRule>
                        {
                            new LSystemRule("F/[&+F]\\F[&-F]/[^F]", 0.4f),
                            new LSystemRule("F\\[&+F]F/[^-F]", 0.3f),
                            new LSystemRule("F/[^-F]\\F[&+F]", 0.3f)
                        }
                    }
                };

            case TreeSpecies.Palm:
                return new Dictionary<char, List<LSystemRule>>
                {
                    { 'F', new List<LSystemRule> { new LSystemRule("FF", 1f) } },
                    { 'A', new List<LSystemRule>
                        {
                            new LSystemRule("F[&&+A]/[&&-A]//[&&&+A]///[&&&-A]", 0.5f),
                            new LSystemRule("FF/[&&+A]//[&&-A]///[&&&A]", 0.5f)
                        }
                    }
                };

            default:
                return new Dictionary<char, List<LSystemRule>>
                {
                    { 'F', new List<LSystemRule> { new LSystemRule("F[+F]F[-F]F", 1f) } }
                };
        }
    }

    private string GetAxiomForSpecies(TreeSpecies species)
    {
        switch (species)
        {
            case TreeSpecies.Conifer:
            case TreeSpecies.Willow:
            case TreeSpecies.Palm:
                return "A";
            default:
                return "F";
        }
    }

    void OnDrawGizmos()
    {
        if (!ShowSkeleton || _rootNode == null) return;
        DrawNodeGizmos(_rootNode);
    }

    private void DrawNodeGizmos(BranchNode node)
    {
        foreach (var child in node.Children)
        {
            float t = Mathf.Clamp01(child.Depth / 5f);
            Gizmos.color = Color.Lerp(Color.red, Color.yellow, t);
            Gizmos.DrawLine(node.Position + transform.position,
                           child.Position + transform.position);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(child.Position + transform.position, child.Radius * 0.5f);

            DrawNodeGizmos(child);
        }
    }

    private Material CreateDefaultMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.name = "BarkMaterial";
        mat.color = BarkColor;
        return mat;
    }

    private int ComputeParameterHash()
    {
        int hash = 17;
        hash = hash * 31 + Iterations;
        hash = hash * 31 + Seed;
        hash = hash * 31 + (int)Species;
        hash = hash * 31 + BranchAngle.GetHashCode();
        hash = hash * 31 + SegmentLength.GetHashCode();
        hash = hash * 31 + TrunkRadius.GetHashCode();
        hash = hash * 31 + RadiusDecay.GetHashCode();
        hash = hash * 31 + LengthDecay.GetHashCode();
        hash = hash * 31 + AngleVariation.GetHashCode();
        hash = hash * 31 + DivergenceAngle.GetHashCode();
        hash = hash * 31 + Gravitropism.GetHashCode();
        hash = hash * 31 + RadialSegments;
        hash = hash * 31 + MaxMeshDepth;
        hash = hash * 31 + (SmoothNormals ? 1 : 0);
        return hash;
    }
}

public enum TreeSpecies
{
    Deciduous,  // Listnatý (dub, javor)
    Conifer,    // Ihličnan (smrek, borovica)
    Willow,     // Vŕba
    Bush,       // Ker
    Palm        // Palma
}
