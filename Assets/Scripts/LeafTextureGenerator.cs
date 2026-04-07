using UnityEngine;

/// <summary>
/// Procedurálny generátor textúry listu.
/// Vytvára jednoduchú textúru listu s alpha cutout pre realistickejší vzhľad.
/// 
/// Tvar listu je definovaný eliptickou funkciou s pilovitým okrajom
/// a žilkovou štruktúrou (veins) pre prirodzený vzhľad.
/// </summary>
public static class LeafTextureGenerator
{
    /// <summary>
    /// Vygeneruje procedurálnu textúru listu.
    /// </summary>
    /// <param name="baseColor">Základná farba listu</param>
    /// <param name="resolution">Rozlíšenie textúry</param>
    /// <param name="seed">Seed pre variáciu</param>
    public static Texture2D GenerateLeafTexture(Color baseColor, int resolution = 128, int seed = 42)
    {
        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true);
        tex.name = "ProceduralLeaf";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        System.Random rng = new System.Random(seed);
        Color[] pixels = new Color[resolution * resolution];
        float offsetX = (float)rng.NextDouble() * 100f;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                // Normalizované koordináty (0-1)
                float u = (float)x / resolution;
                float v = (float)y / resolution;

                // Posun stredu na (0.5, 0.35) — list má stopku dole
                float cx = u - 0.5f;
                float cy = v - 0.35f;

                // === Tvar listu: eliptická funkcia s variáciou ===
                // List je širší v hornej tretine a zúžený dole (pri stopke)
                float widthAtY = LeafWidth(v);
                float leafShape = Mathf.Abs(cx) / Mathf.Max(widthAtY, 0.001f);

                // Pilovitý okraj (zuby na obvode)
                float edgeNoise = Mathf.Sin(v * 25f + offsetX) * 0.03f
                                + Mathf.Sin(v * 13f + offsetX * 2f) * 0.02f;
                leafShape += edgeNoise;

                // Alpha: vnútri listu = 1, vonku = 0
                float alpha = leafShape < 1f ? 1f : 0f;

                // Anti-aliasing na okraji
                if (leafShape > 0.9f && leafShape < 1.1f)
                    alpha = Mathf.Clamp01(1f - (leafShape - 0.9f) * 5f);

                if (alpha < 0.01f)
                {
                    pixels[y * resolution + x] = Color.clear;
                    continue;
                }

                // === Farba listu ===
                // Stredná žilka (tmavšia)
                float midVein = 1f - Mathf.Exp(-Mathf.Abs(cx) * 40f) * 0.25f;

                // Bočné žilky (šikmé čiary od stredu)
                float sideVeins = 0f;
                for (int i = 1; i <= 5; i++)
                {
                    float veinY = 0.15f + i * 0.13f;
                    float veinDist = Mathf.Abs(v - veinY + cx * 0.8f);
                    sideVeins += Mathf.Exp(-veinDist * 60f) * 0.15f;
                }

                // Gradient: stred svetlejší, okraj tmavší
                float edgeDarkening = 1f - leafShape * 0.3f;

                // Variácia v odtieni
                float noise = Mathf.PerlinNoise(u * 8f + offsetX, v * 8f) * 0.1f;

                float brightness = edgeDarkening * midVein - sideVeins + noise;
                brightness = Mathf.Clamp(brightness, 0.5f, 1.1f);

                Color pixelColor = new Color(
                    baseColor.r * brightness,
                    baseColor.g * brightness * 1.05f, // Zelená mierne zvýraznená
                    baseColor.b * brightness * 0.9f,
                    alpha
                );

                pixels[y * resolution + x] = pixelColor;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(true);
        return tex;
    }

    /// <summary>
    /// Definuje šírku listu v závislosti od vertikálnej pozície.
    /// Vracia polovičnú šírku (0-0.5) pre danú pozíciu v (0-1).
    /// </summary>
    private static float LeafWidth(float v)
    {
        // Stopka (v < 0.1): veľmi úzky
        if (v < 0.1f) return v * 1.5f;

        // Hlavná plocha (0.1 - 0.75): eliptický tvar, najširší okolo v=0.45
        if (v < 0.75f)
        {
            float t = (v - 0.1f) / 0.65f; // Normalizácia do 0-1
            return 0.4f * Mathf.Sin(t * Mathf.PI);
        }

        // Špička (0.75 - 1.0): zúženie na bod
        float tip = (v - 0.75f) / 0.25f;
        float widthAtStart = 0.4f * Mathf.Sin(0.75f / 0.65f * Mathf.PI);
        return widthAtStart * (1f - tip * tip);
    }

    /// <summary>
    /// Vytvorí materiál pre listy s alpha cutout.
    /// Automaticky detekuje URP vs Built-in pipeline.
    /// </summary>
    public static Material CreateLeafMaterial(Color baseColor, int seed = 42, int resolution = 128)
    {
        Texture2D leafTex = GenerateLeafTexture(baseColor, resolution, seed);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        bool isURP = shader != null;
        if (!isURP) shader = Shader.Find("Standard");

        Material mat = new Material(shader) { name = "ProceduralLeafMaterial" };

        if (isURP)
        {
            mat.SetTexture("_BaseMap", leafTex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.2f);

            // Alpha clipping pre cutout transparentnosť
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetFloat("_Surface", 0f); // Opaque s alpha test
            mat.renderQueue = 2450; // AlphaTest queue

            // Obojstranné renderovanie
            mat.SetFloat("_Cull", 0f); // Off
        }
        else
        {
            mat.SetTexture("_MainTex", leafTex);
            mat.color = Color.white;
            mat.SetFloat("_Glossiness", 0.2f);

            // Alpha cutout mode pre Standard shader
            mat.SetFloat("_Mode", 1f); // Cutout
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = 2450;

            // Obojstranné renderovanie
            mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        }

        return mat;
    }
}
