using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[ExecuteAlways]
public class TreeGenerator : MonoBehaviour
{
    [Header("=== Preset Druhu ===")]
    public TreeSpeciesPreset SpeciesPreset;

    [Header("=== L-System Nastavenia ===")]
    [Range(1, 7)] public int Iterations = 4;
    public int Seed = 42;
    [HideInInspector] public TreeSpecies Species = TreeSpecies.Deciduous;

    [Header("=== Geometria Vetiev ===")]
    [Range(10f, 60f)] public float BranchAngle = 25f;
    [Range(0.1f, 3f)] public float SegmentLength = 1.0f;
    [Range(0.05f, 2f)] public float TrunkRadius = 0.3f;
    [Range(0.5f, 0.95f)] public float RadiusDecay = 0.75f;
    [Range(0.5f, 0.95f)] public float LengthDecay = 0.85f;
    [Range(0f, 20f)] public float AngleVariation = 5f;
    [Range(30f, 180f)] public float DivergenceAngle = 137.5f;
    [Range(0f, 0.5f)] public float Gravitropism = 0.08f;
    [Range(0f, 0.5f)] public float Phototropism = 0.05f;

    [Header("=== Lístie (Foliage) ===")]
    public bool EnableLeaves = true;
    [Range(0.5f, 3f)] public float LeafSize = 1.2f;
    [Range(2, 6)] public int LeavesPerNode = 3;
    public Color LeafColor = new Color(0.2f, 0.6f, 0.1f);

    [Header("=== Mesh Nastavenia ===")]
    [Range(4, 16)] public int RadialSegments = 8;
    [Range(-1, 10)] public int MaxMeshDepth = -1;
    public bool SmoothNormals = true;

    [Header("=== Vizualizacia ===")]
    public bool ShowSkeleton = false;
    public Color BarkColor = new Color(0.4f, 0.28f, 0.15f);
    public bool ProceduralBarkTexture = true;
    [Range(0f, 1f)] public float BarkRoughness = 0.6f;
    [Range(64, 512)] public int BarkTextureResolution = 256;
    public bool AutoRegenerate = true;

