/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace OnlineMaps.Editors.Windows
{
    public class Updater : EditorWindow 
    {
        private const string packageID = "Online Maps v4";
        private const string assetPrefix = "OnlineMaps";
        private const string lastVersionKey = assetPrefix + "LastVersion";
        private const string lastVersionCheckKey = assetPrefix + "LastVersionCheck";
        private const string channelKey = assetPrefix + "UpdateChannel";
        private const string invoiceNumberKey = assetPrefix + "InvoiceNumber";

        public static bool hasNewVersion = false;

        private static UpdateChannel channel = UpdateChannel.stable;
        private string invoiceNumber;
        private Vector2 scrollPosition;
        private List<UpdateItem> updates;
        private static string lastVersionID;
        private GUIContent helpContent;

        private void CheckNewVersions()
        {
            if (string.IsNullOrEmpty(invoiceNumber))
            {
                EditorUtility.DisplayDialog("Error", "Please enter the Invoice Number.", "OK");
                return;
            }

            SavePrefs();

            string updateKey = GetUpdateKey();
            GetUpdateList(updateKey);
        }

        public static void CheckNewVersionAvailable()
        {
            if (EditorPrefs.HasKey(lastVersionKey))
            {
                lastVersionID = EditorPrefs.GetString(lastVersionKey);

                if (CompareVersions())
                {
                    hasNewVersion = true;
                    return;
                }
            }

            const long ticksInHour = 36000000000;

            if (EditorPrefs.HasKey(lastVersionCheckKey))
            {
                long lastVersionCheck = EditorPrefs.GetInt(lastVersionCheckKey) * ticksInHour;
                if (DateTime.Now.Ticks - lastVersionCheck < 24 * ticksInHour)
                {
                    return;
                }
            }

            EditorPrefs.SetInt(lastVersionCheckKey, (int)(DateTime.Now.Ticks / ticksInHour));

            if (EditorPrefs.HasKey(channelKey)) channel = (UpdateChannel)EditorPrefs.GetInt(channelKey);
            else channel = UpdateChannel.stable;

            if (channel == UpdateChannel.stablePrevious) channel = UpdateChannel.stable;

            WebClient client = new WebClient();
         
            client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
            client.UploadDataCompleted += delegate(object sender, UploadDataCompletedEventArgs response)
            {
                if (response.Error != null)
                {
                    Debug.Log(response.Error.Message);
                    return;
                }

                string version = Encoding.UTF8.GetString(response.Result);

                try
                {
                    string[] vars = version.Split(new[] { '.' });
                    string[] vars2 = new string[4];
                    while (vars[1].Length < 8) vars[1] += "0";
                    vars2[0] = vars[0];
                    vars2[1] = int.Parse(vars[1].Substring(0, 2)).ToString();
                    vars2[2] = int.Parse(vars[1].Substring(2, 2)).ToString();
                    vars2[3] = int.Parse(vars[1].Substring(4)).ToString();

                    version = string.Join(".", vars2);
                }
                catch (Exception)
                {
                    Debug.Log("Automatic check for Online Maps updates: Bad response.");
                    return;
                }

                lastVersionID = version;

                hasNewVersion = CompareVersions();
                EditorApplication.update += SetLastVersion;
            };
            client.UploadDataAsync(new Uri("https://infinity-code.com/products_update/getlastversion.php"), "POST", Encoding.UTF8.GetBytes("c=" + (int)channel + "&package=" + WebRequest.EscapeURL(packageID)));
        }

        private static bool CompareVersions()
        {
            double v1 = GetDoubleVersion(Map.version);
            double v2 = GetDoubleVersion(lastVersionID);
            return v1 < v2;
        }

        private static double GetDoubleVersion(string v)
        {
            string[] vs = v.Split(new[] {"."}, StringSplitOptions.RemoveEmptyEntries);
            if (vs[1].Length < 2) vs[1] = "0" + vs[1];
            if (vs[2].Length < 2) vs[2] = "0" + vs[2];
            if (vs[3].Length < 4)
            {
                vs[3] = "000" + vs[3];
                vs[3] = vs[3].Substring(vs[3].Length - 4, 4);
            }
            v = vs[0] + "." + vs[1] + vs[2] + vs[3];
            double result;
            if (!double.TryParse(v, NumberStyles.AllowDecimalPoint, Culture.numberFormat, out result)) result = 1;
            return result;
        }

        private string GetUpdateKey()
        {
            WebClient client = new WebClient();
            client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
            string updateKey = client.UploadString("https://infinity-code.com/products_update/getupdatekey.php",
                "key=" + invoiceNumber + "&package=" + WebRequest.EscapeURL(packageID));

            return updateKey;
        }

        private void GetUpdateList(string updateKey)
        {
            WebClient client = new WebClient();
            client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";

            string response;

            try
            {
                response = client.UploadString("https://infinity-code.com/products_update/checkupdates.php",
                    "k=" + WebRequest.EscapeURL(updateKey) + "&v=" + Map.version + "&c=" + (int)channel);
            }
            catch(Exception exception)
            {
                Debug.Log(exception.Message);
                return;
            }

            XML xml = XML.Load(response);
            updates = new List<UpdateItem>();

            foreach (XML node in xml) updates.Add(new UpdateItem(node));
        }

        private static string Unescape(string str)
        {
            return Regex.Replace(str, @"&(\w+);", delegate(Match match)
            {
                string v = match.Groups[1].Value;
                if (v == "amp") return "&";
                if (v == "lt") return "<";
                if (v == "gt") return ">";
                if (v == "quot") return "\"";
                if (v == "apos") return "'";
                return match.Value;
            });
        }

        private void OnEnable()
        {
            if (EditorPrefs.HasKey(invoiceNumberKey)) invoiceNumber = EditorPrefs.GetString(invoiceNumberKey);
            else invoiceNumber = "";

            if (EditorPrefs.HasKey(channelKey)) channel = (UpdateChannel)EditorPrefs.GetInt(channelKey);
            else channel = UpdateChannel.stable;

            helpContent = new GUIContent(EditorUtils.LoadAsset<Texture2D>("Icons/HelpIcon.png"), "You can find out your Invoice Number in the email confirming the purchase, or page the user in Unity Asset Store.\nClick to go to the Unity Asset Store.");
        }

        private void OnDestroy()
        {
            SavePrefs();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            invoiceNumber = EditorGUILayout.TextField("Invoice Number:", invoiceNumber).Trim(' ');

            GUIStyle helpStyle = new GUIStyle();
            helpStyle.margin = new RectOffset(2, 2, 2, 2);

            if (helpContent != null && GUILayout.Button(helpContent, helpStyle, GUILayout.ExpandWidth(false)))
            {
                Process.Start("https://assetstore.unity.com/orders");
            }

            EditorGUILayout.EndHorizontal();

            channel = (UpdateChannel) EditorGUILayout.EnumPopup("Channel:", channel);
            GUILayout.Label("Current version: " + Map.version);

            if (GUILayout.Button("Check new versions")) CheckNewVersions();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            if (updates != null)
            {
                foreach (UpdateItem update in updates) update.Draw();
                if (updates.Count == 0) GUILayout.Label("No updates");
            }

            EditorGUILayout.EndScrollView();
        }

        [MenuItem(EditorUtils.MenuPath + "Check Updates", false, 2)]
        public static void OpenWindow()
        {
            GetWindow<Updater>(false, "Online Maps Updater", true);
        }

        private void SavePrefs()
        {
            EditorPrefs.SetString(invoiceNumberKey, invoiceNumber);
            EditorPrefs.SetInt(channelKey, (int) channel);
        }

        private static void SetLastVersion()
        {
            EditorPrefs.SetString(lastVersionKey, lastVersionID);
            EditorApplication.update -= SetLastVersion;
        }

        public class UpdateItem
        {
            private string version;
            private int type;
            private string changelog;
            private string download;
            private string date;

            private static GUIStyle _changelogStyle;
            private static GUIStyle _titleStyle;

            private static GUIStyle changelogStyle
            {
                get { return _changelogStyle ?? (_changelogStyle = new GUIStyle(EditorStyles.label) { wordWrap = true }); }
            }

            private static GUIStyle titleStyle
            {
                get
                {
                    return _titleStyle ??
                           (_titleStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter });
                }
            }

            public UpdateItem(XML node)
            {
                version = node.Get<string>("Version");
                type = node.Get<int>("Type");
                changelog = Unescape(node.Get<string>("ChangeLog"));
                download = node.Get<string>("Download");
                date = node.Get<string>("Date");

                string[] vars = version.Split('.');
                string[] vars2 = new string[4];
                vars2[0] = vars[0];
                vars2[1] = int.Parse(vars[1].Substring(0, 2)).ToString();
                vars2[2] = int.Parse(vars[1].Substring(2, 2)).ToString();
                vars2[3] = int.Parse(vars[1].Substring(4, 4)).ToString();
                version = string.Join(".", vars2);
            }

            public void Draw()
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("Version: " + version + " (" + typeStr + "). " + date, titleStyle);

                GUILayout.Label(changelog, changelogStyle);

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Download"))
                {
                    Process.Start("https://infinity-code.com/products_update/download.php?k=" + download);
                }

                if (GUILayout.Button("Copy download link", GUILayout.ExpandWidth(false)))
                {
                    EditorGUIUtility.systemCopyBuffer = "https://infinity-code.com/products_update/download.php?k=" + download;
                    EditorUtility.DisplayDialog("Success",
                        "Download link is copied to the clipboard.\nOpen a browser and paste the link into the address bar.",
                        "OK");
                }

                EditorGUILayout.EndHorizontal();

                GUILayout.EndVertical();
            }

            public string typeStr
            {
                get { return Enum.GetName(typeof(UpdateChannel), type); }
            }
        }

        public enum UpdateChannel
        {
            stable = 10,
            stablePrevious = 15,
            releaseCandidate = 20,
            beta = 30,
            alpha = 40,
            working = 50
        }
    }
}