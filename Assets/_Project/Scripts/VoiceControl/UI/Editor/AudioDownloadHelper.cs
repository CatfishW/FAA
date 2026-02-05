using UnityEngine;
using UnityEditor;
using System.Diagnostics;

namespace VoiceControl.UI.Editor
{
    /// <summary>
    /// Helper to download free UI audio from open source sources.
    /// Provides direct links to royalty-free audio files.
    /// </summary>
    public class AudioDownloadHelper : EditorWindow
    {
        private const string AUDIO_PATH = "Assets/Resources/VoiceControl/Audio";

        [MenuItem("Tools/Aviation/Voice Control/Download Free UI Audio")]
        public static void ShowWindow()
        {
            GetWindow<AudioDownloadHelper>("Download UI Audio");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Free UI Audio Sources", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "Download royalty-free UI sounds from these trusted sources. " +
                "All are free for commercial use without attribution.",
                MessageType.Info);

            EditorGUILayout.Space(15);

            // Source 1: Pixabay
            DrawSourceSection(
                "1. Pixabay (Recommended)",
                "1,090+ royalty-free UI sounds",
                "License: Pixabay License (free for commercial use, no attribution)",
                "https://pixabay.com/sound-effects/search/ui/",
                new string[] {
                    "Search: 'interface', 'button', 'menu'",
                    "Look for: swoosh, click, beep, chime sounds"
                });

            EditorGUILayout.Space(10);

            // Source 2: 99sounds
            DrawSourceSection(
                "2. 99sounds",
                "Professional UI sound packs",
                "License: 100% royalty-free, no attribution",
                "https://99sounds.org/",
                new string[] {
                    "Recommended packs:",
                    "- 'UI & UX Sound Effects'",
                    "- 'Interface Sounds'"
                });

            EditorGUILayout.Space(10);

            // Source 3: Taira Komori (CC0)
            DrawSourceSection(
                "3. 小森平 (Taira Komori) - CC0/Public Domain",
                "Japanese sound designer - truly open source",
                "License: CC0 - no restrictions, truly open source",
                "https://taira-komori.jpn.org/sound_effects.html",
                new string[] {
                    "Navigate to:",
                    "- 'push_button' folder for button sounds",
                    "- 'switch' folder for toggle sounds",
                    "- 'computer' folder for UI sounds"
                });

            EditorGUILayout.Space(10);

            // Source 4: PAV's Music
            DrawSourceSection(
                "4. PAV's Music - 21 Free UI Sounds",
                "Specifically designed for UI/UX",
                "License: Royalty-free, commercial use OK",
                "https://www.pavsmusic.com/free-sound-effects/free-user-interface-sound-effects/",
                new string[] {
                    "Direct download pack",
                    "Includes: beeps, slides, swooshes",
                    "Each sound in 3 variants"
                });

            EditorGUILayout.Space(20);

            // Recommended sounds section
            EditorGUILayout.LabelField("Recommended Sounds for Radial Menu", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginVertical("box");

            DrawRecommendedSound("Open Sound", "Whoosh/swoosh upward", "MenuExpand, SwooshUp, InterfaceOpen");
            DrawRecommendedSound("Close Sound", "Whoosh/swoosh downward", "MenuClose, SwooshDown, InterfaceClose");
            DrawRecommendedSound("Select Sound", "Soft click or tick", "ButtonClick, InterfaceTick, Select");
            DrawRecommendedSound("Execute Sound", "Confirmation chime/bell", "SuccessChime, Bell, Confirm");

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(15);

            // Quick actions
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);

            if (GUILayout.Button("Open Audio Folder", GUILayout.Height(30)))
            {
                OpenAudioFolder();
            }

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Auto-Assign Downloaded Audio", GUILayout.Height(30)))
            {
                AutoAssignAudio();
            }

            EditorGUILayout.Space(20);

            EditorGUILayout.HelpBox(
                "After downloading, place WAV files in:\n" +
                AUDIO_PATH + "\n\n" +
                "Name them:\n" +
                "- RadialMenu_Open.wav\n" +
                "- RadialMenu_Close.wav\n" +
                "- RadialMenu_Select.wav\n" +
                "- RadialMenu_Execute.wav",
                MessageType.Info);
        }

        private void DrawSourceSection(string title, string description, string license, string url, string[] notes)
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField(license, EditorStyles.miniLabel);

            EditorGUILayout.Space(5);

            foreach (var note in notes)
            {
                EditorGUILayout.LabelField("  • " + note, EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Open Website"))
            {
                Application.OpenURL(url);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawRecommendedSound(string title, string description, string searchTerms)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(title, GUILayout.Width(100));
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(description, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Search: " + searchTerms, EditorStyles.miniBoldLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
        }

        private void OpenAudioFolder()
        {
            string fullPath = System.IO.Path.GetFullPath(AUDIO_PATH);

            if (!System.IO.Directory.Exists(fullPath))
            {
                System.IO.Directory.CreateDirectory(fullPath);
                AssetDatabase.Refresh();
            }

            // Open in file explorer
            Process.Start(fullPath);
        }

        private void AutoAssignAudio()
        {
            // Find radial menu in scene
            var menu = FindObjectOfType<UIToolkitRadialMenuAdvanced>();
            if (menu == null)
            {
                EditorUtility.DisplayDialog("Error", "No UIToolkitRadialMenuAdvanced found in scene.", "OK");
                return;
            }

            // Try to load audio clips with various naming conventions
            AudioClip openClip = FindAudioClip(new[] { "RadialMenu_Open", "MenuOpen", "SwooshUp", "InterfaceOpen", "open" });
            AudioClip closeClip = FindAudioClip(new[] { "RadialMenu_Close", "MenuClose", "SwooshDown", "InterfaceClose", "close" });
            AudioClip selectClip = FindAudioClip(new[] { "RadialMenu_Select", "Select", "ButtonClick", "Click", "Tick", "select" });
            AudioClip executeClip = FindAudioClip(new[] { "RadialMenu_Execute", "Execute", "Success", "Chime", "Bell", "Confirm", "execute" });

            // Use SerializedObject for proper undo support
            SerializedObject so = new SerializedObject(menu);
            int assignedCount = 0;

            if (openClip != null)
            {
                so.FindProperty("openSound").objectReferenceValue = openClip;
                assignedCount++;
            }
            if (closeClip != null)
            {
                so.FindProperty("closeSound").objectReferenceValue = closeClip;
                assignedCount++;
            }
            if (selectClip != null)
            {
                so.FindProperty("selectSound").objectReferenceValue = selectClip;
                assignedCount++;
            }
            if (executeClip != null)
            {
                so.FindProperty("executeSound").objectReferenceValue = executeClip;
                assignedCount++;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(menu);

            if (assignedCount > 0)
            {
                EditorUtility.DisplayDialog("Success", $"Assigned {assignedCount} audio clip(s) to radial menu!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("No Audio Found",
                    "No audio files found. Please download and place WAV files in:\n" + AUDIO_PATH, "OK");
            }
        }

        private AudioClip FindAudioClip(string[] possibleNames)
        {
            foreach (var name in possibleNames)
            {
                // Try Resources folder
                var clip = Resources.Load<AudioClip>("VoiceControl/Audio/" + name);
                if (clip != null) return clip;

                // Try with different extensions/paths
                string[] guids = AssetDatabase.FindAssets(name + " t:AudioClip");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                }
            }
            return null;
        }
    }
}
