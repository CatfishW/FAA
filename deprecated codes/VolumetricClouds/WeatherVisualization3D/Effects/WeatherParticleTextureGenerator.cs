using UnityEngine;

namespace WeatherVisualization3D
{
    /// <summary>
    /// Generates procedural textures for weather particle effects.
    /// Creates rain streaks and snowflake textures at runtime.
    /// </summary>
    public static class WeatherParticleTextureGenerator
    {
        private static Texture2D _rainTexture;
        private static Texture2D _snowTexture;
        private static Texture2D _softGlowTexture;
        
        /// <summary>
        /// Get or create the rain particle texture
        /// </summary>
        public static Texture2D GetRainTexture()
        {
            if (_rainTexture == null)
            {
                _rainTexture = CreateRainTexture(64, 256);
            }
            return _rainTexture;
        }
        
        /// <summary>
        /// Get or create the snow particle texture
        /// </summary>
        public static Texture2D GetSnowTexture()
        {
            if (_snowTexture == null)
            {
                _snowTexture = CreateSnowTexture(64, 64);
            }
            return _snowTexture;
        }
        
        /// <summary>
        /// Get or create a soft glow texture for particles
        /// </summary>
        public static Texture2D GetSoftGlowTexture()
        {
            if (_softGlowTexture == null)
            {
                _softGlowTexture = CreateSoftGlowTexture(64, 64);
            }
            return _softGlowTexture;
        }
        
        /// <summary>
        /// Create a rain streak texture - elongated drop shape with motion blur
        /// </summary>
        private static Texture2D CreateRainTexture(int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "RainParticleTexture";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            
            Color[] pixels = new Color[width * height];
            Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Create elongated drop shape
                    float dx = (x - center.x) / (width * 0.3f);
                    float dy = (y - center.y) / (height * 0.45f);
                    
                    // Elliptical distance with tapering at bottom
                    float normalizedY = (float)y / height;
                    float widthFactor = Mathf.Lerp(0.3f, 1.0f, normalizedY); // Taper at top
                    
                    float dist = Mathf.Sqrt(dx * dx / (widthFactor * widthFactor) + dy * dy);
                    
                    // Create rain drop shape with gradient
                    float alpha = 0f;
                    if (dist < 1.0f)
                    {
                        // Core of the rain drop - bright center
                        alpha = Mathf.SmoothStep(1.0f, 0.0f, dist);
                        
                        // Add highlight in the center
                        float highlight = Mathf.Exp(-dist * dist * 4f) * 0.5f;
                        alpha = Mathf.Max(alpha, highlight);
                    }
                    
                    // Fade edges
                    alpha *= Mathf.SmoothStep(0f, 0.1f, normalizedY); // Fade at top
                    alpha *= Mathf.SmoothStep(1f, 0.9f, normalizedY); // Fade at bottom
                    
                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            
            return texture;
        }
        
        /// <summary>
        /// Create a snowflake texture - hexagonal crystal with soft edges
        /// </summary>
        private static Texture2D CreateSnowTexture(int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "SnowParticleTexture";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            
            Color[] pixels = new Color[width * height];
            Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
            float maxRadius = Mathf.Min(width, height) * 0.45f;
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2 pos = new Vector2(x - center.x, y - center.y);
                    float dist = pos.magnitude;
                    float angle = Mathf.Atan2(pos.y, pos.x) * Mathf.Rad2Deg;
                    
                    float alpha = 0f;
                    
                    if (dist < maxRadius)
                    {
                        // Hexagonal shape modulation
                        float hexMod = Mathf.Cos(angle * 6f * Mathf.Deg2Rad) * 0.1f + 0.9f;
                        float hexRadius = maxRadius * hexMod;
                        
                        // Base circle with hexagonal influence
                        if (dist < hexRadius)
                        {
                            alpha = Mathf.SmoothStep(1.0f, 0.0f, dist / hexRadius);
                        }
                        
                        // Add crystalline structure - radial lines
                        float lineMod = Mathf.Abs(Mathf.Sin(angle * 6f * Mathf.Deg2Rad));
                        if (lineMod < 0.15f && dist < maxRadius * 0.8f)
                        {
                            alpha = Mathf.Max(alpha, Mathf.SmoothStep(0.15f, 0f, lineMod) * 0.7f);
                        }
                        
                        // Add sparkle in center
                        if (dist < maxRadius * 0.2f)
                        {
                            alpha = Mathf.Max(alpha, 0.9f);
                        }
                    }
                    
                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            
            return texture;
        }
        
        /// <summary>
        /// Create a soft radial glow texture
        /// </summary>
        private static Texture2D CreateSoftGlowTexture(int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "SoftGlowTexture";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            
            Color[] pixels = new Color[width * height];
            Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
            float maxDist = Mathf.Min(width, height) * 0.5f;
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.SmoothStep(1.0f, 0.0f, dist / maxDist);
                    alpha = alpha * alpha; // Square for softer falloff
                    
                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            
            return texture;
        }
        
        /// <summary>
        /// Clean up generated textures
        /// </summary>
        public static void Cleanup()
        {
            if (_rainTexture != null)
            {
                Object.Destroy(_rainTexture);
                _rainTexture = null;
            }
            if (_snowTexture != null)
            {
                Object.Destroy(_snowTexture);
                _snowTexture = null;
            }
            if (_softGlowTexture != null)
            {
                Object.Destroy(_softGlowTexture);
                _softGlowTexture = null;
            }
        }
    }
}