    private BranchNode _rootNode;
    private List<BranchSegment> _segments;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private GameObject _leavesObject;
    private int _lastHash;

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
        {
            if (ProceduralBarkTexture)
                _meshRenderer.sharedMaterial = BarkTextureGenerator.CreateBarkMaterial(
                    BarkColor, Seed, BarkRoughness, BarkTextureResolution);
            else
                _meshRenderer.sharedMaterial = CreateDefaultMaterial(BarkColor, "Bark");
        }
        GenerateTree();
    }

    void OnDisable() { CleanupMesh(); }

    void Update()
    {
        if (AutoRegenerate)
        {
            int hash = ComputeParameterHash();
            if (hash != _lastHash) { GenerateTree(); _lastHash = hash; }
        }
    }

    public void GenerateTree()
    {
        Stopwatch sw = Stopwatch.StartNew();
        CleanupMesh();

        var rules = GetRulesForSpecies(Species);
        LSystem lSystem = new LSystem(GetAxiomForSpecies(Species), rules, Seed);
        string lSystemString = lSystem.Generate(Iterations);
        LastLSystemLength = lSystemString.Length;
        LastWasTruncated = lSystem.WasTruncated;

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
            Phototropism = Phototropism,
            Seed = Seed
        };

        (_rootNode, _segments) = turtle.Interpret(lSystemString);
        LastNodeCount = CountNodes(_rootNode);

        RadiusSmoothing.Smooth(_rootNode, 2, 0.3f);
        Mesh mesh = BranchMeshGenerator.GenerateTreeMesh(_rootNode, RadialSegments, MaxMeshDepth, SmoothNormals);
        _meshFilter.sharedMesh = mesh;

        GenerateLeafMesh(); // Generovanie lístia

        sw.Stop();
        LastGenerationTimeMs = (float)sw.Elapsed.TotalMilliseconds;
        _lastHash = ComputeParameterHash();
    }

    public void RebuildMeshOnly()
    {
        if (_rootNode == null) return;

        // Uvoľnenie starého meshu (prevencia memory leaku)
        if (_meshFilter.sharedMesh != null)
        {
            if (Application.isPlaying) Destroy(_meshFilter.sharedMesh);
            else DestroyImmediate(_meshFilter.sharedMesh);
            _meshFilter.sharedMesh = null;
        }

        RadiusSmoothing.Smooth(_rootNode, 2, 0.3f);
        _meshFilter.sharedMesh = BranchMeshGenerator.GenerateTreeMesh(_rootNode, RadialSegments, -1, true);
        LastNodeCount = CountNodes(_rootNode);
        GenerateLeafMesh();
    }

    /// <summary>
    /// Pregeneruje materiál kôry s aktuálnymi parametrami.
    /// Volané z editora alebo pri zmene vizuálnych parametrov.
    /// </summary>
    public void RegenerateBarkMaterial()
    {
        if (_meshRenderer == null) return;

        // Uvoľni starý materiál
        if (_meshRenderer.sharedMaterial != null)
        {
            if (Application.isPlaying) Destroy(_meshRenderer.sharedMaterial);
            else DestroyImmediate(_meshRenderer.sharedMaterial);
        }

        if (ProceduralBarkTexture)
            _meshRenderer.sharedMaterial = BarkTextureGenerator.CreateBarkMaterial(
                BarkColor, Seed, BarkRoughness, BarkTextureResolution);
        else
            _meshRenderer.sharedMaterial = CreateDefaultMaterial(BarkColor, "Bark");
    }

    // === GENERÁTOR LÍSTIA ===

    private void GenerateLeafMesh()
    {
        if (_leavesObject != null) DestroyImmediate(_leavesObject);
        if (!EnableLeaves || _rootNode == null) return;

        List<BranchNode> leaves = new List<BranchNode>();
        CollectLeaves(_rootNode, leaves);

        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        System.Random rnd = new System.Random(Seed);

        foreach (var leaf in leaves)
        {
            // Smer rastu vetvy (pre orientáciu listov)
            Vector3 branchDir = Vector3.up;
            if (leaf.Parent != null)
                branchDir = (leaf.Position - leaf.Parent.Position).normalized;

            // Cluster listov okolo koncového uzla
            for (int i = 0; i < LeavesPerNode; i++)
            {
                // Náhodná veľkosť (70% - 130% základnej veľkosti)
                float sizeVariation = LeafSize * (0.7f + (float)rnd.NextDouble() * 0.6f);

                // Náhodný offset od uzla (listy nie sú presne na špičke)
                float offsetDist = sizeVariation * 0.3f * (float)rnd.NextDouble();
                Vector3 randomOffset = new Vector3(
                    (float)(rnd.NextDouble() * 2 - 1),
                    (float)(rnd.NextDouble() * 0.5),
                    (float)(rnd.NextDouble() * 2 - 1)
                ).normalized * offsetDist;

                Vector3 leafPos = leaf.Position + randomOffset;

                // Náhodná orientácia — listy sa natáčajú k svetlu (hore)
                // ale zachovávajú väzbu na smer vetvy
                float yaw = (float)rnd.NextDouble() * 360f;
                float pitch = 20f + (float)rnd.NextDouble() * 40f; // 20-60° od vertikály
                float roll = (float)rnd.NextDouble() * 20f - 10f;

                Quaternion rot = Quaternion.LookRotation(branchDir, Vector3.up)
                               * Quaternion.Euler(pitch, yaw, roll);

                Vector3 right = rot * Vector3.right * sizeVariation * 0.5f;
                Vector3 up = rot * Vector3.up * sizeVariation;

                // 4 rohové body quadu
                Vector3 p0 = leafPos - right;
                Vector3 p1 = leafPos + right;
                Vector3 p2 = leafPos - right + up;
                Vector3 p3 = leafPos + right + up;

                // Jeden quad stačí — materiál má Cull Off (obojstranný)
                int idx = verts.Count;
                verts.Add(p0); verts.Add(p1); verts.Add(p2); verts.Add(p3);
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1));

                tris.Add(idx); tris.Add(idx + 2); tris.Add(idx + 1);
                tris.Add(idx + 1); tris.Add(idx + 2); tris.Add(idx + 3);
            }
        }

        Mesh leafMesh = new Mesh { name = "LeavesMesh" };
        if (verts.Count > 65000) leafMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        leafMesh.SetVertices(verts);
        leafMesh.SetTriangles(tris, 0);
        leafMesh.SetUVs(0, uvs);
        leafMesh.RecalculateNormals();

        _leavesObject = new GameObject("Leaves");
        _leavesObject.transform.SetParent(this.transform, false);
        var mf = _leavesObject.AddComponent<MeshFilter>();
        var mr = _leavesObject.AddComponent<MeshRenderer>();

        mf.sharedMesh = leafMesh;
        mr.sharedMaterial = LeafTextureGenerator.CreateLeafMaterial(LeafColor, Seed);
    }

    private void CollectLeaves(BranchNode node, List<BranchNode> list)
    {
        if (node.IsLeaf) list.Add(node);
        foreach (var child in node.Children) CollectLeaves(child, list);
    }
    // ========================

    private void CleanupMesh()
    {
        if (_meshFilter != null && _meshFilter.sharedMesh != null)
        {
            if (Application.isPlaying) Destroy(_meshFilter.sharedMesh); else DestroyImmediate(_meshFilter.sharedMesh);
            _meshFilter.sharedMesh = null;
        }
        if (_leavesObject != null) DestroyImmediate(_leavesObject);
    }

    private int CountNodes(BranchNode node)
    {
        int count = 1;
        foreach (var child in node.Children) count += CountNodes(child);
        return count;
    }

    private Dictionary<char, List<LSystemRule>> GetRulesForSpecies(TreeSpecies species)
    {
        switch (species)
        {
            case TreeSpecies.Deciduous: return new Dictionary<char, List<LSystemRule>> { { 'F', new List<LSystemRule> { new LSystemRule("FF/[&+F-F-F]\\[-F+F+F]", 0.33f), new LSystemRule("FF\\[&-F+F]//[^+F-F]", 0.33f), new LSystemRule("FF/[^F-F+F]\\[&F+F-F]", 0.34f) } } };
            case TreeSpecies.Conifer: return new Dictionary<char, List<LSystemRule>> { { 'F', new List<LSystemRule> { new LSystemRule("FF", 1f) } }, { 'A', new List<LSystemRule> { new LSystemRule("F[&+A]/F[&-A]//+A", 0.5f), new LSystemRule("F[&A]\\F[&+A]//-A", 0.5f) } } };
            case TreeSpecies.Willow: return new Dictionary<char, List<LSystemRule>> { { 'F', new List<LSystemRule> { new LSystemRule("FF", 1f) } }, { 'A', new List<LSystemRule> { new LSystemRule("F[&&&+A]/F[&&&-A]\\F[&&A]", 0.5f), new LSystemRule("F[&&+A]//F[&&&-A]\\[&&A]", 0.5f) } } };
            case TreeSpecies.Bush: return new Dictionary<char, List<LSystemRule>> { { 'F', new List<LSystemRule> { new LSystemRule("F/[&+F]\\F[&-F]/[^F]", 0.4f), new LSystemRule("F\\[&+F]F/[^-F]", 0.3f), new LSystemRule("F/[^-F]\\F[&+F]", 0.3f) } } };
            case TreeSpecies.Palm: return new Dictionary<char, List<LSystemRule>> { { 'F', new List<LSystemRule> { new LSystemRule("FF", 1f) } }, { 'A', new List<LSystemRule> { new LSystemRule("F[&&+A]/[&&-A]//[&&&+A]///[&&&-A]", 0.5f), new LSystemRule("FF/[&&+A]//[&&-A]///[&&&A]", 0.5f) } } };
            default: return new Dictionary<char, List<LSystemRule>> { { 'F', new List<LSystemRule> { new LSystemRule("F[+F]F[-F]F", 1f) } } };
        }
    }

    private string GetAxiomForSpecies(TreeSpecies species) { return (species == TreeSpecies.Conifer || species == TreeSpecies.Willow || species == TreeSpecies.Palm) ? "A" : "F"; }

    void OnDrawGizmos()
    {
        if (!ShowSkeleton || _rootNode == null) return;
        DrawNodeGizmos(_rootNode);
    }

    private void DrawNodeGizmos(BranchNode node)
    {
        foreach (var child in node.Children)
        {
            Gizmos.color = Color.Lerp(Color.red, Color.yellow, Mathf.Clamp01(child.Depth / 5f));
            Gizmos.DrawLine(node.Position + transform.position, child.Position + transform.position);
            DrawNodeGizmos(child);
        }
    }

    private Material CreateDefaultMaterial(Color color, string name)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        bool isURP = shader != null;

        if (!isURP) shader = Shader.Find("Standard");

        Material mat = new Material(shader) { name = name + "Material" };

        if (isURP)
        {
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.0f);
        }
        else
        {
            // Pre starý (Standard) render pipeline
            mat.color = color;
            mat.SetFloat("_Glossiness", 0.0f);
        }

        return mat;
    }

    public void LoadPreset() { if (SpeciesPreset != null) SpeciesPreset.ApplyTo(this); }
    public void SaveToPreset()
    {
        if (SpeciesPreset != null)
        {
            SpeciesPreset.SaveFrom(this);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(SpeciesPreset);
#endif
        }
    }

    private int ComputeParameterHash()
    {
        int hash = Iterations.GetHashCode() ^ Seed ^ (int)Species;
        hash ^= BranchAngle.GetHashCode() ^ SegmentLength.GetHashCode();
        hash ^= TrunkRadius.GetHashCode() ^ RadiusDecay.GetHashCode();
        hash ^= LengthDecay.GetHashCode() ^ AngleVariation.GetHashCode();
        hash ^= DivergenceAngle.GetHashCode() ^ Gravitropism.GetHashCode();
        hash ^= Phototropism.GetHashCode() ^ (EnableLeaves ? 1 : 0);
        hash ^= LeafSize.GetHashCode() ^ RadialSegments ^ (LeavesPerNode << 16);
        hash ^= (SmoothNormals ? 256 : 0) ^ MaxMeshDepth;
        return hash;
    }
}
public enum TreeSpecies { Deciduous, Conifer, Willow, Bush, Palm }