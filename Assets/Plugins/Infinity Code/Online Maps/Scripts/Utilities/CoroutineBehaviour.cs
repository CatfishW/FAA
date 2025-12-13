/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Collections;
using UnityEngine;

namespace OnlineMaps
{
    public static class CoroutineBehaviour
    {
        private static MonoBehaviour container;

        public static MonoBehaviour Get()
        {
            if (Map.instance) return Map.instance;
            if (!container)
            {
                GameObject go = new GameObject("__OnlineMaps WebRequest__");
                go.hideFlags = HideFlags.HideInHierarchy;
                container = go.AddComponent<RequestBehaviour>();
            }

            return container;
        }

        public static Coroutine Start(IEnumerator routine)
        {
            return Get().StartCoroutine(routine);
        }

        public static void Stop(IEnumerator routine)
        {
            Get().StopCoroutine(routine);
        }
    }
}