using UnityEngine;
using UnityEngine.PostProcessing;
using UnityEditor.ProjectWindowCallback;
using System.IO;

namespace UnityEditor.PostProcessing
{
    public class PostProcessingFactory
    {
        [MenuItem("Assets/Create/Post-Processing Profile", priority = 201)]
        static void MenuCreatePostProcessingProfile()
        {
            var icon = EditorGUIUtility.FindTexture("ScriptableObject Icon");
#if UNITY_6000_2_OR_NEWER
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
                EntityId.FromULong(0),
                ScriptableObject.CreateInstance<DoCreatePostProcessingProfile>(),
                "New Post-Processing Profile.asset",
                icon,
                null);
#else
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, ScriptableObject.CreateInstance<DoCreatePostProcessingProfile>(), "New Post-Processing Profile.asset", icon, null);
#endif
        }

        internal static PostProcessingProfile CreatePostProcessingProfileAtPath(string path)
        {
            var profile = ScriptableObject.CreateInstance<PostProcessingProfile>();
            profile.name = Path.GetFileName(path);
            profile.fog.enabled = true;
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }
    }

#if UNITY_6000_2_OR_NEWER
    class DoCreatePostProcessingProfile : AssetCreationEndAction
    {
        public override void Action(EntityId entityId, string pathName, string resourceFile)
        {
            PostProcessingProfile profile = PostProcessingFactory.CreatePostProcessingProfileAtPath(pathName);
            ProjectWindowUtil.ShowCreatedAsset(profile);
        }
    }
#else
    class DoCreatePostProcessingProfile : EndNameEditAction
    {
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            PostProcessingProfile profile = PostProcessingFactory.CreatePostProcessingProfileAtPath(pathName);
            ProjectWindowUtil.ShowCreatedAsset(profile);
        }
    }
#endif
}
