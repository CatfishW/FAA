/*         INFINITY CODE         */
/*   https://infinity-code.com   */

namespace OnlineMaps
{
    public static class TrafficSources
    {
        private static TrafficProvider[] _providers;

        public static TrafficProvider[] providers
        {
            get
            {
                if (_providers != null) return _providers;
                
                _providers = new [] {
                    google,
                    here,
                    virtualEarth,
                    custom
                };

                return _providers;
            }
        }

        public static TrafficProvider custom { get; } = new TrafficProvider
        {
            id = "custom",
            title = "Custom",
            isCustom = true
        };

        public static TrafficProvider google { get; } = new TrafficProvider
        {
            id = "googlemaps",
            title = "Google Maps",
            url = "https://mts0.google.com/vt?pb=!1m4!1m3!1i{zoom}!2i{x}!3i{y}!2m3!1e0!2sm!3i301114286!2m6!1e2!2straffic!4m2!1soffset_polylines!2s0!5i1!2m12!1e2!2spsm!4m2!1sgid!2sl0t0vMkIqfb3hBb090479A!4m2!1ssp!2s1!5i1!8m2!13m1!14b1!3m25!2sru-RU!3sUS!5e18!12m1!1e50!12m3!1e37!2m1!1ssmartmaps!12m5!1e14!2m1!1ssolid!2m1!1soffset_polylines!12m4!1e52!2m2!1sentity_class!2s0S!12m4!1e26!2m2!1sstyles!2zcy5lOmx8cC52Om9mZixzLnQ6MXxwLnY6b2ZmLHMudDozfHAudjpvZmY!4e0"
        };

        public static TrafficProvider here { get; } = new TrafficProvider
        {
            id = "nokia",
            title = "Nokia Maps (here.com)",
            url = "https://1.traffic.maps.api.here.com/maptile/2.1/flowtile/newest/terrain.day/{zoom}/{x}/{y}/256/png8?app_id=xWVIueSv6JL0aJ5xqTxb&app_code=djPZyynKsbTjIUDOBcHZ2g&lg=rus&ppi=72&pview=RUS&tprof=PrtlHere"
        };

        public static TrafficProvider virtualEarth { get; } = new TrafficProvider
        {
            id = "virtualearth",
            title = "Virtual Earth (Bing Maps)",
            url = "https://t0-traffic.tiles.virtualearth.net/comp/ch/{quad}?it=Z,TF&L&n=z"
        };
    }
}