using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VoiceControl.Core;

namespace VoiceControl.UI
{
    /// <summary>
    /// Builds a compact command list inside the radial wheel panel.
    /// </summary>
    [AddComponentMenu("Voice Control/UI/Voice Command Wheel Commands Panel")]
    public class VoiceCommandWheelCommandsPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private TMP_Text headerLabel;
        [SerializeField] private TMP_Text statusLabel;

        [Header("Behavior")]
        [SerializeField] private bool autoDiscoverTargets = true;
        [SerializeField] private bool includeParameterizedCommands = true;

        [Header("Palette")]
        [SerializeField] private Color headerColor = new Color(0.2f, 0.8f, 1f, 0.95f);
        [SerializeField] private Color rowColor = new Color(0.06f, 0.08f, 0.12f, 0.9f);
        [SerializeField] private Color rowAltColor = new Color(0.04f, 0.06f, 0.1f, 0.85f);
        [SerializeField] private Color buttonColor = new Color(0.2f, 0.8f, 1f, 1f);
        [SerializeField] private Color buttonTextColor = new Color(0.02f, 0.04f, 0.06f, 1f);
        [SerializeField] private Color textColor = new Color(0.85f, 0.9f, 0.95f, 1f);

        [Header("Layout")]
        [SerializeField] private float rowHeight = 26f;
        [SerializeField] private float labelWidth = 150f;
        [SerializeField] private float inputWidth = 86f;
        [SerializeField] private int labelFontSize = 11;
        [SerializeField] private int headerFontSize = 12;

        private bool _built;
        private Coroutine _retryRoutine;
        private Sprite _iconSprite;

        private static readonly Dictionary<string, string> TargetBadges = new Dictionary<string, string>
        {
            { "weather_radar", "WX" },
            { "traffic_radar", "TFC" },
            { "indicator_system", "IND" },
            { "symbology", "SYM" },
            { "visionbriefing", "VIS" }
        };

        private void OnEnable()
        {
            var registry = VoiceCommandRegistry.Instance;
            if (registry != null)
                registry.OnRegistryUpdated += HandleRegistryUpdated;

            if (_iconSprite == null)
                _iconSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");

            if (!_built)
                Build();
        }

        private void OnDisable()
        {
            var registry = VoiceCommandRegistry.Instance;
            if (registry != null)
                registry.OnRegistryUpdated -= HandleRegistryUpdated;
        }

        private void Start()
        {
            if (!_built)
                Build();
        }

        private void HandleRegistryUpdated()
        {
            Build();
        }

        public void Build()
        {
            if (contentRoot == null)
                return;

            if (scrollRect != null && scrollRect.content != contentRoot)
                scrollRect.content = contentRoot;

            ClearChildren(contentRoot);
            SetStatus("");

            var registry = VoiceCommandRegistry.Instance ?? FindObjectOfType<VoiceCommandRegistry>();
            if (registry == null)
            {
                SetStatus("Voice command registry not found.");
                RetryBuild();
                return;
            }

            if (autoDiscoverTargets)
                registry.DiscoverTargets();

            var commands = registry.GetAllCommands();
            if (!includeParameterizedCommands)
            {
                commands = commands
                    .Where(cmd => cmd.Parameters == null || cmd.Parameters.All(p => !p.Required))
                    .ToList();
            }

            if (commands.Count == 0)
            {
                SetStatus("No commands available.");
                return;
            }

            var displayNames = registry.Targets.ToDictionary(k => k.Key, v => v.Value.DisplayName);
            var grouped = commands
                .OrderBy(c => c.TargetName)
                .ThenBy(c => c.Name)
                .GroupBy(c => c.TargetName)
                .ToList();

            int rowIndex = 0;
            foreach (var group in grouped)
            {
                string targetId = group.Key;
                string displayName = displayNames.ContainsKey(targetId) ? displayNames[targetId] : targetId;
                CreateSectionHeader(contentRoot, displayName, targetId);

                foreach (var cmd in group)
                {
                    CreateCommandRow(contentRoot, targetId, cmd, rowIndex++);
                }
            }

            _built = true;
        }

