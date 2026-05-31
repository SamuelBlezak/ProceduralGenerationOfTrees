using UnityEngine;

public static class BarkTextureGenerator
{
    public static Texture2D GenerateBarkTexture(
        Color baseColor, int resolution = 256, int seed = 42, float roughness = 0.6f)
    {
        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true);
        tex.name = "ProceduralBark";
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        System.Random rng = new System.Random(seed);

        float offsetX = (float)rng.NextDouble() * 1000f;
        float offsetY = (float)rng.NextDouble() * 1000f;

        Color[] pixels = new Color[resolution * resolution];

        float[] crackIntensity = new float[resolution];
        for (int y = 0; y < resolution; y++)
        {
            float crackChance = FBM(0f, y * 0.15f + offsetY, 2) * 0.5f + 0.5f;
            crackIntensity[y] = crackChance > 0.72f ? (crackChance - 0.72f) * 3f : 0f;
        }

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float u = (float)x / resolution;
                float v = (float)y / resolution;

                float largeNoise = FBM(
                    u * 4f + offsetX,
                    v * 8f + offsetY,
                    4) * roughness;

                float fineNoise = FBM(
                    u * 16f + offsetX * 2f,
                    v * 24f + offsetY * 2f,
                    3) * roughness * 0.3f;

                float stripes = Mathf.Sin(v * 40f + largeNoise * 8f) * 0.5f + 0.5f;
                stripes = Mathf.Pow(stripes, 2f) * 0.15f * roughness;

                float crack = crackIntensity[y];
                float crackWave = FBM(u * 6f + offsetX * 3f, v * 0.5f, 2);
                crack *= Mathf.Clamp01(1f - Mathf.Abs(crackWave) * 2f);

                float brightness = 1f
                    - largeNoise * 0.4f
                    - fineNoise
                    - stripes
                    - crack * 0.5f;

                brightness = Mathf.Clamp(brightness, 0.3f, 1.1f);

                float hueShift = FBM(u * 3f + offsetX, v * 5f + offsetY, 2) * 0.08f;
                Color pixelColor = new Color(
                    baseColor.r * brightness + hueShift * 0.3f,
                    baseColor.g * brightness - hueShift * 0.1f,
                    baseColor.b * brightness - hueShift * 0.2f,
                    1f
                );

                pixels[y * resolution + x] = pixelColor;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(true);
        return tex;
    }


    public static Texture2D GenerateNormalMap(Texture2D heightMap, float strength = 1.5f)
    {
        int w = heightMap.width;
        int h = heightMap.height;
        Texture2D normalMap = new Texture2D(w, h, TextureFormat.RGBA32, true);
        normalMap.name = "ProceduralBarkNormal";
        normalMap.wrapMode = TextureWrapMode.Repeat;
        normalMap.filterMode = FilterMode.Bilinear;

        Color[] heightPixels = heightMap.GetPixels();
        Color[] normalPixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float left = GetHeight(heightPixels, (x - 1 + w) % w, y, w);
                float right = GetHeight(heightPixels, (x + 1) % w, y, w);
                float down = GetHeight(heightPixels, x, (y - 1 + h) % h, w);
                float up = GetHeight(heightPixels, x, (y + 1) % h, w);

                float dx = (left - right) * strength;
                float dy = (down - up) * strength;

                Vector3 normal = new Vector3(dx, dy, 1f).normalized;

                normalPixels[y * w + x] = new Color(
                    normal.x * 0.5f + 0.5f,
                    normal.y * 0.5f + 0.5f,
                    normal.z * 0.5f + 0.5f,
                    1f
                );
            }
        }

        normalMap.SetPixels(normalPixels);
        normalMap.Apply(true);
        return normalMap;
    }


    public static Material CreateBarkMaterial(
        Color baseColor, int seed = 42, float roughness = 0.6f, int resolution = 256)
    {
        Texture2D barkTex = GenerateBarkTexture(baseColor, resolution, seed, roughness);
        Texture2D normalTex = GenerateNormalMap(barkTex, 1.5f);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        bool isURP = shader != null;
        if (!isURP) shader = Shader.Find("Standard");

        Material mat = new Material(shader) { name = "ProceduralBarkMaterial" };

        if (isURP)
        {
            mat.SetTexture("_BaseMap", barkTex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BumpMap", normalTex);
            mat.SetFloat("_BumpScale", 1.0f);
            mat.SetFloat("_Smoothness", 0.05f);
            mat.EnableKeyword("_NORMALMAP");
        }
        else
        {
            mat.SetTexture("_MainTex", barkTex);
            mat.color = Color.white;
            mat.SetTexture("_BumpMap", normalTex);
            mat.SetFloat("_BumpScale", 1.0f);
            mat.SetFloat("_Glossiness", 0.05f);
            mat.EnableKeyword("_NORMALMAP");
        }

        return mat;
    }

    private static float FBM(float x, float y, int octaves)
    {
        float value = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float maxValue = 0f;

        for (int i = 0; i < octaves; i++)
        {
            value += PerlinNoise(x * frequency, y * frequency) * amplitude;
            maxValue += amplitude;
            amplitude *= 0.5f;
            frequency *= 2.0f;
        }

        return value / maxValue;
    }


    private static float PerlinNoise(float x, float y)
    {
        return Mathf.Clamp01(Mathf.PerlinNoise(x, y)) * 2f - 1f;
    }

    private static float GetHeight(Color[] pixels, int x, int y, int width)
    {
        Color c = pixels[y * width + x];
        return (c.r + c.g + c.b) / 3f;
    }
}
