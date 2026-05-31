using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(TreeGenerator))]
public class TreeInteractor : MonoBehaviour
{
    [Header("=== Interakcia ===")]
    public InteractionMode Mode = InteractionMode.Invigorate;

    [Range(0.5f, 10f)]
    public float VigorStrength = 3f;

    [Range(0.3f, 0.95f)]
    public float ApicalControl = 0.6f;

    [Range(1f, 5f)]
    public float GrowthThreshold = 2f;

    [Range(0.1f, 1f)]
    public float NewBranchLength = 0.3f;

    [Range(10f, 60f)]
    public float NewBranchAngle = 30f;

    public bool ShowVigorHeatmap = true;

    private TreeGenerator _generator;
    private MeshCollider _meshCollider;
    private Camera _mainCamera;
    private float _clickStartTime;
    private bool _isClicking;
    private BranchNode _hoveredNode;
    private BranchNode _selectedNode;
    private int _growSeed = 100;
    private Dictionary<int, BranchNode> _triangleToNode;

    void Start()
    {
        _generator = GetComponent<TreeGenerator>();
        _mainCamera = Camera.main;
        SetupCollider();
    }

    private void SetupCollider()
    {
        _meshCollider = GetComponent<MeshCollider>();
        if (_meshCollider == null)
            _meshCollider = gameObject.AddComponent<MeshCollider>();

        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
            _meshCollider.sharedMesh = mf.sharedMesh;

        BuildTriangleMap();
    }

    private void BuildTriangleMap()
    {
        _triangleToNode = new Dictionary<int, BranchNode>();
        if (_generator.RootNode == null) return;

        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;

        Mesh mesh = mf.sharedMesh;
        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;

        List<BranchNode> allNodes = new List<BranchNode>();
        CollectAllNodes(_generator.RootNode, allNodes);

        for (int i = 0; i < tris.Length; i += 3)
        {
            int triIndex = i / 3;
            Vector3 center = (verts[tris[i]] + verts[tris[i + 1]] + verts[tris[i + 2]]) / 3f;

            BranchNode closest = FindClosestNode(center, allNodes);
            if (closest != null)
                _triangleToNode[triIndex] = closest;
        }
    }

