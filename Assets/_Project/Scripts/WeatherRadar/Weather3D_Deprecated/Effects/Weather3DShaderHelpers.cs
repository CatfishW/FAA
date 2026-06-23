using UnityEngine;

namespace WeatherRadar.Weather3D
{
    /// <summary>
    /// Helper class for creating improved weather visualization materials.
    /// Provides utilities for gradient generation, emission effects, and soft edges.
    /// </summary>
    public static class Weather3DShaderHelpers
    {
        #region Material Creation
        
        /// <summary>
        /// Create a cloud material with gradient and glow effects
        /// </summary>
        public static Material CreateCloudMaterial(Color baseColor, float intensity, bool enableEmission = true)
        {
            // Use Standard shader with transparency for best compatibility
            Material mat = new Material(Shader.Find("Standard"));
            
            // Set rendering mode to Fade for soft transparency
            SetMaterialToFade(mat);
            
            // Calculate gradient colors based on intensity
            Color coreColor = GetIntensityGradientColor(intensity, true);
            Color edgeColor = GetIntensityGradientColor(intensity, false);
            
            // Apply base color with alpha
            mat.color = coreColor;
            
            // Enable emission for glow effect
            if (enableEmission)
            {
                mat.EnableKeyword("_EMISSION");
                Color emissionColor = CalculateEmissionColor(baseColor, intensity);
                mat.SetColor("_EmissionColor", emissionColor);
            }
            
            // Set smoothness and metallic for cloud appearance
            mat.SetFloat("_Glossiness", 0.1f);
            mat.SetFloat("_Metallic", 0f);
            
            return mat;
        }
        
        /// <summary>
        /// Create a turbulence zone material with animated properties
        /// </summary>
        public static Material CreateTurbulenceMaterial(float severity, Color tint)
        {
            Material mat = new Material(Shader.Find("Standard"));
            SetMaterialToFade(mat);
            
            // Yellow to red based on severity
            Color turbulenceColor = GetTurbulenceColor(severity);
            turbulenceColor = Color.Lerp(turbulenceColor, tint, 0.3f);
            
            mat.color = turbulenceColor;
            
            // Add emission for visibility
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", turbulenceColor * 0.5f * Mathf.Lerp(0.3f, 1f, severity));
            
            mat.SetFloat("_Glossiness", 0f);
            mat.SetFloat("_Metallic", 0f);
            
            return mat;
        }
        
        /// <summary>
        /// Create a hazard pillar material with striped pattern effect
        /// </summary>
        public static Material CreateHazardPillarMaterial(float intensity)
        {
            Material mat = new Material(Shader.Find("Standard"));
            SetMaterialToFade(mat);
            
            // Red/magenta for severe hazards
            Color hazardColor = intensity > 0.7f 
                ? new Color(1f, 0f, 0.5f, 0.7f)  // Magenta
                : new Color(1f, 0.2f, 0f, 0.6f); // Red-orange
            
            mat.color = hazardColor;
            
            // Strong emission for visibility
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", hazardColor * 1.5f);
            
            return mat;
        }
        
        /// <summary>
        /// Create a lightning bolt material with bright HDR emission
        /// </summary>
        public static Material CreateLightningMaterial()
        {
            Material mat = new Material(Shader.Find("Standard"));
            SetMaterialToFade(mat);
            
            // Bright white-blue
            Color lightningColor = new Color(0.9f, 0.95f, 1f, 1f);
            mat.color = lightningColor;
            
            // HDR emission for bloom effect
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", lightningColor * 5f);
            
            mat.SetFloat("_Glossiness", 1f);
            
            return mat;
        }
        
        #endregion
        
        #region Color Utilities
        
        /// <summary>
        /// Get aviation-standard intensity color with gradient support
        /// </summary>
        public static Color GetIntensityGradientColor(float intensity, bool isCore)
        {
            Color baseColor;
            float alpha;
            
            if (intensity < 0.2f)
            {
                // Green - Light
                baseColor = new Color(0.2f, 0.9f, 0.3f);
                alpha = isCore ? 0.5f : 0.2f;
            }
            else if (intensity < 0.4f)
            {
                // Yellow - Moderate
                baseColor = Color.Lerp(new Color(0.2f, 0.9f, 0.3f), new Color(1f, 0.95f, 0.2f), (intensity - 0.2f) / 0.2f);
                alpha = isCore ? 0.6f : 0.3f;
            }
            else if (intensity < 0.6f)
            {
                // Orange - Heavy
                baseColor = Color.Lerp(new Color(1f, 0.95f, 0.2f), new Color(1f, 0.5f, 0f), (intensity - 0.4f) / 0.2f);
                alpha = isCore ? 0.7f : 0.4f;
            }
            else if (intensity < 0.8f)
            {
                // Red - Intense
                baseColor = Color.Lerp(new Color(1f, 0.5f, 0f), new Color(1f, 0.1f, 0.1f), (intensity - 0.6f) / 0.2f);
                alpha = isCore ? 0.8f : 0.5f;
            }
            else
            {
                // Magenta - Extreme
                baseColor = Color.Lerp(new Color(1f, 0.1f, 0.1f), new Color(1f, 0f, 0.8f), (intensity - 0.8f) / 0.2f);
                alpha = isCore ? 0.9f : 0.6f;
            }
            
            baseColor.a = alpha;
            return baseColor;
        }
        
