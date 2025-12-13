using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CompassNavigatorPro {

    public partial class CompassPro : MonoBehaviour {

        #region Indicators

        const string INDICATORS_ROOT_NAME = "OnScreen Indicators Root";
        const int MAX_STORED_VPOS = 100;
        readonly Vector3[] lastVPos = new Vector3[MAX_STORED_VPOS];

        void InitIndicators() {
            if (_onScreenIndicatorPrefab == null) {
                _onScreenIndicatorPrefab = Resources.Load<GameObject>("CNPro/Prefabs/POIGizmo");
                if (_onScreenIndicatorPrefab == null) {
                    Debug.LogError("CompassNavigatorPro: Could not load POIGizmo prefab from Resources/CNPro/Prefabs/POIGizmo. Offscreen indicators will not work.");
                }
            }

            if (indicatorsRoot == null) {
                indicatorsRoot = transform.Find(INDICATORS_ROOT_NAME);
                if (indicatorsRoot == null) {
                    GameObject root = Resources.Load<GameObject>("CNPro/Prefabs/OnScreenIndicatorsRoot");
                    if (root != null) {
                        GameObject rootGO = Instantiate(root, transform, false);
                        rootGO.name = INDICATORS_ROOT_NAME;
                        indicatorsRoot = rootGO.transform;
                    } else {
                        Debug.LogError("CompassNavigatorPro: Could not load OnScreenIndicatorsRoot prefab from Resources/CNPro/Prefabs/OnScreenIndicatorsRoot. Creating fallback root.");
                        // Create a fallback root object
                        GameObject fallbackRoot = new GameObject(INDICATORS_ROOT_NAME);
                        fallbackRoot.transform.SetParent(transform, false);
                        indicatorsRoot = fallbackRoot.transform;
                        
                        // Add Canvas components needed for indicators
                        Canvas canvas = fallbackRoot.AddComponent<Canvas>();
                        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                        canvas.overrideSorting = true;
                        canvas.sortingOrder = 1000;
                        
                        fallbackRoot.AddComponent<GraphicRaycaster>();
                    }
                }
            }
            indicatorsRoot.gameObject.SetActive(_showOnScreenIndicators || _showOffScreenIndicators);
        }

        void UpdateIndicators() {
            
            if (_cameraMain == null) {
                Debug.LogError("CompassNavigatorPro: Camera reference is null. Indicators cannot be updated.");
                return;
            }

            float aspect = _cameraMain.aspect;

            float overlapDir = 1f;
            float distThreshold = _offScreenIndicatorOverlapDistance * 0.9f;

            float vextentsX = 0.5f - _offScreenIndicatorMargin;
            float vextentsY = 0.5f - _offScreenIndicatorMargin * aspect;
            float minX = _offScreenIndicatorMargin;
            float maxX = 1f - minX;
            float minY = _offScreenIndicatorMargin * aspect;
            float maxY = 1f - minY;
            Vector3 scaleVector = Misc.Vector3one;
            float scaleAnimSpeed = Time.deltaTime * 10f;
            int frameCount = Time.frameCount;
            int vPosCount = 0;
            int poiCount = pois.Count;

            for (int k = 0; k < poiCount; k++) {
                CompassProPOI poi = pois[k];

                if (poi == null || !poi.isActiveAndEnabled) {
                    if (poi != null) {
                        poi.ToggleIndicatorVisibility(false);
                    }
                    continue;
                }

                bool visible = true;
                
                // Hide visited POIs if they should be hidden
                if (poi.isVisited && poi.hideWhenVisited) {
                    visible = false;
                }
                
                // For on-screen indicators, also hide when mini-map is full screen (original behavior)
                // But offscreen indicators should always be available regardless of mini-map state
                bool hideForMiniMapFullScreen = _miniMapFullScreenState;

                if (!visible) {
                    poi.ToggleIndicatorVisibility(false);
                    continue;
                }

                // Update POI viewport position and distance
                if (frameCount != poi.viewportPosFrameCount) {
                    poi.viewportPosFrameCount = frameCount;
                    ComputePOIViewportPos(poi);
                }

                Vector3 vpos = poi.viewportPos;

                bool isOnScreen = vpos.z > 0 && vpos.x >= minX && vpos.x < maxX && vpos.y >= minY && vpos.y < maxY;
                float ang = 0;
                float scale = 1f;

                if (isOnScreen) {
                    // On-screen indicators are hidden when mini-map is full screen (original behavior)
                    visible = _showOnScreenIndicators && poi.showOnScreenIndicator && !hideForMiniMapFullScreen;
                    if (visible && poi.isOnScreen >= 0) {
                        poi.isOnScreen = -1;
                        OnPOIOnScreen?.Invoke(poi);
                    }
                    scale = _onScreenIndicatorScale * 0.25f;
                } else {
                    // Off-screen indicators are NOT affected by mini-map full screen state
                    visible = _showOffScreenIndicators && poi.showOffScreenIndicator;
                    if (visible) {
                        if (poi.isOnScreen <= 0) {
                            poi.isOnScreen = 1;
                            OnPOIOffScreen?.Invoke(poi);
                        }
                        scale = _offScreenIndicatorScale * 0.25f;
                        vpos.x -= 0.5f;
                        vpos.y -= 0.5f;
                        if (vpos.z < 0) {
                            vpos *= -1f;
                            if (vpos.y > 0) vpos.y = -vpos.y; // when behind, always show indicator on the bottom half of the screen
                        }
                        ang = Mathf.Atan2(vpos.y, vpos.x);
                        float s = Mathf.Tan(ang);
                        if (vpos.x > 0) {
                            vpos.x = vextentsX;
                            vpos.y = vextentsX * s;
                        } else {
                            vpos.x = -vextentsX;
                            vpos.y = -vextentsX * s;
                        }
                        if (vpos.y > vextentsY) {
                            vpos.x = vextentsY / s;
                            vpos.y = vextentsY;
                        } else if (vpos.y < -vextentsY) {
                            vpos.x = -vextentsY / s;
                            vpos.y = -vextentsY;
                        }

                        // check collision
                        if (_offScreenIndicatorAvoidOverlap) {
                            float disp = 0;
                            bool vert = vpos.x * vpos.x > vpos.y * vpos.y;
                            int maxj = Mathf.Min(vPosCount, MAX_STORED_VPOS);
                            if (vert) {
                                for (int j = 0; j < maxj; j++) {
                                    float dx = lastVPos[j].x - vpos.x;
                                    if (dx < 0) dx = -dx;
                                    float dy = lastVPos[j].y - vpos.y;
                                    if (dy < 0) dy = -dy;
                                    if (dx < distThreshold && dy < distThreshold) {
                                        if (disp <= 0) {
                                            vpos = lastVPos[j];
                                            disp = _offScreenIndicatorOverlapDistance * overlapDir;
                                        }
                                        vpos.y += disp;
                                        if (vpos.y < -0.4f || vpos.y > 0.4f) break;
                                        j = -1;
                                    }
                                }
                            } else {
                                for (int j = 0; j < maxj; j++) {
                                    float dx = lastVPos[j].x - vpos.x;
                                    if (dx < 0) dx = -dx;
                                    float dy = lastVPos[j].y - vpos.y;
                                    if (dy < 0) dy = -dy;
                                    if (dx < distThreshold && dy < distThreshold) {
                                        if (disp <= 0) {
                                            vpos = lastVPos[j];
                                            disp = _offScreenIndicatorOverlapDistance * overlapDir;
                                        }
                                        vpos.x += disp;
                                        if (vpos.x < -0.4f || vpos.x > 0.4f) break;
                                        j = -1;
                                    }
                                }
                            }
                            overlapDir = -overlapDir;
                            lastVPos[vPosCount++] = vpos;
                        }

                        vpos.x += 0.5f;
                        vpos.y += 0.5f;
                    }
                }

                if (poi.indicatorImage != null) {
                    poi.ToggleIndicatorVisibility(visible);
                    if (!visible) continue;
                } else {
                    if (!visible) continue;

                    // Add a dummy child gameObject
                    GameObject go = GetIndicator();
                    poi.indicatorRT = go.GetComponent<RectTransform>();
                    poi.indicatorCanvasGroup = go.GetComponent<CanvasGroup>();
                    GizmoElements elements = go.GetComponentInChildren<GizmoElements>();
                    if (elements == null) {
                        Debug.LogError("Gizmo prefab missing GizmoElements component.");
                        DestroyImmediate(go);
                        continue;
                    }
                    poi.indicatorImage = elements.iconImage;
                    poi.indicatorDistanceText = elements.distanceText;
                    poi.indicatorTitleText = elements.titleText;
                    poi.indicatorAltitudeText = elements.AltitudeText;
                    poi.indicatorHeadingText = elements.HeadingText;
                    poi.indicatorArrowRT = elements.arrowPivot;
                    
                    // If arrow pivot is missing, warn but continue - offscreen indicators will still show without arrow
                    if (poi.indicatorArrowRT == null) {
                        Debug.LogWarning($"POI '{poi.title}' indicator prefab is missing arrowPivot component - offscreen direction arrow will not be shown.");
                    }
                    poi.indicatorRT.localScale = Misc.Vector3zero;
                }

                RectTransform t = poi.indicatorRT;
                scaleVector.x = scaleVector.y = scale;
                Vector3 newScale = Vector3.Lerp(t.localScale, scaleVector, scaleAnimSpeed);
                t.localScale = newScale;

                if (poi.lastIndicatorViewportPos == vpos) continue;
                poi.lastIndicatorViewportPos = vpos;

                poi.indicatorRT.anchorMin = poi.indicatorRT.anchorMax = vpos;
                poi.indicatorImage.sprite = poi.isVisited && poi.iconVisited != null ? poi.iconVisited : poi.iconNonVisited;
                bool distanceVisible = isOnScreen && poi.onScreenIndicatorShowDistance && _onScreenIndicatorShowDistance;
                if (poi.indicatorDistanceText.isActiveAndEnabled != distanceVisible) {
                    poi.indicatorDistanceText.gameObject.SetActive(distanceVisible);
                }
                bool titleVisible = isOnScreen && poi.onScreenIndicatorShowTitle && _onScreenIndicatorShowTitle;
                if (poi.indicatorTitleText.isActiveAndEnabled != titleVisible) {
                    poi.indicatorTitleText.gameObject.SetActive(titleVisible);
                }
                bool altitudeVisible = isOnScreen && _onScreenIndicatorShowAltitude && poi.indicatorAltitudeText != null;
                if (poi.indicatorAltitudeText != null && poi.indicatorAltitudeText.isActiveAndEnabled != altitudeVisible) {
                    poi.indicatorAltitudeText.gameObject.SetActive(altitudeVisible);
                }
                bool headingVisible = isOnScreen && _onScreenIndicatorShowHeading && poi.indicatorHeadingText != null;
                if (poi.indicatorHeadingText != null && poi.indicatorHeadingText.isActiveAndEnabled != headingVisible) {
                    poi.indicatorHeadingText.gameObject.SetActive(headingVisible);
                }

                float iconAlpha;
                if (isOnScreen) {
                    float nearFadeMin = poi.onScreenIndicatorNearFadeMin > 0 ? poi.onScreenIndicatorNearFadeMin : _onScreenIndicatorNearFadeMin;
                    float nearFadeDistance = poi.onScreenIndicatorNearFadeDistance > 0 ? poi.onScreenIndicatorNearFadeDistance : _onScreenIndicatorNearFadeDistance;
                    float gizmoAlphaFactor = nearFadeDistance <= nearFadeMin ? 1f : Mathf.Clamp01((poi.distanceToFollow - nearFadeMin) / (nearFadeDistance - nearFadeMin));
                    iconAlpha = _onScreenIndicatorAlpha * gizmoAlphaFactor;
                    if (poi.onScreenIndicatorShowDistance && _onScreenIndicatorShowDistance) {
                        if (poi.prevIndicatorDistance != poi.distanceToFollow) {
                            poi.prevIndicatorDistance = poi.distanceToFollow;
                            // Convert meters to nautical miles (1 nm = 1852 meters)
                            float distanceInNauticalMiles = poi.distanceToFollow / 1852f;
                            // Use dynamic precision: show more decimal places for smaller distances
                            string format = distanceInNauticalMiles < 1.0f ? "0.000nm" : 
                                           distanceInNauticalMiles < 10.0f ? "0.00nm" : 
                                           distanceInNauticalMiles < 100.0f ? "0.0nm" : "0nm";
                            poi.lastIndicatorDistanceText = distanceInNauticalMiles.ToString(format);
                            poi.indicatorDistanceText.text = poi.lastIndicatorDistanceText;
                        }
                    }

                    if (poi.onScreenIndicatorShowTitle && _onScreenIndicatorShowTitle) {
                        if (!poi.indicatorTitleText.enabled) {
                            poi.indicatorTitleText.enabled = true;
                        }
                        poi.indicatorTitleText.text = poi.title;
                        if (vpos.x > 0.85f) {
                            poi.indicatorTitleText.alignment = TextAlignmentOptions.MidlineRight;
                        } else if (vpos.x < 0.15f) {
                            poi.indicatorTitleText.alignment = TextAlignmentOptions.MidlineLeft;
                        } else {
                            poi.indicatorTitleText.alignment = TextAlignmentOptions.Midline;
                        }
                    }
                    


                    // Display altitude if enabled and altitude text component exists
                    if (poi.indicatorAltitudeText != null && _onScreenIndicatorShowAltitude)
                    {
                        if (poi.prevIndicatorAltitude != poi.altitude)
                        {
                            poi.prevIndicatorAltitude = poi.altitude;
                            // Convert relative altitude from meters to feet (1 meter = 3.28084 feet)
                            float relativeAltitudeInFeet = poi.altitude * 3.28084f;
                            // Convert to hundreds of feet and format as +XX or -XX
                            int altitudeInHundredsFeet = Mathf.RoundToInt(relativeAltitudeInFeet / 100f);
                            string sign = altitudeInHundredsFeet >= 0 ? "+" : "";
                            poi.lastIndicatorAltitudeText = $"{sign}{altitudeInHundredsFeet:00}";
                            poi.indicatorAltitudeText.text = poi.lastIndicatorAltitudeText;
                        }
                    }

                    // Display heading if enabled and heading text component exists
                    if (poi.indicatorHeadingText != null && _onScreenIndicatorShowHeading) {
                        if (poi.prevIndicatorHeading != poi.heading) {
                            poi.prevIndicatorHeading = poi.heading;
                            // Format heading as 3-digit degrees (000-359°)
                            int headingDegrees = Mathf.RoundToInt(poi.heading) % 360;
                            if (headingDegrees < 0) headingDegrees += 360; // Ensure positive
                            poi.lastIndicatorHeadingText = $"{headingDegrees:000}°";
                            poi.indicatorHeadingText.text = poi.lastIndicatorHeadingText;
                        }
                    }
                } else {
                    iconAlpha = _offScreenIndicatorAlpha;
                    if (poi.indicatorArrowRT != null) {
                        poi.indicatorArrowRT.localRotation = Quaternion.Euler(0, 0, ang * Mathf.Rad2Deg);
                    }
                }

                poi.indicatorImage.color = poi.tintColor;
                poi.indicatorCanvasGroup.alpha = iconAlpha;
                
                // Handle arrow visibility for offscreen indicators
                if (poi.indicatorArrowRT != null) {
                    poi.indicatorArrowRT.gameObject.SetActive(!isOnScreen);
                } else if (!isOnScreen) {
                    // If no arrow component but indicator should be offscreen, ensure it's still visible
                    // by making the main icon more prominent for offscreen indicators
                    poi.indicatorImage.color = new Color(poi.tintColor.r, poi.tintColor.g, poi.tintColor.b, poi.tintColor.a * 1.2f);
                }
            }
        }

        GameObject GetIndicator() {
            GameObject indicatorGO = Instantiate(_onScreenIndicatorPrefab, indicatorsRoot, false);
            return indicatorGO;
        }
        #endregion

    }

}

