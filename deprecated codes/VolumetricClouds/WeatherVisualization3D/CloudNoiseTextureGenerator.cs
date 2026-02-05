using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WeatherVisualization3D
{
    /// <summary>
    /// Generates procedural 3D noise textures for volumetric clouds at runtime
    /// Based on UnityVolumetricCloudsURP noise patterns
    /// </summary>
    [ExecuteInEditMode]
    public class CloudNoiseTextureGenerator : MonoBehaviour
    {
        [Header("Texture Resolution")]
        [Range(16, 128)]
        public int textureSize = 64;

        [Header("Noise Parameters")]
        [Range(1, 8)]
        public int worleyCells = 4;
        [Range(1, 4)]
        public int octaves = 3;
        [Range(0f, 1f)]
        public float persistence = 0.5f;

        [Header("Generated Textures")]
        public Texture3D worleyNoise128;
        public Texture3D erosionNoise32;
        public Texture3D perlinNoise32;

        [Header("Auto Generate")]
        public bool autoGenerateOnStart = true;
        public bool saveToDisk = false;

        private void OnEnable()
        {
            if (autoGenerateOnStart && worleyNoise128 == null)
            {
                GenerateAllTextures();
            }
        }

        [ContextMenu("Generate All Textures")]
        public void GenerateAllTextures()
        {
            Debug.Log("[CloudNoiseTextureGenerator] Generating 3D noise textures...");

            worleyNoise128 = GenerateWorleyNoise(textureSize, worleyCells, octaves, persistence);
            erosionNoise32 = GenerateWorleyNoise(32, 8, 2, 0.5f);
            perlinNoise32 = GeneratePerlinNoise(32, octaves, persistence);

            Debug.Log("[CloudNoiseTextureGenerator] Textures generated successfully!");

#if UNITY_EDITOR
            if (saveToDisk)
            {
                SaveTexturesToDisk();
            }
#endif
        }

        /// <summary>
        /// Generates 3D Worley noise (cellular noise) for cloud shapes
        /// </summary>
        private Texture3D GenerateWorleyNoise(int size, int cells, int octaves, float persistence)
        {
            Texture3D tex = new Texture3D(size, size, size, TextureFormat.RGBA32, false);
            tex.name = $"WorleyNoise_{size}";
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;

            Color[] pixels = new Color[size * size * size];

            // Generate random cell points for each octave
            for (int z = 0; z < size; z++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float noiseR = 0, noiseG = 0, noiseB = 0, noiseA = 0;
                        float amplitude = 1f;
                        float frequency = 1f;

                        for (int o = 0; o < octaves; o++)
                        {
                            float n = SampleWorley(x * frequency, y * frequency, z * frequency, size, cells * (o + 1));
                            noiseR += n * amplitude;

                            // Different channel variations
                            n = SampleWorley(x * frequency + 100, y * frequency, z * frequency, size, cells * (o + 1));
                            noiseG += n * amplitude;

                            n = SampleWorley(x * frequency + 200, y * frequency, z * frequency, size, cells * (o + 1));
                            noiseB += n * amplitude;

                            n = SampleWorley(x * frequency + 300, y * frequency, z * frequency, size, cells * (o + 1));
                            noiseA += n * amplitude;

                            amplitude *= persistence;
                            frequency *= 2f;
                        }

                        int idx = x + y * size + z * size * size;
                        pixels[idx] = new Color(noiseR, noiseG, noiseB, noiseA);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private float SampleWorley(float x, float y, float z, int size, int cells)
        {
            float cellSize = (float)size / cells;

            // Find which cell this point is in
            int cellX = Mathf.FloorToInt(x / cellSize);
            int cellY = Mathf.FloorToInt(y / cellSize);
            int cellZ = Mathf.FloorToInt(z / cellSize);

            float minDist = float.MaxValue;

            // Check neighboring cells
            for (int oz = -1; oz <= 1; oz++)
            {
                for (int oy = -1; oy <= 1; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        // Get random point in neighboring cell
                        int nx = cellX + ox;
                        int ny = cellY + oy;
                        int nz = cellZ + oz;

                        // Generate pseudo-random point in cell
                        float seed = nx * 374761 + ny * 668265 + nz * 132897;
                        float px = (nx + Frac(Mathf.Sin(seed) * 43758.5453f)) * cellSize;
                        float py = (ny + Frac(Mathf.Sin(seed + 1) * 43758.5453f)) * cellSize;
                        float pz = (nz + Frac(Mathf.Sin(seed + 2) * 43758.5453f)) * cellSize;

                        float dist = Mathf.Sqrt(
                            (x - px) * (x - px) +
                            (y - py) * (y - py) +
                            (z - pz) * (z - pz)
                        );

                        minDist = Mathf.Min(minDist, dist);
                    }
                }
            }

            // Normalize to 0-1 range
            float maxDist = cellSize * Mathf.Sqrt(3f);
            return minDist / maxDist;
        }

        private float Frac(float f)
        {
            return f - Mathf.Floor(f);
        }

        /// <summary>
        /// Generates 3D Perlin noise for cloud base shapes
        /// </summary>
        private Texture3D GeneratePerlinNoise(int size, int octaves, float persistence)
        {
            Texture3D tex = new Texture3D(size, size, size, TextureFormat.RGB24, false);
            tex.name = $"PerlinNoise_{size}";
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;

            Color[] pixels = new Color[size * size * size];

            for (int z = 0; z < size; z++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float noiseR = 0, noiseG = 0, noiseB = 0;
                        float amplitude = 1f;
                        float frequency = 1f;

                        for (int o = 0; o < octaves; o++)
                        {
                            noiseR += SamplePerlin(x * frequency / size, y * frequency / size, z * frequency / size) * amplitude;
                            noiseG += SamplePerlin(x * frequency / size + 10, y * frequency / size, z * frequency / size) * amplitude;
                            noiseB += SamplePerlin(x * frequency / size + 20, y * frequency / size, z * frequency / size) * amplitude;

                            amplitude *= persistence;
                            frequency *= 2f;
                        }

                        // Normalize
                        float maxVal = (1f - Mathf.Pow(persistence, octaves)) / (1f - persistence);
                        noiseR = (noiseR / maxVal + 1f) * 0.5f;
                        noiseG = (noiseG / maxVal + 1f) * 0.5f;
                        noiseB = (noiseB / maxVal + 1f) * 0.5f;

                        int idx = x + y * size + z * size * size;
                        pixels[idx] = new Color(noiseR, noiseG, noiseB);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private float SamplePerlin(float x, float y, float z)
        {
            int X = Mathf.FloorToInt(x) & 255;
            int Y = Mathf.FloorToInt(y) & 255;
            int Z = Mathf.FloorToInt(z) & 255;

            x -= Mathf.Floor(x);
            y -= Mathf.Floor(y);
            z -= Mathf.Floor(z);

            float u = Fade(x);
            float v = Fade(y);
            float w = Fade(z);

            int A = Perm(X) + Y;
            int AA = Perm(A) + Z;
            int AB = Perm(A + 1) + Z;
            int B = Perm(X + 1) + Y;
            int BA = Perm(B) + Z;
            int BB = Perm(B + 1) + Z;

            return Lerp(w, Lerp(v, Lerp(u, Grad(Perm(AA), x, y, z),
                                           Grad(Perm(BA), x - 1, y, z)),
                                   Lerp(u, Grad(Perm(AB), x, y - 1, z),
                                           Grad(Perm(BB), x - 1, y - 1, z))),
                           Lerp(v, Lerp(u, Grad(Perm(AA + 1), x, y, z - 1),
                                           Grad(Perm(BA + 1), x - 1, y, z - 1)),
                                   Lerp(u, Grad(Perm(AB + 1), x, y - 1, z - 1),
                                           Grad(Perm(BB + 1), x - 1, y - 1, z - 1))));
        }

        private float Fade(float t)
        {
            return t * t * t * (t * (t * 6 - 15) + 10);
        }

        private float Lerp(float t, float a, float b)
        {
            return a + t * (b - a);
        }

        private float Grad(int hash, float x, float y, float z)
        {
            int h = hash & 15;
            float u = h < 8 ? x : y;
            float v = h < 4 ? y : h == 12 || h == 14 ? x : z;
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        private int Perm(int i)
        {
            int[] perm = new int[] {
                151,160,137,91,90,15,131,13,201,95,96,53,194,233,7,225,
                140,36,103,30,69,142,8,99,37,240,21,10,23,190,6,148,
                247,120,234,75,0,26,197,62,94,252,219,203,117,35,11,32,
                57,177,33,88,237,149,56,87,174,20,125,136,171,168,68,175,
                74,165,71,134,139,48,27,166,77,146,158,231,83,111,229,122,
                60,211,133,230,220,105,92,41,55,46,245,40,244,102,143,54,
                65,25,63,161,1,216,80,73,209,76,132,187,208,89,18,169,
                200,196,135,130,116,188,159,86,164,100,109,198,173,186,3,64,
                52,217,226,250,124,123,5,202,38,147,118,126,255,82,85,212,
                207,206,59,227,47,16,58,17,182,189,28,42,223,183,170,213,
                119,248,152,2,44,154,163,70,221,153,101,155,167,43,172,9,
                129,22,39,253,19,98,108,110,79,113,224,232,178,185,112,104,
                218,246,97,228,251,34,242,193,238,210,144,12,191,179,162,241,
                81,51,145,235,249,14,239,107,49,192,214,31,181,199,106,157,
                184,84,204,176,115,121,50,45,127,4,150,254,138,236,205,93,
                222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180
            };
            return perm[i & 255];
        }

#if UNITY_EDITOR
        private void SaveTexturesToDisk()
        {
            string path = "Assets/_Project/Textures/CloudNoise/Generated/";
            System.IO.Directory.CreateDirectory(path);

            if (worleyNoise128 != null)
            {
                AssetDatabase.CreateAsset(worleyNoise128, path + "WorleyNoise128_Generated.asset");
            }
            if (erosionNoise32 != null)
            {
                AssetDatabase.CreateAsset(erosionNoise32, path + "ErosionNoise32_Generated.asset");
            }
            if (perlinNoise32 != null)
            {
                AssetDatabase.CreateAsset(perlinNoise32, path + "PerlinNoise32_Generated.asset");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[CloudNoiseTextureGenerator] Textures saved to disk!");
        }
#endif

        [ContextMenu("Apply to Material")]
        public void ApplyToMaterial()
        {
            Renderer rend = GetComponent<Renderer>();
            if (rend == null)
            {
                Debug.LogError("[CloudNoiseTextureGenerator] No Renderer found on this GameObject!");
                return;
            }

            Material mat = rend.sharedMaterial;
            if (mat == null)
            {
                Debug.LogError("[CloudNoiseTextureGenerator] No material assigned to Renderer!");
                return;
            }

            if (worleyNoise128 != null)
                mat.SetTexture("_WorleyNoise", worleyNoise128);
            if (erosionNoise32 != null)
                mat.SetTexture("_ErosionNoise", erosionNoise32);
            if (perlinNoise32 != null)
                mat.SetTexture("_PerlinNoise", perlinNoise32);

            Debug.Log("[CloudNoiseTextureGenerator] Textures applied to material!");
        }
    }
}
