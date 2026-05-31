using UnityEngine;

public static class LeafTextureGenerator
{

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
                float u = (float)x / resolution;
                float v = (float)y / resolution;

                float cx = u - 0.5f;

                float alpha = 0f;

                if (v < 0.08f)
                {
                    float petioleHalfWidth = 0.025f;
                    if (Mathf.Abs(cx) < petioleHalfWidth)
                        alpha = 1f;
                }
                else
                {
                    float halfWidth = LeafHalfWidth(v);
                    float dx = Mathf.Abs(cx) / Mathf.Max(halfWidth, 0.001f);

                    float edgeNoise = Mathf.Sin(v * 25f + offsetX) * 0.04f
                                    + Mathf.Sin(v * 13f + offsetX * 2f) * 0.025f;
                    float edgeDist = dx + edgeNoise;

                    if (edgeDist < 1f) alpha = 1f;
                    else if (edgeDist < 1.1f) alpha = Mathf.Clamp01(1f - (edgeDist - 1f) * 10f);
                }

                if (alpha < 0.01f)
                {
                    pixels[y * resolution + x] = Color.clear;
                    continue;
                }

                float midVein = 1f - Mathf.Exp(-Mathf.Abs(cx) * 40f) * 0.25f;

                float sideVeins = 0f;
                for (int i = 1; i <= 5; i++)
                {
                    float veinY = 0.15f + i * 0.13f;
                    float veinDist = Mathf.Abs(v - veinY + cx * 0.8f);
                    sideVeins += Mathf.Exp(-veinDist * 60f) * 0.15f;
                }

                float halfWidthAtV = LeafHalfWidth(v);
                float normalizedDist = halfWidthAtV > 0.001f
                    ? Mathf.Clamp01(Mathf.Abs(cx) / halfWidthAtV)
                    : 0f;
                float edgeDarkening = 1f - normalizedDist * 0.3f;

                float noise = Mathf.PerlinNoise(u * 8f + offsetX, v * 8f) * 0.1f;

                float brightness = edgeDarkening * midVein - sideVeins + noise;
                brightness = Mathf.Clamp(brightness, 0.5f, 1.1f);

                Color pixelColor = new Color(
                    baseColor.r * brightness,
                    baseColor.g * brightness * 1.05f,
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

    private static float LeafHalfWidth(float v)
    {
        const float bladeStart = 0.08f;
        const float bladePeak = 0.45f;
        const float maxHalfWidth = 0.32f;

        if (v < bladeStart || v > 1f) return 0f;

        float t = (v - bladeStart) / (1f - bladeStart);

        float peakT = (bladePeak - bladeStart) / (1f - bladeStart);

        float width;
        if (t < peakT)
        {
            float localT = t / peakT;
            width = maxHalfWidth * Mathf.Sin(localT * Mathf.PI * 0.5f);
        }
        else
        {
            float localT = (t - peakT) / (1f - peakT);
            width = maxHalfWidth * Mathf.Cos(localT * Mathf.PI * 0.5f);
            width *= (1f - localT * localT * 0.3f);
        }

        return width;
    }

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

            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetFloat("_Surface", 0f);
            mat.renderQueue = 2450;

            mat.SetFloat("_Cull", 0f);
        }
        else
        {
            mat.SetTexture("_MainTex", leafTex);
            mat.color = Color.white;
            mat.SetFloat("_Glossiness", 0.2f);

            mat.SetFloat("_Mode", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = 2450;

            mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        }

        return mat;
    }
}
