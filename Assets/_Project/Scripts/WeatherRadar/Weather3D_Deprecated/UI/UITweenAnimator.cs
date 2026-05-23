using UnityEngine;
using System;
using System.Collections.Generic;

namespace WeatherRadar.Weather3D.UI
{
    /// <summary>
    /// High-performance script-based UI animation utility.
    /// No Unity Animator required - uses pooled tweens for zero GC.
    /// </summary>
    public class UITweenAnimator : MonoBehaviour
    {
        private static UITweenAnimator _instance;
        public static UITweenAnimator Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("UITweenAnimator");
                    _instance = go.AddComponent<UITweenAnimator>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // Active tweens
        private readonly List<Tween> _activeTweens = new List<Tween>(32);
        private readonly List<Tween> _tweenPool = new List<Tween>(32);

        #region Tween Types

        public enum EaseType
        {
            Linear,
            EaseInQuad,
            EaseOutQuad,
            EaseInOutQuad,
            EaseOutBack,
            EaseOutElastic
        }

        private class Tween
        {
            public RectTransform target;
            public CanvasGroup canvasGroup;
            public TweenType type;
            public Vector2 startPos;
            public Vector2 endPos;
            public float startValue;
            public float endValue;
            public Vector3 startScale;
            public Vector3 endScale;
            public float duration;
            public float elapsed;
            public EaseType easeType;
            public Action onComplete;
            public bool isActive;

            public void Reset()
            {
                target = null;
                canvasGroup = null;
                onComplete = null;
                isActive = false;
                elapsed = 0f;
            }
        }

        private enum TweenType
        {
            Move,
            Fade,
            Scale
        }

        #endregion

