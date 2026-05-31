#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public static class TreeSpeciesPresetFactoryMenu
{
    [MenuItem("Assets/Create/Tree/Presets/Dub (Oak)", priority = 0)]
    public static void CreateOak()
    {
        SaveAsAsset(TreeSpeciesPreset.CreateOakPreset(), "Dub.asset");
    }

    [MenuItem("Assets/Create/Tree/Presets/Vŕba (Willow)", priority = 2)]
    public static void CreateWillow()
    {
        SaveAsAsset(TreeSpeciesPreset.CreateWillowPreset(), "Vrba.asset");
    }

    [MenuItem("Assets/Create/Tree/Presets/[Vytvor všetky druhy]", priority = -10)]
    public static void CreateAllPresets()
    {
        string folder = GetCurrentAssetFolder();
        string presetsFolder = $"{folder}/TreePresets";

        if (!AssetDatabase.IsValidFolder(presetsFolder))
        {
            AssetDatabase.CreateFolder(folder, "TreePresets");
        }

        SaveToFolder(TreeSpeciesPreset.CreateOakPreset(), presetsFolder, "Dub.asset");
        SaveToFolder(TreeSpeciesPreset.CreateWillowPreset(), presetsFolder, "Vrba.asset");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[TreePresets] Vytvorené presety v {presetsFolder}");
    }

    private static void SaveAsAsset(TreeSpeciesPreset preset, string defaultFileName)
    {
        string folder = GetCurrentAssetFolder();
        string fullPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{defaultFileName}");

        AssetDatabase.CreateAsset(preset, fullPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = preset;
        Debug.Log($"[TreePresets] Vytvorený preset: {fullPath}");
    }

    private static void SaveToFolder(TreeSpeciesPreset preset, string folder, string fileName)
    {
        string fullPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}");
        AssetDatabase.CreateAsset(preset, fullPath);
    }

    private static string GetCurrentAssetFolder()
    {
        string path = "Assets";
        foreach (Object obj in Selection.GetFiltered(typeof(Object), SelectionMode.Assets))
        {
            path = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                path = Path.GetDirectoryName(path);
            break;
        }
        return path;
    }
}
#endif
