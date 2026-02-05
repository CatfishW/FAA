using UnityEngine;
using UnityEngine.UI;

namespace VoiceControl.UI
{
    /// <summary>
    /// Spawns the voice command wheel from a prefab at runtime.
    /// </summary>
    [AddComponentMenu("Voice Control/UI/Voice Command Wheel Spawner")]
    public class VoiceCommandWheelSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject wheelPrefab;
        [SerializeField] private bool spawnOnStart = true;
        [SerializeField] private Vector2 anchoredPosition = new Vector2(240, -220);
        [SerializeField] private bool createCanvasIfMissing = true;

        private GameObject _instance;

        private void Start()
        {
            if (spawnOnStart)
                Spawn();
        }

        public void Spawn()
        {
            if (_instance != null)
                return;

            if (wheelPrefab == null)
            {
                wheelPrefab = Resources.Load<GameObject>("VoiceControl/VoiceCommandWheel");
            }

            if (wheelPrefab == null)
            {
                Debug.LogWarning("[VoiceCommandWheelSpawner] Prefab not assigned and not found in Resources/VoiceControl.");
                return;
            }

            Canvas canvas = FindCommandCanvas();
            if (canvas == null)
            {
                Debug.LogWarning("[VoiceCommandWheelSpawner] No Canvas found.");
                return;
            }

            _instance = Instantiate(wheelPrefab, canvas.transform, false);
            var rect = _instance.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = anchoredPosition;
            }
        }

        private Canvas FindCommandCanvas()
        {
            var canvasObj = GameObject.Find("VoiceCommandCanvas");
            if (canvasObj != null)
            {
                var existing = canvasObj.GetComponent<Canvas>();
                if (existing != null)
                    return existing;
            }

            var anyCanvas = FindObjectOfType<Canvas>();
            if (anyCanvas != null)
                return anyCanvas;

            if (!createCanvasIfMissing)
                return null;

            GameObject newCanvas = new GameObject("VoiceCommandCanvas");
            Canvas canvas = newCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 110;
            newCanvas.AddComponent<UnityEngine.UI.CanvasScaler>();
            newCanvas.AddComponent<GraphicRaycaster>();
            return canvas;
        }
    }
}