        private void RetryBuild()
        {
            if (_retryRoutine != null)
                StopCoroutine(_retryRoutine);
            _retryRoutine = StartCoroutine(RetryRoutine());
        }

        private IEnumerator RetryRoutine()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            _retryRoutine = null;
            Build();
        }

        private void CreateSectionHeader(Transform parent, string title, string targetId)
        {
            GameObject headerObj = new GameObject($"Section_{title}", typeof(RectTransform));
            headerObj.transform.SetParent(parent, false);
            RectTransform rect = headerObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, rowHeight + 2f);

            var layout = headerObj.AddComponent<LayoutElement>();
            layout.preferredHeight = rowHeight + 2f;

            Image bg = headerObj.AddComponent<Image>();
            bg.color = rowAltColor;

            GameObject badge = new GameObject("Badge", typeof(RectTransform));
            badge.transform.SetParent(headerObj.transform, false);
            RectTransform badgeRect = badge.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0, 0.5f);
            badgeRect.anchorMax = new Vector2(0, 0.5f);
            badgeRect.pivot = new Vector2(0, 0.5f);
            badgeRect.sizeDelta = new Vector2(32, rowHeight - 4f);
            badgeRect.anchoredPosition = new Vector2(6, 0);

            Image badgeBg = badge.AddComponent<Image>();
            badgeBg.color = headerColor;

            TMP_Text badgeText = CreateText(badge.transform, GetBadgeText(targetId), headerFontSize, FontStyles.Bold);
            badgeText.alignment = TextAlignmentOptions.Center;
            badgeText.color = new Color(0.02f, 0.04f, 0.06f, 1f);

            TMP_Text text = CreateText(headerObj.transform, title.ToUpper(), headerFontSize, FontStyles.Bold);
            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.offsetMin = new Vector2(44, 0);
            textRect.offsetMax = new Vector2(-6, 0);
            text.color = headerColor;
        }

        private void CreateCommandRow(Transform parent, string targetId, VoiceCommandInfo cmd, int index)
        {
            GameObject rowObj = new GameObject($"{targetId}_{cmd.Name}", typeof(RectTransform));
            rowObj.transform.SetParent(parent, false);
            RectTransform rowRect = rowObj.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0, rowHeight);

            var rowLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.spacing = 4;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childForceExpandWidth = false;
            rowLayout.padding = new RectOffset(6, 6, 2, 2);

            var rowLE = rowObj.AddComponent<LayoutElement>();
            rowLE.preferredHeight = rowHeight;

            Image rowBg = rowObj.AddComponent<Image>();
            rowBg.color = (index % 2 == 0) ? rowColor : rowAltColor;

            GameObject iconObj = new GameObject("Icon", typeof(RectTransform));
            iconObj.transform.SetParent(rowObj.transform, false);
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(12, 12);
            var iconLE = iconObj.AddComponent<LayoutElement>();
            iconLE.preferredWidth = 12;
            iconLE.preferredHeight = 12;
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = _iconSprite;
            iconImg.type = Image.Type.Sliced;
            iconImg.color = new Color(headerColor.r, headerColor.g, headerColor.b, 0.7f);

            TMP_Text label = CreateText(rowObj.transform, FormatLabel(cmd.Name), labelFontSize, FontStyles.Normal);
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(labelWidth, rowHeight - 4f);
            var labelLE = label.gameObject.AddComponent<LayoutElement>();
            labelLE.preferredWidth = labelWidth;
            labelLE.preferredHeight = rowHeight - 4f;

            var bindings = new List<VoiceCommandUIEntry.ParamBinding>();
            if (cmd.Parameters != null)
            {
                foreach (var param in cmd.Parameters)
                {
                    TMP_InputField input = CreateInputField(rowObj.transform, param);
                    bindings.Add(new VoiceCommandUIEntry.ParamBinding
                    {
                        name = param.Name,
                        type = param.Type,
                        required = param.Required,
                        inputField = input
                    });
                }
            }

            Button executeBtn = CreateButton(rowObj.transform, "RUN");
            var entry = rowObj.AddComponent<VoiceCommandUIEntry>();
            entry.Configure(targetId, cmd.Name, label, executeBtn, bindings);
        }

        private TMP_InputField CreateInputField(Transform parent, VoiceCommandParameter param)
        {
            GameObject inputObj = new GameObject($"{param.Name}_Input", typeof(RectTransform));
            inputObj.transform.SetParent(parent, false);
            RectTransform rect = inputObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(inputWidth, rowHeight - 6f);

            Image bg = inputObj.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.1f, 0.16f, 0.95f);

            TMP_InputField input = inputObj.AddComponent<TMP_InputField>();
            input.contentType = GetContentType(param.Type);
            input.characterValidation = TMP_InputField.CharacterValidation.None;

            TMP_Text text = CreateText(inputObj.transform, "", labelFontSize, FontStyles.Normal);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.color = textColor;
            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(6, 2);
            textRect.offsetMax = new Vector2(-6, -2);

            TMP_Text placeholder = CreateText(inputObj.transform, GetPlaceholder(param), labelFontSize - 1, FontStyles.Italic);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            placeholder.color = new Color(textColor.r, textColor.g, textColor.b, 0.6f);
            RectTransform phRect = placeholder.GetComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.offsetMin = new Vector2(6, 2);
            phRect.offsetMax = new Vector2(-6, -2);

            input.textComponent = text;
            input.placeholder = placeholder;

            var le = inputObj.AddComponent<LayoutElement>();
            le.preferredWidth = inputWidth;
            le.preferredHeight = rowHeight - 6f;

            return input;
        }

        private Button CreateButton(Transform parent, string label)
        {
            GameObject btnObj = new GameObject("Execute", typeof(RectTransform));
            btnObj.transform.SetParent(parent, false);
            RectTransform rect = btnObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(52, rowHeight - 6f);

            Image img = btnObj.AddComponent<Image>();
            img.color = buttonColor;

            Button btn = btnObj.AddComponent<Button>();

            TMP_Text text = CreateText(btnObj.transform, label, labelFontSize, FontStyles.Bold);
            text.alignment = TextAlignmentOptions.Center;
            text.color = buttonTextColor;
            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var le = btnObj.AddComponent<LayoutElement>();
            le.preferredWidth = 52;
            le.preferredHeight = rowHeight - 6f;

            return btn;
        }

        private TMP_Text CreateText(Transform parent, string text, int fontSize, FontStyles style)
        {
            GameObject textObj = new GameObject("Text", typeof(RectTransform));
            textObj.transform.SetParent(parent, false);
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = textColor;
            tmp.alignment = TextAlignmentOptions.Left;
            var rect = textObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return tmp;
        }

        private TMP_InputField.ContentType GetContentType(string type)
        {
            if (string.IsNullOrEmpty(type)) return TMP_InputField.ContentType.Standard;
            switch (type.ToLower())
            {
                case "number":
                case "float":
                case "double":
                    return TMP_InputField.ContentType.DecimalNumber;
                case "integer":
                case "int":
                    return TMP_InputField.ContentType.IntegerNumber;
                default:
                    return TMP_InputField.ContentType.Standard;
            }
        }

        private string GetPlaceholder(VoiceCommandParameter param)
        {
            if (param.EnumValues != null && param.EnumValues.Length > 0)
                return string.Join("/", param.EnumValues);

            return param.Required ? $"{param.Name}*" : param.Name;
        }

        private string GetBadgeText(string targetId)
        {
            if (string.IsNullOrEmpty(targetId))
                return "CMD";

            return TargetBadges.TryGetValue(targetId, out var badge)
                ? badge
                : targetId.Substring(0, Mathf.Min(3, targetId.Length)).ToUpper();
        }

        private string FormatLabel(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "Command";

            return name.Replace("_", " ");
        }

        private void SetStatus(string message)
        {
            if (headerLabel != null && string.IsNullOrEmpty(headerLabel.text))
                headerLabel.text = "COMMANDS";

            if (statusLabel == null)
                return;

            statusLabel.text = message;
            statusLabel.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        private void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Destroy(parent.GetChild(i).gameObject);
            }
        }
    }
}
