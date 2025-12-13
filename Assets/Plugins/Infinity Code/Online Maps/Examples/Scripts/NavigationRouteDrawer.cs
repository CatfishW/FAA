using System.Collections.Generic;
using OnlineMaps;
using OnlineMaps.Webservices;
using UnityEngine;

namespace OnlineMapsDemos
{
    public class NavigationRouteDrawer : MonoBehaviour
    {
        private Navigation navigation;

        private List<GeoPoint> remainPoints;
        private List<GeoPoint> coveredPoints;
        private Line routeLine;
        private Line coveredLine;

        private DrawingElementManager drawingElementManager => navigation.control.drawingElementManager;

        public void InitCoveredPoints()
        {
            coveredPoints = new List<GeoPoint>(remainPoints.Count);
            coveredLine = new Line(coveredPoints, Color.gray, 3);
            drawingElementManager.Add(coveredLine);
        }

        private void OnEnable()
        {
            navigation = GetComponent<Navigation>();
        }

        public void RemoveLines()
        {
            // Remove covered and remain lines.
            drawingElementManager.Remove(routeLine);
            routeLine = null;

            if (coveredLine != null)
            {
                drawingElementManager.Remove(coveredLine);
                coveredLine = null;
            }
        }

        public void SetRemainPoints(List<GeoPoint> points)
        {
            remainPoints = points;

            // Create a line and add it to the map
            if (routeLine == null)
            {
                routeLine = new Line(remainPoints, Color.green, 3);
                drawingElementManager.Add(routeLine);
            }
            else routeLine.SetPoints(remainPoints);
        }

        /// <summary>
        /// Updates covered and remain lines
        /// </summary>
        public void UpdateLines()
        {
            // Clears line points.
            coveredPoints.Clear();
            remainPoints.Clear();

            // Iterate all steps.
            GoogleRoutingResult.RouteLegStep[] steps = navigation.steps;
            int currentStepIndex = navigation.currentStepIndex;

            for (int i = 0; i < steps.Length; i++)
            {
                // Get a polyline
                var step = steps[i];
                GeoPoint[] polyline = step.polyline.points;

                // Iterate all points of polyline
                for (int j = 0; j < polyline.Length; j++)
                {
                    GeoPoint p = polyline[j];

                    // If index of step less than current step, add to covered list
                    // If index of step greater than current step, add to remain list
                    // If this is current step, points than less current point add to covered list, otherwise add to remain list
                    if (i < currentStepIndex)
                    {
                        coveredPoints.Add(p);
                    }
                    else if (i > currentStepIndex)
                    {
                        remainPoints.Add(p);
                    }
                    else
                    {
                        if (j < navigation.pointIndex)
                        {
                            coveredPoints.Add(p);
                        }
                        else if (j > navigation.pointIndex)
                        {
                            remainPoints.Add(p);
                        }
                        else
                        {
                            coveredPoints.Add(p);
                            coveredPoints.Add(navigation.lastPointOnRoute);
                            remainPoints.Add(navigation.lastPointOnRoute);
                        }
                    }
                }
            }
            
            // Update lines
            routeLine.SetPoints(remainPoints);
            coveredLine.SetPoints(coveredPoints);
        }
    }
}