    void Update()
    {
        var mouse = Mouse.current;
        var kb = Keyboard.current;
        if (mouse == null || _mainCamera == null) return;

        Vector2 mousePos = mouse.position.ReadValue();
        Ray ray = _mainCamera.ScreenPointToRay(mousePos);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit) && hit.collider == _meshCollider)
        {
            int triIndex = hit.triangleIndex;
            if (_triangleToNode != null && _triangleToNode.ContainsKey(triIndex))
            {
                _hoveredNode = _triangleToNode[triIndex];
            }
            else
            {
                _hoveredNode = FindClosestNodeToWorldPoint(hit.point, _generator.RootNode);
            }
        }
        else
        {
            _hoveredNode = null;
        }

        if (mouse.leftButton.wasPressedThisFrame && _hoveredNode != null)
        {
            _clickStartTime = Time.time;
            _isClicking = true;
            _selectedNode = _hoveredNode;
        }

        if (mouse.leftButton.wasReleasedThisFrame && _isClicking && _selectedNode != null)
        {
            float duration = Time.time - _clickStartTime;
            float vigor = VigorStrength * Mathf.Max(duration, 0.2f);

            if (Mode == InteractionMode.Invigorate)
                PerformInvigoration(_selectedNode, vigor);
            else if (Mode == InteractionMode.AdventBud)
                PerformAdventitiousBud(_selectedNode, vigor);
            else if (Mode == InteractionMode.Prune)
                PerformPruning(_selectedNode);

            _isClicking = false;
            _selectedNode = null;
        }

        if (kb != null && kb.leftShiftKey.isPressed)
        {
            float scroll = mouse.scroll.y.ReadValue();
            if (Mathf.Abs(scroll) > 0.1f)
            {
                VigorStrength = Mathf.Clamp(VigorStrength + scroll * 0.01f, 0.5f, 10f);
            }
        }

        if (kb != null)
        {
            if (kb.digit1Key.wasPressedThisFrame) Mode = InteractionMode.Invigorate;
            if (kb.digit2Key.wasPressedThisFrame) Mode = InteractionMode.Prune;
            if (kb.digit3Key.wasPressedThisFrame) Mode = InteractionMode.AdventBud;
        }
    }


    private void PerformInvigoration(BranchNode node, float vigor)
    {
        VigorSystem.InjectVigor(node, vigor, ApicalControl);
        _growSeed++;

        int newNodes = VigorSystem.GrowFromVigor(
            node, GrowthThreshold,
            NewBranchLength, NewBranchAngle, _growSeed);

        if (newNodes > 0)
        {
            Debug.Log($"[Invigoration] Vigor {vigor:F1} → {newNodes} novych uzlov");
            RegenerateMesh();
        }
        else
        {
            Debug.Log($"[Invigoration] Vigor {vigor:F1} injektovany (pod prahom)");
        }
    }

    private void PerformAdventitiousBud(BranchNode node, float vigor)
    {
        node.Vigor = vigor * 2f;
        _growSeed++;

        int newNodes = VigorSystem.GrowFromVigor(
            node, 1f,
            NewBranchLength * 0.7f, NewBranchAngle * 1.2f, _growSeed);

        Debug.Log($"[Adventitious Bud] {newNodes} novych vyhonkov");
        RegenerateMesh();
    }

    private void PerformPruning(BranchNode node)
    {
        if (node.IsRoot)
        {
            Debug.Log("[Pruning] Nemozes orezat koren!");
            return;
        }
        int removed = VigorSystem.Prune(node);
        Debug.Log($"[Pruning] Odstranenych {removed} uzlov");
        RegenerateMesh();
    }

    private void RegenerateMesh()
    {
        _generator.RebuildMeshOnly();

        MeshFilter mf = GetComponent<MeshFilter>();
        if (_meshCollider != null && mf != null && mf.sharedMesh != null)
            _meshCollider.sharedMesh = mf.sharedMesh;

        BuildTriangleMap();
    }


    void OnDrawGizmos()
    {
        if (_generator == null || _generator.RootNode == null) return;

        if (ShowVigorHeatmap)
            DrawVigorHeatmap(_generator.RootNode);

        if (_hoveredNode != null)
        {
            Gizmos.color = Mode == InteractionMode.Prune
                ? new Color(1f, 0.2f, 0.2f, 0.8f)
                : new Color(0.2f, 1f, 0.4f, 0.8f);
            Gizmos.DrawWireSphere(
                _hoveredNode.Position + transform.position,
                Mathf.Max(_hoveredNode.Radius * 2f, 0.05f));
        }
    }

    private void DrawVigorHeatmap(BranchNode node)
    {
        if (node.Vigor > 1.1f)
        {
            float t = Mathf.Clamp01((node.Vigor - 1f) / 5f);
            Gizmos.color = Color.Lerp(
                new Color(0.2f, 0.6f, 1f, 0.6f),
                new Color(1f, 0.8f, 0.1f, 0.8f), t);
            Gizmos.DrawSphere(node.Position + transform.position,
                Mathf.Max(node.Radius, 0.02f));
        }
        foreach (var child in node.Children)
            DrawVigorHeatmap(child);
    }

    private BranchNode FindClosestNodeToWorldPoint(Vector3 worldPoint, BranchNode root)
    {
        List<BranchNode> allNodes = new List<BranchNode>();
        CollectAllNodes(root, allNodes);

        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        return FindClosestNode(localPoint, allNodes);
    }

    private static BranchNode FindClosestNode(Vector3 localPoint, List<BranchNode> nodes)
    {
        BranchNode closest = null;
        float minDistSq = float.MaxValue;

        foreach (var node in nodes)
        {
            if (node.Parent == null)
            {
                float rootDistSq = (node.Position - localPoint).sqrMagnitude;
                if (rootDistSq < minDistSq) { minDistSq = rootDistSq; closest = node; }
                continue;
            }

            Vector3 a = node.Parent.Position;
            Vector3 b = node.Position;
            Vector3 ab = b - a;

            float lengthSq = ab.sqrMagnitude;
            float distSq;

            if (lengthSq < 0.0001f)
            {
                distSq = (localPoint - a).sqrMagnitude;
            }
            else
            {
                float t = Mathf.Clamp01(Vector3.Dot(localPoint - a, ab) / lengthSq);
                Vector3 projectedPoint = a + t * ab;
                distSq = (localPoint - projectedPoint).sqrMagnitude;
            }

            if (distSq < minDistSq)
            {
                minDistSq = distSq;
                closest = node;
            }
        }
        return closest;
    }

    private static void CollectAllNodes(BranchNode node, List<BranchNode> list)
    {
        list.Add(node);
        foreach (var child in node.Children) CollectAllNodes(child, list);
    }


    void OnGUI()
    {
        if (!Application.isPlaying) return;

        GUILayout.BeginArea(new Rect(10, Screen.height - 210, 280, 160));
        GUI.Box(new Rect(0, 0, 280, 160), "");

        GUILayout.Space(5);
        GUILayout.Label("  Interaktivne Editovanie", GUI.skin.box);
        GUILayout.Space(5);

        GUILayout.BeginHorizontal();
        GUI.color = Mode == InteractionMode.Invigorate ? Color.green : Color.white;
        if (GUILayout.Button("1: Rast")) Mode = InteractionMode.Invigorate;
        GUI.color = Mode == InteractionMode.Prune ? Color.red : Color.white;
        if (GUILayout.Button("2: Orezanie")) Mode = InteractionMode.Prune;
        GUI.color = Mode == InteractionMode.AdventBud ? Color.cyan : Color.white;
        if (GUILayout.Button("3: Puky")) Mode = InteractionMode.AdventBud;
        GUI.color = Color.white;
        GUILayout.EndHorizontal();

        GUILayout.Label($"Sila vigoru: {VigorStrength:F1} (Shift+scroll)");

        if (_hoveredNode != null)
        {
            GUILayout.Label($"Uzol: depth={_hoveredNode.Depth}, vigor={_hoveredNode.Vigor:F1}");
            GUILayout.Label($"Polomer: {_hoveredNode.Radius:F4}, deti: {_hoveredNode.Children.Count}");
        }
        else
        {
            GUILayout.Label("Ukazuj mysou na strom...");
        }

        if (GUILayout.Button("Reset Vigor"))
        {
            VigorSystem.ResetVigor(_generator.RootNode);
        }

        GUILayout.EndArea();
    }
}

public enum InteractionMode
{
    Invigorate,
    Prune,
    AdventBud
}