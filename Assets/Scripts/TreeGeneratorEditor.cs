using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using System.IO;
using System.Text;
using System.Globalization; // <-- PRIDANE PRE SPRAVNE FORMATOVANIE CISEL

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

        EditorGUILayout.Space(5);

        // --- TLACIDLO NA EXPORT DO BLENDERU ---
        GUI.backgroundColor = new Color(0.2f, 0.6f, 0.9f);
        if (GUILayout.Button("💾 Exportuj do .OBJ (Pre Blender)", GUILayout.Height(25)))
        {
            ExportTreeToObj(generator);
        }
        GUI.backgroundColor = Color.white;
        // ---------------------------------------------

        EditorGUILayout.Space(10);

        DrawDefaultInspector();

        // Varovania
        if (generator.UseStrands)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "Strand mod: AutoRegenerate je vypnute. Pouzi tlacidlo 'Generuj Strom' po zmene parametrov.",
                MessageType.Info
            );
        }

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

                string timeStr = $"{generator.LastGenerationTimeMs:F1} ms";
                if (generator.LastGenerationTimeMs > 500f)
                    timeStr += " ⚠️ pomale";
                EditorGUILayout.LabelField("Cas generovania", timeStr);

                EditorGUILayout.LabelField("Vrcholy (Vertices)", mesh.vertexCount.ToString("N0"));
                EditorGUILayout.LabelField("Trojuholniky", (mesh.triangles.Length / 3).ToString("N0"));
                EditorGUILayout.LabelField("Uzly grafu", generator.LastNodeCount.ToString("N0"));
                EditorGUILayout.LabelField("L-system dlzka", generator.LastLSystemLength.ToString("N0") + " znakov");

                if (generator.UseStrands)
                {
                    EditorGUILayout.LabelField("Pocet strandov", generator.LastStrandCount.ToString("N0"));
                }

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

    // --- LOGIKA PRE EXPORT DO OBJ S OPRAVENOU KULTUROU (BODKY NAMIESTO CIAROK) ---
    private void ExportTreeToObj(TreeGenerator generator)
    {
        MeshFilter mf = generator.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            EditorUtility.DisplayDialog("Chyba", "Najprv vygeneruj strom, az potom ho mozes exportovat!", "OK");
            return;
        }

        string path = EditorUtility.SaveFilePanel("Exportuj Strom ako OBJ", "", "ProceduralTree.obj", "obj");
        if (string.IsNullOrEmpty(path)) return;

        Mesh mesh = mf.sharedMesh;
        StringBuilder sb = new StringBuilder();
        
        sb.AppendLine("# Generovane pomocou Procedural Tree Generator");
        sb.AppendLine("o ProceduralTree");

        // OPRAVA: Povieme C#, aby vzdy pouzival americky format cisel (s bodkami)
        CultureInfo ci = CultureInfo.InvariantCulture;

        // 1. Zapiseme vrcholy (Prevod z Lavo-tociveho do Pravo-tociveho systemu)
        foreach (Vector3 v in mesh.vertices)
            sb.AppendLine(string.Format(ci, "v {0:F6} {1:F6} {2:F6}", -v.x, v.y, v.z));

        // 2. Zapiseme UV mapu
        foreach (Vector2 uv in mesh.uv)
            sb.AppendLine(string.Format(ci, "vt {0:F6} {1:F6}", uv.x, uv.y));

        // 3. Zapiseme normaly
        foreach (Vector3 n in mesh.normals)
            sb.AppendLine(string.Format(ci, "vn {0:F6} {1:F6} {2:F6}", -n.x, n.y, n.z));

        // 4. Zapiseme trojuholniky (plochy) - tie su bez desatinnych miest, takze su bezpecne
        for (int i = 0; i < mesh.triangles.Length; i += 3)
        {
            int t1 = mesh.triangles[i] + 1;
            int t2 = mesh.triangles[i + 1] + 1;
            int t3 = mesh.triangles[i + 2] + 1;
            
            sb.AppendLine($"f {t3}/{t3}/{t3} {t2}/{t2}/{t2} {t1}/{t1}/{t1}");
        }

        File.WriteAllText(path, sb.ToString());
        Debug.Log($"[Export Uspesny] Strom bol ulozeny do: {path}");
        EditorUtility.DisplayDialog("Uspesny Export", "Strom bol uspesne exportovany do OBJ formatu!\nTeraz ho mozes otvorit v Blenderi alebo 3D Vieweri.", "Super");
    }
}
#endif