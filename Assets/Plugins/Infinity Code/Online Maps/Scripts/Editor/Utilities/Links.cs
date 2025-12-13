/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEngine;

namespace OnlineMaps.Editors
{
    public static class Links
    {
        private const string aid = "?aid=1100liByC";
        private const string api = "https://infinity-code.com/documentation/online-maps-v4.html#api";
        private const string assetStore = "https://assetstore.unity.com/packages/slug/297114";
        private const string atlasOfExamples = "https://infinity-code.com/atlas/online-maps-v4";
        private const string changelog = "https://infinity-code.com/products_update/get-changelog.php?asset=Online%20Maps&from=4.0";
        private const string documentation = "https://infinity-code.com/documentation/online-maps-v4.html";
        private const string forum = "https://forum.infinity-code.com";
        private const string homepage = "https://infinity-code.com/assets/online-maps";
        private const string reviews = assetStore + "/reviews";
        private const string support = "mailto:support@infinity-code.com?subject=Online%20Maps";
        private const string youtube = "https://www.youtube.com/channel/UCxCID3jp7RXKGqiCGpjPuOg";

        public static void Open(string url)
        {
            Application.OpenURL(url);
        }

        public static void OpenAPIReference()
        {
            Open(api);
        }

        public static void OpenAssetStore()
        {
            Open(assetStore + aid);
        }

        public static void OpenAtlasOfExamples()
        {
            Open(atlasOfExamples);
        }

        public static void OpenChangelog()
        {
            Open(changelog);
        }

        public static void OpenDocumentation()
        {
            OpenDocumentation(null);
        }

        public static void OpenDocumentation(string anchor)
        {
            string url = documentation;
            if (!string.IsNullOrEmpty(anchor)) url += "#" + anchor;
            Open(url);
        }

        public static void OpenForum()
        {
            Open(forum);
        }

        public static void OpenHomepage()
        {
            Open(homepage);
        }

        public static void OpenLocalDocumentation()
        {
            string url = EditorUtils.assetPath + "Documentation/Content/Documentation-Content.html";
            Application.OpenURL(url);
        }

        public static void OpenReviews()
        {
            Open(reviews + aid);
        }

        public static void OpenSupport()
        {
            Open(support);
        }

        public static void OpenYouTube()
        {
            Open(youtube);
        }
    }
}