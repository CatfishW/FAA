using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VoiceControl.Core;

namespace VoiceControl.UI
{
    /// <summary>
    /// Runtime UI entry for executing a single voice command with optional parameters.
    /// Used by the Voice Control setup wizard to create manual controls for all commands.
    /// </summary>
    [AddComponentMenu("Voice Control/UI/Voice Command UI Entry")]
    public class VoiceCommandUIEntry : MonoBehaviour
    {
        [Serializable]
        public class ParamBinding
        {
            public string name;
            public string type;
            public bool required;
            public TMP_InputField inputField;
        }

        [Header("Command")]
        [SerializeField] private string targetId;
        [SerializeField] private string commandName;

        [Header("UI")]
        [SerializeField] private TMP_Text label;
        [SerializeField] private Button executeButton;
        [SerializeField] private List<ParamBinding> parameters = new List<ParamBinding>();

        private void OnEnable()
        {
            if (executeButton != null)
            {
                executeButton.onClick.RemoveListener(Execute);
                executeButton.onClick.AddListener(Execute);
            }
        }

        private void OnDisable()
        {
            if (executeButton != null)
            {
                executeButton.onClick.RemoveListener(Execute);
            }
        }

        public void Configure(string target, string command, TMP_Text labelText, Button button, List<ParamBinding> paramBindings)
        {
            targetId = target;
            commandName = command;
            label = labelText;
            executeButton = button;
            parameters = paramBindings ?? new List<ParamBinding>();

            if (executeButton != null)
            {
                executeButton.onClick.RemoveListener(Execute);
                executeButton.onClick.AddListener(Execute);
            }
        }

        public void Execute()
        {
            var registry = VoiceCommandRegistry.Instance;
            if (registry == null)
            {
                Debug.LogWarning("[VoiceCommandUIEntry] VoiceCommandRegistry not found");
                return;
            }

            if (!registry.HasTarget(targetId))
            {
                registry.DiscoverTargets();
            }

            var args = BuildArguments();
            bool success = registry.ExecuteCommand(targetId, commandName, args);

            if (!success)
            {
                Debug.LogWarning($"[VoiceCommandUIEntry] Failed to execute {targetId}_{commandName}");
            }
        }

        private Dictionary<string, object> BuildArguments()
        {
            var args = new Dictionary<string, object>();

            foreach (var param in parameters)
            {
                if (param == null || param.inputField == null)
                    continue;

                string raw = param.inputField.text != null ? param.inputField.text.Trim() : string.Empty;
                if (string.IsNullOrEmpty(raw))
                {
                    if (param.required)
                    {
                        Debug.LogWarning($"[VoiceCommandUIEntry] Missing required parameter: {param.name}");
                    }
                    continue;
                }

                if (TryParseValue(param.type, raw, out object parsed))
                {
                    args[param.name] = parsed;
                }
                else
                {
                    Debug.LogWarning($"[VoiceCommandUIEntry] Invalid value for {param.name}: {raw}");
                }
            }

            return args;
        }

        private bool TryParseValue(string type, string raw, out object value)
        {
            value = raw;

            if (string.IsNullOrEmpty(type))
                return true;

            switch (type.ToLower())
            {
                case "number":
                case "float":
                case "double":
                    if (float.TryParse(raw, out float f))
                    {
                        value = f;
                        return true;
                    }
                    return false;
                case "integer":
                case "int":
                    if (int.TryParse(raw, out int i))
                    {
                        value = i;
                        return true;
                    }
                    return false;
                case "boolean":
                case "bool":
                    if (bool.TryParse(raw, out bool b))
                    {
                        value = b;
                        return true;
                    }
                    if (raw == "0" || raw == "1")
                    {
                        value = raw == "1";
                        return true;
                    }
                    return false;
                default:
                    value = raw;
                    return true;
            }
        }
    }
}
