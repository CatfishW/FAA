/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEngine;
using UnityEngine.Rendering;

namespace OnlineMaps
{
    public static class RenderPipelineHelper
    {
        public static RenderPipelineAsset renderPipeline
        {
            get
            {
#if UNITY_6000_0_OR_NEWER
                return GraphicsSettings.defaultRenderPipeline;
#else
                return GraphicsSettings.renderPipelineAsset;
#endif
            }
        }

        public static bool isHDRP => renderPipeline && renderPipeline.GetType().Name.Contains("HDRenderPipelineAsset");
        public static bool isURP => renderPipeline && renderPipeline.GetType().Name.Contains("UniversalRenderPipelineAsset");
        
        public static Shader GetDefaultShader()
        {
            if (!renderPipeline) return Shader.Find("Standard");
            return Shader.Find(isHDRP ? "HDRenderPipeline/Lit" : "Universal Render Pipeline/Lit");
        }

        public static Shader GetTransparentShader()
        {
            return !renderPipeline ? Shader.Find("Unlit/Transparent") : GetUnlitShader();
        }

        public static Shader GetUnlitShader()
        {
            if (!renderPipeline) return Shader.Find("Unlit/Texture");
            return Shader.Find(isHDRP ? "HDRenderPipeline/Unlit" : "Universal Render Pipeline/Unlit");
        }
    }
}