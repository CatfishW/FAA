/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEditor;
using UnityEngine;

namespace OnlineMaps.Editors
{
	public class ResourcesImporter : AssetPostprocessor
	{  
		void OnPreprocessTexture()
		{
			if (!assetPath.Contains("Resources/OnlineMapsTiles")) return;
			
			TextureImporter textureImporter = assetImporter as TextureImporter;
			textureImporter.mipmapEnabled = false;
			textureImporter.isReadable = true;
			textureImporter.wrapMode = TextureWrapMode.Clamp;
			textureImporter.maxTextureSize = 256;
		}
	}
}