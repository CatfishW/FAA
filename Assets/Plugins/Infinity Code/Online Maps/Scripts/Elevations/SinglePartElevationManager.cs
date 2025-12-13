/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Implements elevation managers, which loads elevation data in one piece.
    /// </summary>
    /// <typeparam name="T">Type of elevation manager</typeparam>
    public abstract class SinglePartElevationManager<T> : ElevationManager<T>
        where T : SinglePartElevationManager<T>
    {
        /// <summary>
        /// Indicates whether to update values with a tween animation.
        /// </summary>
        public bool tweenUpdateValues;

        /// <summary>
        /// Duration of the tween animation.
        /// </summary>
        public float tweenDuration = 0.5f;

        /// <summary>
        /// The geographical bounds of the current request.
        /// </summary>
        protected GeoRect requestRect;

        /// <summary>
        /// The geographical bounds of the current data.
        /// </summary>
        protected GeoRect dataRect;

        /// <summary>
        /// The geographical bounds of the previous data.
        /// </summary>
        protected GeoRect prevRect;

        /// <summary>
        /// The elevation data.
        /// </summary>
        protected short[,] elevationData;

        /// <summary>
        /// The width of the elevation data.
        /// </summary>
        protected int elevationDataWidth;

        /// <summary>
        /// The height of the elevation data.
        /// </summary>
        protected int elevationDataHeight;

        /// <summary>
        /// Indicates whether to wait for setting elevation data.
        /// </summary>
        protected bool waitSetElevationData;

        /// <summary>
        /// Indicates whether the tween animation has started.
        /// </summary>
        protected bool tweenStarted;

        /// <summary>
        /// The progress of the tween animation.
        /// </summary>
        protected float tweenProgress;

        /// <summary>
        /// The previous elevation data.
        /// </summary>
        protected short[,] prevData;

        /// <summary>
        /// Gets a value indicating whether the elevation data is available.
        /// </summary>
        public override bool hasData => elevationData != null;

        public override float GetElevationValue(double x, double z, float yScale, GeoRect rect)
        {
            if (elevationData == null) return 0;

            float v = GetUnscaledElevationValue(x, z, rect);
            if (bottomMode == ElevationBottomMode.minValue) v -= minValue;
            return v * yScale * scale;
        }

        public override float GetUnscaledElevationValue(double x, double z, GeoRect rect)
        {
            if (elevationData == null) return 0;
            if (elevationDataWidth == 0 || elevationDataHeight == 0 || dataRect.size.sqrMagnitude == 0) return 0;

            x = x / -_sizeInScene.x;
            z = z / _sizeInScene.y;

            int ew = elevationDataWidth - 1;
            int eh = elevationDataHeight - 1;

            if (x < 0) x = 0;
            else if (x > 1) x = 1;

            if (z < 0) z = 0;
            else if (z > 1) z = 1;

            double cx = rect.width * x + rect.left;
            double cz = rect.height * z + rect.top;

            float rx = (float)((cx - dataRect.left) / dataRect.width * ew);
            float ry = (float)((cz - dataRect.top) / dataRect.height * eh);

            if (rx < 0) rx = 0;
            else if (rx > ew) rx = ew;

            if (ry < 0) ry = 0;
            else if (ry > eh) ry = eh;

            int x1 = (int)rx;
            int x2 = x1 + 1;
            int y1 = (int)ry;
            int y2 = y1 + 1;
            if (x2 > ew) x2 = ew;
            if (y2 > eh) y2 = eh;

            float p1 = (elevationData[x2, eh - y1] - elevationData[x1, eh - y1]) * (rx - x1) + elevationData[x1, eh - y1];
            float p2 = (elevationData[x2, eh - y2] - elevationData[x1, eh - y2]) * (rx - x1) + elevationData[x1, eh - y2];

            float v = (p2 - p1) * (ry - y1) + p1;
            if (!tweenStarted || prevData == null) return v;

            float pv = GetPrevUnscaledElevation(x, z, rect);
            return pv > float.MinValue ? Mathf.Lerp(pv, v, tweenProgress) : v;
        }

        private float GetPrevUnscaledElevation(double x, double z, GeoRect rect)
        {
            int ew = elevationDataWidth - 1;
            int eh = elevationDataHeight - 1;

            double cx = rect.width * x + rect.left;
            double cz = rect.height * z + rect.top;

            float rx = (float)((cx - prevRect.left) / prevRect.width * ew);
            float ry = (float)((cz - prevRect.top) / prevRect.height * eh);

            if (rx < 0) rx = 0;
            else if (rx > ew) rx = ew;

            if (ry < 0) ry = 0;
            else if (ry > eh) ry = eh;

            int x1 = (int)rx;
            int x2 = x1 + 1;
            int y1 = (int)ry;
            int y2 = y1 + 1;
            if (x2 > ew) x2 = ew;
            if (y2 > eh) y2 = eh;

            float p1 = (prevData[x2, eh - y1] - prevData[x1, eh - y1]) * (rx - x1) + prevData[x1, eh - y1];
            float p2 = (prevData[x2, eh - y2] - prevData[x1, eh - y2]) * (rx - x1) + prevData[x1, eh - y2];

            return (p2 - p1) * (ry - y1) + p1;
        }

        protected void SavePrevValues()
        {
            if (!tweenUpdateValues) return;

            prevRect = dataRect;
            tweenProgress = 0;
            if (elevationData != null)
            {
                if (prevData == null || prevData.GetLength(0) != elevationData.GetLength(0) || prevData.GetLength(1) != elevationData.GetLength(1)) prevData = new short[elevationData.GetLength(0), elevationData.GetLength(1)];
                for (int i = 0; i < prevData.GetLength(0); i++)
                {
                    for (int j = 0; j < prevData.GetLength(1); j++)
                    {
                        prevData[i, j] = elevationData[i, j];
                    }
                }

                tweenStarted = true;
            }
        }

        public override void SetElevationData(short[,] data)
        {
            SavePrevValues();

            elevationData = data;
            dataRect = requestRect;
            elevationDataWidth = data.GetLength(0);
            elevationDataHeight = data.GetLength(1);

            UpdateMinMax();

            waitSetElevationData = false;

            if (OnElevationUpdated != null) OnElevationUpdated();
            map.Redraw();
        }

        protected override void Update()
        {
            if (tweenStarted)
            {
                tweenProgress += Time.deltaTime / tweenDuration;
                if (tweenProgress >= 1)
                {
                    tweenProgress = 1;
                    tweenStarted = false;
                    if (OnElevationUpdated != null) OnElevationUpdated();
                }

                map.Redraw();
            }

            if (!zoomRange.Contains(map.buffer.lastState.zoom)) return;
            if (elevationBufferPosition == bufferPosition) return;

            RequestNewElevationData();
        }

        protected override void UpdateMinMax()
        {
            minValue = short.MaxValue;
            maxValue = short.MinValue;

            if (!hasData) return;

            int s1 = elevationData.GetLength(0);
            int s2 = elevationData.GetLength(1);

            for (int i = 0; i < s1; i++)
            {
                for (int j = 0; j < s2; j++)
                {
                    short v = elevationData[i, j];
                    if (v < minValue) minValue = v;
                    if (v > maxValue) maxValue = v;
                }
            }
        }
    }
}