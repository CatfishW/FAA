#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace VoiceControl.Editor
{
    /// <summary>
    /// Generates high-quality radial wheel segment textures with anti-aliased edges.
    /// </summary>
    public static class RadialWheelTextureGenerator
    {
        private const string TextureFolder = "Assets/Resources/VoiceControl/Textures";

        [MenuItem("Tools/Aviation/Voice Control/Generate Wheel Textures", priority = 105)]
        public static void GenerateAllTextures()
        {
            GenerateSegmentTexture(1024);
            GenerateGlowTexture(512);
            GenerateCenterTexture(512);
            GenerateIconBadgeTexture(128);
            GenerateSegmentFillTexture(1024);
            AssetDatabase.Refresh();
            Debug.Log("[RadialWheelTextureGenerator] All wheel textures generated.");
        }

        /// <summary>
        /// Generates a smooth radial segment texture for the wheel segments.
        /// Uses a 45-degree wedge shape with soft edges.
        /// </summary>
        public static void GenerateSegmentTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float outerRadius = size / 2f - 8;
            float innerRadius = outerRadius * 0.58f;
            float segmentHalfAngle = 22.5f; // 45 / 2 degrees for 8 segments

            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y) - center;
                    float distance = pos.magnitude;
                    float angle = Mathf.Atan2(pos.y, pos.x) * Mathf.Rad2Deg;

                    // Normalize angle to -180 to 180 range
                    while (angle > 180f) angle -= 360f;
                    while (angle < -180f) angle += 360f;

                    // Distance from segment center (0 degrees)
                    float angleDist = Mathf.Abs(angle);

                    // Calculate alpha based on edges
                    float alpha = 1f;

                    // Radial edge anti-aliasing
                    if (distance < innerRadius)
                    {
                        float edgeDist = innerRadius - distance;
                        alpha *= Mathf.Clamp01(1f - edgeDist / 4f);
                    }
                    else if (distance > outerRadius)
                    {
                        float edgeDist = distance - outerRadius;
                        alpha *= Mathf.Clamp01(1f - edgeDist / 4f);
                    }

                    // Angular edge anti-aliasing
                    if (angleDist > segmentHalfAngle)
                    {
                        float edgeDist = angleDist - segmentHalfAngle;
                        alpha *= Mathf.Clamp01(1f - edgeDist / 2f);
                    }

                    // Create subtle gradient
                    float gradient = 0.9f + 0.1f * ((distance - innerRadius) / (outerRadius - innerRadius));

                    pixels[y * size + x] = new Color(gradient, gradient, gradient, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            SaveTexture(tex, "WheelSegment.png");
        }

        /// <summary>
        /// Generates a filled segment texture for selected/hover states.
        /// </summary>
        public static void GenerateSegmentFillTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float outerRadius = size / 2f - 8;
            float innerRadius = outerRadius * 0.58f;
            float segmentHalfAngle = 22.5f;

            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y) - center;
                    float distance = pos.magnitude;
                    float angle = Mathf.Atan2(pos.y, pos.x) * Mathf.Rad2Deg;

                    while (angle > 180f) angle -= 360f;
                    while (angle < -180f) angle += 360f;

                    float angleDist = Mathf.Abs(angle);
                    float alpha = 1f;

                    // Radial edge anti-aliasing
                    if (distance < innerRadius)
                    {
                        float edgeDist = innerRadius - distance;
                        alpha *= Mathf.Clamp01(1f - edgeDist / 4f);
                    }
                    else if (distance > outerRadius)
                    {
                        float edgeDist = distance - outerRadius;
                        alpha *= Mathf.Clamp01(1f - edgeDist / 4f);
                    }

                    // Angular edge anti-aliasing
                    if (angleDist > segmentHalfAngle)
                    {
                        float edgeDist = angleDist - segmentHalfAngle;
                        alpha *= Mathf.Clamp01(1f - edgeDist / 2f);
                    }

                    // Inner glow effect
                    float glow = 1f;
                    if (distance > innerRadius && distance < innerRadius + 20)
                    {
                        glow = 0.85f + 0.15f * (1f - (distance - innerRadius) / 20f);
                    }
                    if (distance < outerRadius && distance > outerRadius - 10)
                    {
                        glow *= 0.9f + 0.1f * (outerRadius - distance) / 10f;
                    }

                    pixels[y * size + x] = new Color(glow, glow, glow, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            SaveTexture(tex, "WheelSegmentFill.png");
        }

        /// <summary>
        /// Generates a soft glow texture for behind the wheel.
        /// </summary>
        public static void GenerateGlowTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float maxRadius = size / 2f;

            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y) - center;
                    float distance = pos.magnitude;

                    // Soft radial gradient with power curve for nicer falloff
                    float normalizedDist = distance / maxRadius;
                    float alpha = Mathf.Pow(1f - Mathf.Clamp01(normalizedDist), 1.8f) * 0.5f;

                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            SaveTexture(tex, "WheelGlow.png");
        }

        /// <summary>
        /// Generates a center panel texture with subtle border.
        /// </summary>
        public static void GenerateCenterTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 4;

            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y) - center;
                    float distance = pos.magnitude;

                    float edgeDist = radius - distance;
                    float alpha = Mathf.Clamp01(edgeDist / 3f);

                    // Subtle vignette effect
                    float vignette = 1f - Mathf.Clamp01(distance / radius) * 0.08f;

                    // Subtle inner highlight ring
                    if (distance > radius - 15 && distance < radius)
                    {
                        float ringT = (radius - distance) / 15f;
                        vignette *= 0.9f + 0.15f * ringT;
                    }

                    pixels[y * size + x] = new Color(vignette, vignette, vignette, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            SaveTexture(tex, "WheelCenter.png");
        }

        /// <summary>
        /// Generates a small badge texture for command icons.
        /// </summary>
        public static void GenerateIconBadgeTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 2;

            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y) - center;
                    float distance = pos.magnitude;

                    float edgeDist = radius - distance;
                    float alpha = Mathf.Clamp01(edgeDist / 2f);

                    // Subtle highlight at top-left
                    float highlight = 1f;
                    if (distance < radius * 0.7f)
                    {
                        Vector2 highlightDir = new Vector2(-0.7f, 0.7f).normalized;
                        float highlightFactor = Vector2.Dot(pos.normalized, highlightDir);
                        if (highlightFactor > 0.3f)
                        {
                            highlight = 1f + (highlightFactor - 0.3f) * 0.3f;
                        }
                    }

                    pixels[y * size + x] = new Color(highlight, highlight, highlight, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            SaveTexture(tex, "IconBadge.png");
        }

        private static void SaveTexture(Texture2D tex, string filename)
        {
            if (!Directory.Exists(TextureFolder))
            {
                Directory.CreateDirectory(TextureFolder);
            }

            string path = Path.Combine(TextureFolder, filename);
            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(path, bytes);

            AssetDatabase.ImportAsset(path);

            // Configure import settings for UI
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePivot = new Vector2(0.5f, 0.5f);
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            Object.DestroyImmediate(tex);
        }
    }
}
#endif