        #region Unity Lifecycle

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            
            for (int i = _activeTweens.Count - 1; i >= 0; i--)
            {
                var tween = _activeTweens[i];
                if (!tween.isActive)
                {
                    ReturnToPool(tween);
                    _activeTweens.RemoveAt(i);
                    continue;
                }

                tween.elapsed += dt;
                float t = Mathf.Clamp01(tween.elapsed / tween.duration);
                float easedT = ApplyEasing(t, tween.easeType);

                switch (tween.type)
                {
                    case TweenType.Move:
                        if (tween.target != null)
                        {
                            tween.target.anchoredPosition = Vector2.LerpUnclamped(
                                tween.startPos, tween.endPos, easedT);
                        }
                        break;

                    case TweenType.Fade:
                        if (tween.canvasGroup != null)
                        {
                            tween.canvasGroup.alpha = Mathf.LerpUnclamped(
                                tween.startValue, tween.endValue, easedT);
                        }
                        break;

                    case TweenType.Scale:
                        if (tween.target != null)
                        {
                            tween.target.localScale = Vector3.LerpUnclamped(
                                tween.startScale, tween.endScale, easedT);
                        }
                        break;
                }

                if (t >= 1f)
                {
                    tween.isActive = false;
                    tween.onComplete?.Invoke();
                    ReturnToPool(tween);
                    _activeTweens.RemoveAt(i);
                }
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Animate position from current to target.
        /// </summary>
        public static void Move(RectTransform target, Vector2 to, float duration, 
            EaseType ease = EaseType.EaseOutQuad, Action onComplete = null)
        {
            if (target == null) return;
            Instance.StartMoveTween(target, target.anchoredPosition, to, duration, ease, onComplete);
        }

        /// <summary>
        /// Animate position from specified start to end.
        /// </summary>
        public static void MoveTo(RectTransform target, Vector2 from, Vector2 to, float duration,
            EaseType ease = EaseType.EaseOutQuad, Action onComplete = null)
        {
            if (target == null) return;
            Instance.StartMoveTween(target, from, to, duration, ease, onComplete);
        }

        /// <summary>
        /// Animate CanvasGroup alpha.
        /// </summary>
        public static void Fade(CanvasGroup target, float to, float duration,
            EaseType ease = EaseType.EaseOutQuad, Action onComplete = null)
        {
            if (target == null) return;
            Instance.StartFadeTween(target, target.alpha, to, duration, ease, onComplete);
        }

        /// <summary>
        /// Animate CanvasGroup alpha from specified values.
        /// </summary>
        public static void FadeTo(CanvasGroup target, float from, float to, float duration,
            EaseType ease = EaseType.EaseOutQuad, Action onComplete = null)
        {
            if (target == null) return;
            Instance.StartFadeTween(target, from, to, duration, ease, onComplete);
        }

        /// <summary>
        /// Animate scale.
        /// </summary>
        public static void Scale(RectTransform target, Vector3 to, float duration,
            EaseType ease = EaseType.EaseOutQuad, Action onComplete = null)
        {
            if (target == null) return;
            Instance.StartScaleTween(target, target.localScale, to, duration, ease, onComplete);
        }

        /// <summary>
        /// Cancel all tweens on target.
        /// </summary>
        public static void Cancel(RectTransform target)
        {
            Instance.CancelTweens(target);
        }

        /// <summary>
        /// Cancel all tweens on CanvasGroup.
        /// </summary>
        public static void Cancel(CanvasGroup target)
        {
            Instance.CancelTweens(target);
        }

        #endregion

        #region Private Methods

        private void StartMoveTween(RectTransform target, Vector2 from, Vector2 to, 
            float duration, EaseType ease, Action onComplete)
        {
            // Cancel existing move tweens on this target
            CancelTweens(target, TweenType.Move);

            var tween = GetFromPool();
            tween.target = target;
            tween.type = TweenType.Move;
            tween.startPos = from;
            tween.endPos = to;
            tween.duration = duration;
            tween.elapsed = 0f;
            tween.easeType = ease;
            tween.onComplete = onComplete;
            tween.isActive = true;

            target.anchoredPosition = from;
            _activeTweens.Add(tween);
        }

        private void StartFadeTween(CanvasGroup target, float from, float to,
            float duration, EaseType ease, Action onComplete)
        {
            // Cancel existing fade tweens
            CancelTweens(target);

            var tween = GetFromPool();
            tween.canvasGroup = target;
            tween.type = TweenType.Fade;
            tween.startValue = from;
            tween.endValue = to;
            tween.duration = duration;
            tween.elapsed = 0f;
            tween.easeType = ease;
            tween.onComplete = onComplete;
            tween.isActive = true;

            target.alpha = from;
            _activeTweens.Add(tween);
        }

        private void StartScaleTween(RectTransform target, Vector3 from, Vector3 to,
            float duration, EaseType ease, Action onComplete)
        {
            CancelTweens(target, TweenType.Scale);

            var tween = GetFromPool();
            tween.target = target;
            tween.type = TweenType.Scale;
            tween.startScale = from;
            tween.endScale = to;
            tween.duration = duration;
            tween.elapsed = 0f;
            tween.easeType = ease;
            tween.onComplete = onComplete;
            tween.isActive = true;

            target.localScale = from;
            _activeTweens.Add(tween);
        }

        private void CancelTweens(RectTransform target, TweenType? type = null)
        {
            for (int i = _activeTweens.Count - 1; i >= 0; i--)
            {
                var tween = _activeTweens[i];
                if (tween.target == target && (type == null || tween.type == type))
                {
                    tween.isActive = false;
                }
            }
        }

        private void CancelTweens(CanvasGroup target)
        {
            for (int i = _activeTweens.Count - 1; i >= 0; i--)
            {
                var tween = _activeTweens[i];
                if (tween.canvasGroup == target)
                {
                    tween.isActive = false;
                }
            }
        }

        private Tween GetFromPool()
        {
            if (_tweenPool.Count > 0)
            {
                var tween = _tweenPool[_tweenPool.Count - 1];
                _tweenPool.RemoveAt(_tweenPool.Count - 1);
                return tween;
            }
            return new Tween();
        }

        private void ReturnToPool(Tween tween)
        {
            tween.Reset();
            _tweenPool.Add(tween);
        }

        private float ApplyEasing(float t, EaseType ease)
        {
            switch (ease)
            {
                case EaseType.Linear:
                    return t;

                case EaseType.EaseInQuad:
                    return t * t;

                case EaseType.EaseOutQuad:
                    return t * (2f - t);

                case EaseType.EaseInOutQuad:
                    return t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;

                case EaseType.EaseOutBack:
                    float c1 = 1.70158f;
                    float c3 = c1 + 1f;
                    return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);

                case EaseType.EaseOutElastic:
                    if (t == 0f) return 0f;
                    if (t == 1f) return 1f;
                    float p = 0.3f;
                    return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t - p / 4f) * (2f * Mathf.PI) / p) + 1f;

                default:
                    return t;
            }
        }

        #endregion
    }
}
