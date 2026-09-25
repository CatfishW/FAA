using System;
using System.IO;
using UnityEngine;

namespace FAA.XPlaneIntegration.Runtime
{
    /// <summary>Explicit local/operator endpoint configuration. Never discovers or uploads to a public elevation service.</summary>
    public static class XPlaneTerrainConnection
    {
        [Serializable] private sealed class Settings { public string terrainUrl; }
        public static bool TryNormalize(string value,out string endpoint)
        {
            endpoint=null;
            if(string.IsNullOrWhiteSpace(value)||value.Length>2048||!Uri.TryCreate(value.Trim(),UriKind.Absolute,out var uri))return false;
            if((uri.Scheme!=Uri.UriSchemeHttp&&uri.Scheme!=Uri.UriSchemeHttps)||string.IsNullOrEmpty(uri.Host)||
                !string.IsNullOrEmpty(uri.UserInfo)||!string.IsNullOrEmpty(uri.Query)||!string.IsNullOrEmpty(uri.Fragment))return false;
            endpoint=uri.AbsoluteUri.TrimEnd('/');return true;
        }
        public static bool TryResolve(string inspectorDefault,string configJson,string environment,string[] arguments,out string endpoint,out string error)
        {
            endpoint=null;error=null;string requested=inspectorDefault;
            if(!string.IsNullOrWhiteSpace(configJson))
            {
                if(configJson.Length>16384){error="Terrain configuration too large";return false;}
                try {var settings=JsonUtility.FromJson<Settings>(configJson);if(!string.IsNullOrWhiteSpace(settings?.terrainUrl))requested=settings.terrainUrl;}
                catch(ArgumentException){error="Invalid terrain configuration JSON";return false;}
            }
            if(!string.IsNullOrWhiteSpace(environment))requested=environment;
            if(arguments!=null)for(int i=0;i<arguments.Length;i++)
            {
                if(arguments[i]=="--terrain-url")
                {
                    if(i+1>=arguments.Length||arguments[i+1].StartsWith("--",StringComparison.Ordinal)){error="Missing --terrain-url value";return false;}
                    requested=arguments[++i];
                }
                else if(arguments[i].StartsWith("--terrain-url=",StringComparison.Ordinal))requested=arguments[i].Substring(14);
            }
            if(!TryNormalize(requested,out endpoint)){error="Terrain URL must be HTTP(S), without credentials, query or fragment";return false;}
            return true;
        }
        public static bool Load(string inspectorDefault,out string endpoint,out string error)
        {
            string file=Path.Combine(Application.streamingAssetsPath,"FAA","TerrainConnection.json");
            string json=null;
            try {if(File.Exists(file)){if(new FileInfo(file).Length>16384){endpoint=null;error="Terrain configuration too large";return false;}json=File.ReadAllText(file);}}
            catch(IOException){endpoint=null;error="Terrain configuration could not be read";return false;}
            return TryResolve(inspectorDefault,json,Environment.GetEnvironmentVariable("FAA_TERRAIN_URL"),Environment.GetCommandLineArgs(),out endpoint,out error);
        }
    }
}
