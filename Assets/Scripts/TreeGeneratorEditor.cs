using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using System.IO;
using System.Text;
using System.Globalization;

[CustomEditor(typeof(TreeGenerator))]
public class TreeGeneratorEditor : Editor
{
    private bool _showStats = true;

    public override void OnInspectorGUI()
    {
        TreeGenerator generator = (TreeGenerator)target;

        EditorGUILayout.Space(5);
        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
        if (GUILayout.Button("Generuj Strom", GUILayout.Height(35)))
        {
            Undo.RecordObject(generator, "Generate Tree");
            generator.GenerateTree();
            EditorUtility.SetDirty(generator);
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Nahodny Seed"))
        {
            Undo.RecordObject(generator, "Random Seed");
            generator.Seed = Random.Range(0, 99999);
            generator.GenerateTree();
            EditorUtility.SetDirty(generator);
        }
        if (GUILayout.Button("Kopiruj Seed"))
        {
            GUIUtility.systemCopyBuffer = generator.Seed.ToString();
            Debug.Log($"Seed {generator.Seed} skopirovany do schranky.");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        if (generator.SpeciesPreset != null)
        {
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
            if (GUILayout.Button("Nacitaj Preset"))
            {
                Undo.RecordObject(generator, "Load Preset");
                generator.LoadPreset();
                generator.GenerateTree();
                EditorUtility.SetDirty(generator);
            }
            GUI.backgroundColor = new Color(1f, 0.9f, 0.6f);
            if (GUILayout.Button("Uloz do Presetu"))
            {
                Undo.RecordObject(generator.SpeciesPreset, "Save Preset");
                generator.SaveToPreset();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(5);

        GUI.backgroundColor = new Color(0.2f, 0.6f, 0.9f);
        if (GUILayout.Button("Exportuj do .OBJ (Pre Blender)", GUILayout.Height(25)))
        {
            ExportTreeToObj(generator);
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(5);

        GUI.backgroundColor = new Color(0.6f, 0.9f, 0.7f);
        if (GUILayout.Button("Prebuduj Mesh (Update z grafu)", GUILayout.Height(25)))
        {
            Undo.RecordObject(generator, "Rebuild Mesh");
            generator.RebuildMeshOnly();
            EditorUtility.SetDirty(generator);
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(5);

        GUI.backgroundColor = new Color(0.85f, 0.65f, 0.4f);
        if (GUILayout.Button("Pregeneruj Textúru Kôry", GUILayout.Height(25)))
        {
            Undo.RecordObject(generator, "Regenerate Bark");
            generator.RegenerateBarkMaterial();
            EditorUtility.SetDirty(generator);
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(10);
        DrawDefaultInspector();

        if (generator.LastWasTruncated)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("L-system bol obmedzeny bezpecnostnym limitom! Zniz Iterations.", MessageType.Error);
        }

        if (generator.Iterations >= 6)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("Vysoky pocet iteracii moze sposobit pomalost. Odporucame 4-5.", MessageType.Warning);
        }

        EditorGUILayout.Space(10);
        _showStats = EditorGUILayout.Foldout(_showStats, "Statistiky Stromu");
        if (_showStats)
        {
            EditorGUI.indentLevel++;

            MeshFilter mf = generator.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Mesh mesh = mf.sharedMesh;
                string timeStr = $"{generator.LastGenerationTimeMs:F1} ms";
                if (generator.LastGenerationTimeMs > 500f) timeStr += "pomale";

                EditorGUILayout.LabelField("Cas generovania", timeStr);
                EditorGUILayout.LabelField("Vrcholy (Vertices)", mesh.vertexCount.ToString("N0"));
                EditorGUILayout.LabelField("Trojuholniky", (mesh.triangles.Length / 3).ToString("N0"));
                EditorGUILayout.LabelField("Uzly grafu", generator.LastNodeCount.ToString("N0"));
                EditorGUILayout.LabelField("L-system dlzka", generator.LastLSystemLength.ToString("N0") + " znakov");

                if (generator.RootNode != null)
                {
                    EditorGUILayout.LabelField("Koncove vetvy", CountLeaves(generator.RootNode).ToString());
                    EditorGUILayout.LabelField("Max hlbka", GetMaxDepth(generator.RootNode).ToString());
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
        foreach (var child in node.Children) count += CountLeaves(child);
        return count;
    }

    private int GetMaxDepth(BranchNode node)
    {
        if (node.IsLeaf) return node.Depth;
        int max = node.Depth;
        foreach (var child in node.Children) max = Mathf.Max(max, GetMaxDepth(child));
        return max;
    }

    private void ExportTreeToObj(TreeGenerator generator)
    {
        MeshFilter mf = generator.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            EditorUtility.DisplayDialog("Chyba", "Najprv vygeneruj strom!", "OK");
            return;
        }

        string path = EditorUtility.SaveFilePanel("Exportuj Strom ako OBJ", "", "ProceduralTree.obj", "obj");
        if (string.IsNullOrEmpty(path)) return;

        Mesh mesh = mf.sharedMesh;
        StringBuilder sb = new StringBuilder();
        CultureInfo ci = CultureInfo.InvariantCulture;

        sb.AppendLine("# Generovane pomocou Procedural Tree Generator");
        sb.AppendLine("o ProceduralTree");

        foreach (Vector3 v in mesh.vertices) sb.AppendLine(string.Format(ci, "v {0:F6} {1:F6} {2:F6}", -v.x, v.y, v.z));
        foreach (Vector2 uv in mesh.uv) sb.AppendLine(string.Format(ci, "vt {0:F6} {1:F6}", uv.x, uv.y));
        foreach (Vector3 n in mesh.normals) sb.AppendLine(string.Format(ci, "vn {0:F6} {1:F6} {2:F6}", -n.x, n.y, n.z));

        for (int i = 0; i < mesh.triangles.Length; i += 3)
        {
            int t1 = mesh.triangles[i] + 1;
            int t2 = mesh.triangles[i + 1] + 1;
            int t3 = mesh.triangles[i + 2] + 1;
            sb.AppendLine($"f {t3}/{t3}/{t3} {t2}/{t2}/{t2} {t1}/{t1}/{t1}");
        }

        File.WriteAllText(path, sb.ToString());
        Debug.Log($"[Export] Strom ulozeny do: {path}");
        EditorUtility.DisplayDialog("Uspesny Export", "Strom bol uspesne exportovany do OBJ formatu!\nOtvor ho v Blenderi alebo 3D Vieweri.", "Super");
    }
}
#endif