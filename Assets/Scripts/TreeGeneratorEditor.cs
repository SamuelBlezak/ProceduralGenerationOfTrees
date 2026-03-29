using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(TreeGenerator))]
public class TreeGeneratorEditor : Editor
{
    private bool _showStats = true;

    public override void OnInspectorGUI()
    {
        TreeGenerator generator = (TreeGenerator)target;

        // Hlavne tlacidlo
        EditorGUILayout.Space(5);
        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
        if (GUILayout.Button("🌳  Generuj Strom  🌳", GUILayout.Height(35)))
        {
            Undo.RecordObject(generator, "Generate Tree");
            generator.GenerateTree();
            EditorUtility.SetDirty(generator);
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🎲 Nahodny Seed"))
        {
            Undo.RecordObject(generator, "Random Seed");
            generator.Seed = Random.Range(0, 99999);
            generator.GenerateTree();
            EditorUtility.SetDirty(generator);
        }
        if (GUILayout.Button("📋 Kopiruj Seed"))
        {
            GUIUtility.systemCopyBuffer = generator.Seed.ToString();
            Debug.Log($"Seed {generator.Seed} skopirovany do schranky.");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        DrawDefaultInspector();

        // Varovania
        if (generator.LastWasTruncated)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "L-system bol obmedzeny bezpecnostnym limitom! Zniz Iterations.",
                MessageType.Error
            );
        }

        if (generator.Iterations >= 6)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "Vysoky pocet iteracii moze sposobit pomalost. Odporucame 4-5.",
                MessageType.Warning
            );
        }

        // Statistiky
        EditorGUILayout.Space(10);
        _showStats = EditorGUILayout.Foldout(_showStats, "📊 Statistiky Stromu");
        if (_showStats)
        {
            EditorGUI.indentLevel++;

            MeshFilter mf = generator.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Mesh mesh = mf.sharedMesh;

                // Cas generovania s farebnym indikatorom
                string timeStr = $"{generator.LastGenerationTimeMs:F1} ms";
                if (generator.LastGenerationTimeMs > 500f)
                    timeStr += " ⚠️ pomale";
                EditorGUILayout.LabelField("Cas generovania", timeStr);

                EditorGUILayout.LabelField("Vrcholy (Vertices)", mesh.vertexCount.ToString("N0"));
                EditorGUILayout.LabelField("Trojuholniky", (mesh.triangles.Length / 3).ToString("N0"));
                EditorGUILayout.LabelField("Uzly grafu", generator.LastNodeCount.ToString("N0"));
                EditorGUILayout.LabelField("L-system dlzka", generator.LastLSystemLength.ToString("N0") + " znakov");

                if (generator.RootNode != null)
                {
                    int leafCount = CountLeaves(generator.RootNode);
                    int maxDepth = GetMaxDepth(generator.RootNode);
                    EditorGUILayout.LabelField("Koncove vetvy", leafCount.ToString());
                    EditorGUILayout.LabelField("Max hlbka", maxDepth.ToString());
                }
            }
            else
            {
                EditorGUILayout.LabelField("Strom este nebol vygenerovany.");
            }

            EditorGUI.indentLevel--;
        }
    }

    private int CountLeaves(BranchNode node)
    {
        if (node.IsLeaf) return 1;
        int count = 0;
        foreach (var child in node.Children)
            count += CountLeaves(child);
        return count;
    }

    private int GetMaxDepth(BranchNode node)
    {
        if (node.IsLeaf) return node.Depth;
        int max = node.Depth;
        foreach (var child in node.Children)
            max = Mathf.Max(max, GetMaxDepth(child));
        return max;
    }
}
#endif
