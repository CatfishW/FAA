using System.Collections.Generic;
using System.Text;
using TrafficRadar;
using UnityEngine;

namespace FAA.XPlaneIntegration
{
    /// <summary>Categories from reported ICAO metadata, never speed/callsign guesses.</summary>
    public static class XPlaneTrafficTypeCatalog
    {
        public const string IcaoBytes = "sim/cockpit2/tcas/targets/icao_type";

        public static string ReadNativeIcao(IDictionary<string, float> traffic, int slot)
        {
            if (traffic == null || slot < 1 || slot > 19) return string.Empty;
            var code = new StringBuilder(7);
            for (int i = 0; i < 8; i++)
            {
                if (!traffic.TryGetValue(IcaoBytes + "[" + (slot * 8 + i) + "]", out float value) ||
                    float.IsNaN(value) || float.IsInfinity(value) || value != Mathf.Floor(value)) return string.Empty;
                if (value == 0) return code.Length >= 2 ? code.ToString() : string.Empty;
                if (!((value >= 'A' && value <= 'Z') || (value >= '0' && value <= '9'))) return string.Empty;
                code.Append((char)(int)value);
            }
            return string.Empty; // An unterminated slot is not a valid identity.
        }

        public static TrafficRadarDataManager.AircraftType CategoryForIcao(string code)
        {
            switch (code)
            {
                case "S76": case "R22": case "R44": case "R66": case "B206": case "B407": case "H60":
                    return TrafficRadarDataManager.AircraftType.Helicopter;
                case "B738": case "MD82": case "A333": case "A320": case "B744":
                    return TrafficRadarDataManager.AircraftType.Commercial;
                case "F4": case "F14": case "F16": case "F18":
                    return TrafficRadarDataManager.AircraftType.Military;
                case "C172": case "BE58": case "PA18": case "RV10": case "BE9L":
                case "SF50": case "SR22": case "C750": case "EVOT": case "L5":
                    return TrafficRadarDataManager.AircraftType.General;
                default:
                    return TrafficRadarDataManager.AircraftType.Unknown;
            }
        }
    }
}
