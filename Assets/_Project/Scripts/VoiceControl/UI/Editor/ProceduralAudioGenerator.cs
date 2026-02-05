using UnityEngine;
using UnityEditor;
using System.IO;

namespace VoiceControl.UI.Editor
{
    /// <summary>
    /// Generates procedural audio clips for UI feedback sounds.
    /// Creates clean, professional sounds without external dependencies.
    /// </summary>
    public class ProceduralAudioGenerator : EditorWindow
    {
        private const string OUTPUT_PATH = "Assets/Resources/VoiceControl/Audio";
        private const int SAMPLE_RATE = 44100;

        [MenuItem("Tools/Aviation/Voice Control/Generate UI Audio")]
        public static void ShowWindow()
        {
            GetWindow<ProceduralAudioGenerator>("UI Audio Generator");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Procedural UI Audio Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "Generates clean, professional UI sounds for the radial menu. " +
                "All sounds are synthesized procedurally - no external assets needed.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Generate All Audio", GUILayout.Height(40)))
            {
                GenerateAllAudio();
            }

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Individual Sounds:", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open Sound"))
            {
                GenerateOpenSound();
                AssetDatabase.Refresh();
            }
            if (GUILayout.Button("Close Sound"))
            {
                GenerateCloseSound();
                AssetDatabase.Refresh();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select Sound"))
            {
                GenerateSelectSound();
                AssetDatabase.Refresh();
            }
            if (GUILayout.Button("Execute Sound"))
            {
                GenerateExecuteSound();
                AssetDatabase.Refresh();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Assign to Radial Menu", GUILayout.Height(30)))
            {
                AssignToRadialMenu();
            }
        }

        private void GenerateAllAudio()
        {
            EnsureDirectoryExists();

            GenerateOpenSound();
            GenerateCloseSound();
            GenerateSelectSound();
            GenerateExecuteSound();

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Success", "All UI audio generated successfully!", "OK");
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(OUTPUT_PATH))
            {
                Directory.CreateDirectory(OUTPUT_PATH);
            }
        }

        /// <summary>
        /// Open sound: Rising pitch sweep with subtle reverb-like decay
        /// </summary>
        private void GenerateOpenSound()
        {
            float duration = 0.3f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float frequency = Mathf.Lerp(400f, 1200f, t); // Rising pitch
                float envelope = Mathf.Sin(t * Mathf.PI) * 0.5f; // Smooth attack/decay
                float wave = Mathf.Sin(2f * Mathf.PI * frequency * t);
                float harmonic = Mathf.Sin(2f * Mathf.PI * frequency * 2f * t) * 0.3f;

                data[i] = (wave + harmonic) * envelope * 0.5f;
            }

            SaveAudioClip(data, "RadialMenu_Open", duration);
        }

        /// <summary>
        /// Close sound: Falling pitch sweep - inverse of open
        /// </summary>
        private void GenerateCloseSound()
        {
            float duration = 0.25f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float frequency = Mathf.Lerp(1000f, 300f, t * t); // Falling pitch with curve
                float envelope = Mathf.Sin(t * Mathf.PI) * 0.5f;
                float wave = Mathf.Sin(2f * Mathf.PI * frequency * t);

                data[i] = wave * envelope * 0.5f;
            }

            SaveAudioClip(data, "RadialMenu_Close", duration);
        }

        /// <summary>
        /// Select sound: Short, crisp tick with slight resonance
        /// </summary>
        private void GenerateSelectSound()
        {
            float duration = 0.08f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float frequency = 2000f;
                float envelope = Mathf.Exp(-t * 10f); // Fast decay
                float wave = Mathf.Sin(2f * Mathf.PI * frequency * t);
                float click = (t < 0.01f) ? 0.3f : 0f; // Sharp transient

                data[i] = (wave * envelope + click) * 0.4f;
            }

            SaveAudioClip(data, "RadialMenu_Select", duration);
        }

        /// <summary>
        /// Execute sound: Confident "completion" sound with harmonics
        /// </summary>
        private void GenerateExecuteSound()
        {
            float duration = 0.35f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] data = new float[samples];

            // Two-tone confirmation (like a pleasant bell)
            float freq1 = 880f;  // A5
            float freq2 = 1100f; // C#6

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;

                // Envelope with slight sustain
                float envelope = Mathf.SmoothStep(1f, 0f, t * 2f);
                envelope *= 1f - Mathf.Exp(-t * 20f); // Attack

                float wave1 = Mathf.Sin(2f * Mathf.PI * freq1 * t) * envelope;
                float wave2 = Mathf.Sin(2f * Mathf.PI * freq2 * t) * envelope * 0.5f;
                float wave3 = Mathf.Sin(2f * Mathf.PI * freq1 * 2f * t) * envelope * 0.25f;

                data[i] = (wave1 + wave2 + wave3) * 0.4f;
            }

            SaveAudioClip(data, "RadialMenu_Execute", duration);
        }

        private void SaveAudioClip(float[] data, string name, float duration)
        {
            // Create AudioClip
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);

            // Export as WAV
            string path = Path.Combine(OUTPUT_PATH, name + ".wav");
            byte[] wavData = ConvertToWav(data, SAMPLE_RATE);
            File.WriteAllBytes(path, wavData);

            // Import and configure
            AssetDatabase.ImportAsset(path);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer != null)
            {
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }

            Object.DestroyImmediate(clip);
        }

        private byte[] ConvertToWav(float[] samples, int sampleRate)
        {
            int byteCount = samples.Length * 2; // 16-bit samples
            byte[] wav = new byte[44 + byteCount];

            // RIFF header
            System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(wav, 0);
            System.BitConverter.GetBytes(36 + byteCount).CopyTo(wav, 4);
            System.Text.Encoding.ASCII.GetBytes("WAVE").CopyTo(wav, 8);

            // fmt chunk
            System.Text.Encoding.ASCII.GetBytes("fmt ").CopyTo(wav, 12);
            System.BitConverter.GetBytes(16).CopyTo(wav, 16); // Subchunk1Size
            System.BitConverter.GetBytes((short)1).CopyTo(wav, 20); // AudioFormat (PCM)
            System.BitConverter.GetBytes((short)1).CopyTo(wav, 22); // NumChannels
            System.BitConverter.GetBytes(sampleRate).CopyTo(wav, 24); // SampleRate
            System.BitConverter.GetBytes(sampleRate * 2).CopyTo(wav, 28); // ByteRate
            System.BitConverter.GetBytes((short)2).CopyTo(wav, 32); // BlockAlign
            System.BitConverter.GetBytes((short)16).CopyTo(wav, 34); // BitsPerSample

            // data chunk
            System.Text.Encoding.ASCII.GetBytes("data").CopyTo(wav, 36);
            System.BitConverter.GetBytes(byteCount).CopyTo(wav, 40);

            // Convert float samples to 16-bit PCM
            for (int i = 0; i < samples.Length; i++)
            {
                short sample = (short)(samples[i] * 32767f);
                System.BitConverter.GetBytes(sample).CopyTo(wav, 44 + i * 2);
            }

            return wav;
        }

        private void AssignToRadialMenu()
        {
            // Find radial menu in scene
            var menu = FindObjectOfType<UIToolkitRadialMenuAdvanced>();
            if (menu == null)
            {
                EditorUtility.DisplayDialog("Error", "No UIToolkitRadialMenuAdvanced found in scene. Please add one first.", "OK");
                return;
            }

            // Load audio clips
            AudioClip openClip = Resources.Load<AudioClip>("VoiceControl/Audio/RadialMenu_Open");
            AudioClip closeClip = Resources.Load<AudioClip>("VoiceControl/Audio/RadialMenu_Close");
            AudioClip selectClip = Resources.Load<AudioClip>("VoiceControl/Audio/RadialMenu_Select");
            AudioClip executeClip = Resources.Load<AudioClip>("VoiceControl/Audio/RadialMenu_Execute");

            // Use SerializedObject for proper undo support
            SerializedObject so = new SerializedObject(menu);

            if (openClip != null)
            {
                so.FindProperty("openSound").objectReferenceValue = openClip;
            }
            if (closeClip != null)
            {
                so.FindProperty("closeSound").objectReferenceValue = closeClip;
            }
            if (selectClip != null)
            {
                so.FindProperty("selectSound").objectReferenceValue = selectClip;
            }
            if (executeClip != null)
            {
                so.FindProperty("executeSound").objectReferenceValue = executeClip;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(menu);

            EditorUtility.DisplayDialog("Success", "Audio clips assigned to radial menu!", "OK");
        }
    }
}