        /// <summary>
        /// Get turbulence severity color (yellow to red gradient)
        /// </summary>
        public static Color GetTurbulenceColor(float severity)
        {
            if (severity < 0.33f)
            {
                // Light turbulence - yellow
                return new Color(1f, 0.9f, 0.3f, 0.3f + severity * 0.5f);
            }
            else if (severity < 0.66f)
            {
                // Moderate turbulence - orange
                float t = (severity - 0.33f) / 0.33f;
                return Color.Lerp(
                    new Color(1f, 0.9f, 0.3f, 0.5f),
                    new Color(1f, 0.5f, 0.1f, 0.6f),
                    t
                );
            }
            else
            {
                // Severe turbulence - red
                float t = (severity - 0.66f) / 0.34f;
                return Color.Lerp(
                    new Color(1f, 0.5f, 0.1f, 0.6f),
                    new Color(1f, 0.2f, 0.1f, 0.75f),
                    t
                );
            }
        }
        
        /// <summary>
        /// Calculate emission color for glow effect
        /// </summary>
        public static Color CalculateEmissionColor(Color baseColor, float intensity)
        {
            // Emission increases with intensity for better visibility
            float emissionStrength = Mathf.Lerp(0.1f, 0.8f, intensity);
            Color emission = baseColor * emissionStrength;
            emission.a = 1f; // Emission doesn't use alpha
            return emission;
        }
        
        #endregion
        
        #region Material Setup
        
        /// <summary>
        /// Set material to Fade rendering mode for soft transparency
        /// </summary>
        public static void SetMaterialToFade(Material mat)
        {
            mat.SetFloat("_Mode", 2); // Fade mode
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }
        
        /// <summary>
        /// Set material to Transparent rendering mode
        /// </summary>
        public static void SetMaterialToTransparent(Material mat)
        {
            mat.SetFloat("_Mode", 3); // Transparent mode
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }
        
        #endregion
        
        #region Animation Utilities
        
        /// <summary>
        /// Calculate pulsing alpha based on time for animated effects
        /// </summary>
        public static float GetPulseAlpha(float baseAlpha, float pulseAmount, float frequency, float timeOffset = 0f)
        {
            float pulse = Mathf.Sin((Time.time + timeOffset) * frequency * Mathf.PI * 2f) * 0.5f + 0.5f;
            return baseAlpha + pulse * pulseAmount;
        }
        
        /// <summary>
        /// Calculate breathing scale for cloud morphing
        /// </summary>
        public static Vector3 GetBreathingScale(Vector3 baseScale, float amount, float speed, float timeOffset = 0f)
        {
            float t = Time.time * speed + timeOffset;
            float breathX = Mathf.Sin(t * 0.7f) * amount;
            float breathY = Mathf.Sin(t * 0.5f) * amount * 1.5f;
            float breathZ = Mathf.Cos(t * 0.6f) * amount;
            
            return new Vector3(
                baseScale.x * (1f + breathX),
                baseScale.y * (1f + breathY),
                baseScale.z * (1f + breathZ)
            );
        }
        
        /// <summary>
        /// Get rim light intensity based on view angle
        /// </summary>
        public static float GetRimLightIntensity(Vector3 surfaceNormal, Vector3 viewDirection)
        {
            float rim = 1f - Mathf.Max(0, Vector3.Dot(surfaceNormal, viewDirection));
            return Mathf.Pow(rim, 2f);
        }
        
        #endregion
        
        #region Mesh Utilities
        
        /// <summary>
        /// Create a soft-edged sphere mesh with UV for gradient mapping
        /// </summary>
        public static Mesh CreateSoftSphereMesh(int segments = 16)
        {
            Mesh mesh = new Mesh();
            mesh.name = "SoftSphere";
            
            int rings = segments;
            int slices = segments * 2;
            
            int vertCount = (rings + 1) * (slices + 1);
            Vector3[] vertices = new Vector3[vertCount];
            Vector3[] normals = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            Color[] colors = new Color[vertCount];
            
            int index = 0;
            for (int ring = 0; ring <= rings; ring++)
            {
                float v = ring / (float)rings;
                float phi = v * Mathf.PI;
                
                for (int slice = 0; slice <= slices; slice++)
                {
                    float u = slice / (float)slices;
                    float theta = u * Mathf.PI * 2f;
                    
                    float x = Mathf.Sin(phi) * Mathf.Cos(theta);
                    float y = Mathf.Cos(phi);
                    float z = Mathf.Sin(phi) * Mathf.Sin(theta);
                    
                    vertices[index] = new Vector3(x, y, z) * 0.5f;
                    normals[index] = new Vector3(x, y, z);
                    uvs[index] = new Vector2(u, v);
                    
                    // Color alpha fades at edges for soft look
                    float edgeFactor = Mathf.Abs(y);
                    colors[index] = new Color(1, 1, 1, 0.5f + edgeFactor * 0.5f);
                    
                    index++;
                }
            }
            
            // Create triangles
            int[] triangles = new int[rings * slices * 6];
            int triIndex = 0;
            
            for (int ring = 0; ring < rings; ring++)
            {
                for (int slice = 0; slice < slices; slice++)
                {
                    int current = ring * (slices + 1) + slice;
                    int next = current + slices + 1;
                    
                    triangles[triIndex++] = current;
                    triangles[triIndex++] = next;
                    triangles[triIndex++] = current + 1;
                    
                    triangles[triIndex++] = current + 1;
                    triangles[triIndex++] = next;
                    triangles[triIndex++] = next + 1;
                }
            }
            
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            
            return mesh;
        }
        
        /// <summary>
        /// Create an ellipsoid mesh with configurable dimensions
        /// </summary>
        public static Mesh CreateEllipsoidMesh(Vector3 radii, int segments = 16)
        {
            Mesh mesh = CreateSoftSphereMesh(segments);
            
            // Scale vertices by radii
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = Vector3.Scale(vertices[i] * 2f, radii);
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            
            return mesh;
        }
        
        #endregion
    }
}
