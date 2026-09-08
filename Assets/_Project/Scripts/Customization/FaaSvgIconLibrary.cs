using System;
using UnityEngine;

namespace FAA.Customization
{
    public enum FaaRadarIcon { Lines, Map, Range, Target, Center, Expand, Restore }

    /// <summary>Editor-tessellated SVGs; no bitmap atlas or runtime XML parsing.</summary>
    public sealed class FaaSvgIconLibrary : ScriptableObject
    {
        public const string ResourcePath = "HudIcons/FaaRadarIconLibrary";
        [Serializable]
        public sealed class Entry
        {
            public FaaRadarIcon icon;
            public Vector2[] vertices;
            public int[] triangles;
        }

        public Entry[] entries = Array.Empty<Entry>();
        public Entry Find(FaaRadarIcon icon) => Array.Find(entries, entry => entry.icon == icon);
    }
}
