/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to handle interaction events with buildings.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "BuildingClickExample")]
    public class BuildingClickExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the building manager. If not specified, the current instance will be used.
        /// </summary>
        public Buildings buildings;

        /// <summary>
        /// This method is called when the script starts
        /// </summary>
        private void Start()
        {
            // If the building manager is not specified, get the current instance.
            if (!buildings && !(buildings = Buildings.instance))
            {
                Debug.LogError("Required Buildings component not found.");
                return;
            }
            
            // Subscribe to the building creation event
            buildings.OnBuildingCreated += OnBuildingCreated;
        }

        /// <summary>
        /// This method is called when click on building
        /// </summary>
        /// <param name="building">The building on which clicked</param>
        private void OnBuildingClick(BuildingBase building)
        {
            Debug.Log("click: " + building.id);
        }

        /// <summary>
        /// This method is called when each building is created
        /// </summary>
        /// <param name="building">The building that was created</param>
        private void OnBuildingCreated(BuildingBase building)
        {
            // Subscribe to interaction events
            building.OnPress += OnBuildingPress;
            building.OnRelease += OnBuildingRelease;
            building.OnClick += OnBuildingClick;
        }

        /// <summary>
        /// This method is called when press on building
        /// </summary>
        /// <param name="building">The building on which pressed</param>
        private void OnBuildingPress(BuildingBase building)
        {
            Debug.Log("Press: " + building.id);
        }

        /// <summary>
        /// This method is called when release on building
        /// </summary>
        /// <param name="building">The building on which released</param>
        private void OnBuildingRelease(BuildingBase building)
        {
            Debug.Log("Release: " + building.id);
        }
    }
}