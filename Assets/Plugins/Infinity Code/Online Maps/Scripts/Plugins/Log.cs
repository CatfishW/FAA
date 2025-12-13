/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Collections.Generic;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace OnlineMaps
{
    /// <summary>
    /// A class for logging events in the Online Maps.
    /// </summary>
    [Plugin("Log")]
    [AddComponentMenu("Infinity Code/Online Maps/Plugins/Log")]
    public class Log : MonoBehaviour
    {
        private static Log _instance;
        private static bool missed;

        /// <summary>
        /// Display events using IMGUI.
        /// </summary>
        [Tooltip("Display events using IMGUI.")]
        public bool logOnUI;

        /// <summary>
        /// Font size for the log messages.
        /// </summary>
        public int fontSize = 14;

        /// <summary>
        /// Font color for the log messages.
        /// </summary>
        public Color fontColor = Color.white;

        /// <summary>
        /// Event Types
        /// </summary>
        [Header("Event Types")]
        public bool cacheEvents;

        /// <summary>
        /// Shows the events of interactive elements that have subscriptions.
        /// </summary>
        [Tooltip("Shows the events of interactive elements that have subscriptions")]
        public bool interactiveElementEvents;

        /// <summary>
        /// Shows the events of the map that have subscriptions.
        /// </summary>
        [Tooltip("Shows the events of map that have subscriptions")]
        public bool mapEvents;

        /// <summary>
        /// Shows the request events.
        /// </summary>
        public bool requestEvents;

        private static List<string> messages = new List<string>();
        private static GUIStyle style;

        /// <summary>
        /// Gets the singleton instance of the Log class.
        /// </summary>
        public static Log instance
        {
            get
            {
                if (!_instance && !missed)
                {
                    _instance = Compatibility.FindObjectOfType<Log>();
                    missed = !_instance;
                }

                return _instance;
            }
        }

        private static void AddUIMessage(string message)
        {
            if (!instance.logOnUI) return;

            while (messages.Count > 19)
            {
                messages.RemoveAt(messages.Count - 1);
            }

            messages.Insert(0, message);
        }

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="type">The type of event.</param>
        public static void Info(string message, Type type)
        {
            if (!ValidateType(type)) return;

            Debug.Log(message);
            AddUIMessage(message);
        }

        private void OnEnable()
        {
            _instance = this;
        }

        private void OnGUI()
        {
            if (!logOnUI) return;

            if (style == null)
            {
                style = new GUIStyle()
                {
                    fontSize = fontSize,
                    normal =
                    {
                        textColor = fontColor
                    }
                };
            }

            foreach (string message in messages)
            {
                GUILayout.Label(message, style);
            }
        }

        private static bool ValidateType(Type type)
        {
            if (!instance) return false;
            if (!instance.enabled) return false;

            switch (type)
            {
                case Type.request:
                    return instance.requestEvents;
                case Type.cache:
                    return instance.cacheEvents;
                case Type.interactiveElement:
                    return instance.interactiveElementEvents;
                case Type.map:
                    return instance.mapEvents;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The warning message to log.</param>
        /// <param name="type">The type of event.</param>
        public static void Warning(string message, Type type)
        {
            if (!ValidateType(type)) return;

            Debug.LogWarning(message);
            AddUIMessage("[WARNING] " + message);
        }

        /// <summary>
        /// Types of events that can be logged.
        /// </summary>
        public enum Type
        {
            /// <summary>
            /// Cache events.
            /// </summary>
            cache,

            /// <summary>
            /// Interactive element events.
            /// </summary>
            interactiveElement,

            /// <summary>
            /// Map events.
            /// </summary>
            map,

            /// <summary>
            /// Request events.
            /// </summary>
            request
        }
    }
}