/*         INFINITY CODE         */
/*   https://infinity-code.com   */

namespace OnlineMaps
{
    /// <summary>
    /// Provides a collection of tile sources for map providers.
    /// </summary>
    public static class TileSources
    {
        private const string SATELLITE = "Satellite";
        private const string RELIEF = "Relief";
        private const string TERRAIN = "Terrain";
        private const string MAP = "Map";

        private static TileProvider[] _providers;

        /// <summary>
        /// Gets the array of tile providers.
        /// </summary>
        public static TileProvider[] providers
        {
            get
            {
                if (_providers != null) return _providers;

                _providers = new[]
                {
                    arcGIS,
                    cartoDB,
                    digitalGlobe,
                    google,
                    mapbox,
                    mapboxClassic,
                    mapQuest,
                    maptiler,
                    mapy,
                    nationalmap,
                    nokia,
                    openStreetMap,
                    openTopoMap,
                    openWeatherMap,
                    stamen,
                    thunderforest,
                    tianDiTu,
                    tomtom,
                    virtualEarth,
                    other,
                    custom
                };

                return _providers;
            }
        }

        /// <summary>
        /// ArcGIS (Esri) tile provider.
        /// </summary>
        public static TileProvider arcGIS { get; } = new TileProvider("arcgis", "ArcGIS (Esri)")
        {
            url = "https://server.arcgisonline.com/ArcGIS/rest/services/{variant}/MapServer/tile/{zoom}/{y}/{x}",
            types = new[]
            {
                new MapType("WorldImagery") { variantWithoutLabels = "World_Imagery" },
                new MapType("WorldTopoMap") { variantWithLabels = "World_Topo_Map" },
                new MapType("WorldStreetMap") { variantWithLabels = "World_Street_Map" },
                new MapType("WorldTerrain") { variantWithoutLabels = "World_Terrain_Base" },
                new MapType("WorldShadedRelief") { variantWithoutLabels = "World_Shaded_Relief" },
                new MapType("WorldPhysical") { variantWithoutLabels = "World_Physical_Map" },
                new MapType("OceanBasemap") { variantWithLabels = "Ocean_Basemap" },
                new MapType("NatGeoWorldMap") { variantWithLabels = "NatGeo_World_Map" },
                new MapType("WorldGrayCanvas") { variantWithLabels = "Canvas/World_Light_Gray_Base" },
            }
        };

        /// <summary>
        /// CartoDB tile provider.
        /// </summary>
        public static TileProvider cartoDB { get; } = new TileProvider("CartoDB")
        {
            url = "https://cartodb-basemaps-d.global.ssl.fastly.net/{variant}/{z}/{x}/{y}.png",
            types = new[]
            {
                new MapType("Positron")
                {
                    variantWithLabels = "light_all",
                    variantWithoutLabels = "light_nolabels"
                },
                new MapType("DarkMatter")
                {
                    variantWithLabels = "dark_all",
                    variantWithoutLabels = "dark_nolabels"
                },
            }
        };

        /// <summary>
        /// Custom tile provider.
        /// </summary>
        public static TileProvider custom { get; } = new TileProvider("Custom")
        {
            types = new[]
            {
                new MapType("Custom") { isCustom = true }
            }
        };

        /// <summary>
        /// DigitalGlobe tile provider.
        /// </summary>
        public static TileProvider digitalGlobe { get; } = new TileProvider("DigitalGlobe")
        {
            url = "https://a.tiles.mapbox.com/v4/digitalglobe.{variant}/{zoom}/{x}/{y}.jpg?access_token={accesstoken}",
            types = new[]
            {
                new MapType("Satellite")
                {
                    variantWithoutLabels = "nal0g75k"
                },
                new MapType("Street")
                {
                    variantWithLabels = "nako6329",
                },
                new MapType("Terrain")
                {
                    variantWithLabels = "nako1fhg",
                },
            },
            extraFields = new TileProvider.IExtraField[]
            {
                new TileProvider.ExtraField("Access Token", "accesstoken"),
            }
        };

        /// <summary>
        /// Google Maps tile provider.
        /// </summary>
        public static TileProvider google { get; } = new TileProvider("google", "Google Maps")
        {
            hasLanguage = true,
            types = new[]
            {
                new MapType(SATELLITE)
                {
                    urlWithLabels = "https://mt{rnd0-3}.googleapis.com/vt/lyrs=y&hl={lng}&x={x}&y={y}&z={zoom}",
                    urlWithoutLabels = "https://khm{rnd0-3}.googleapis.com/kh?v={version}&hl={lng}&x={x}&y={y}&z={zoom}",
                    extraFields = new TileProvider.IExtraField[]
                    {
                        new TileProvider.ExtraField("Tile version", "version", "995")
                    }
                },
                new MapType(RELIEF)
                {
                    urlWithLabels = "https://mts{rnd0-3}.google.com/vt/lyrs=t@131,r@216000000&src=app&hl={lng}&x={x}&y={y}&z={zoom}&s="
                },
                new MapType(TERRAIN)
                {
                    urlWithLabels = "https://mt{rnd0-3}.googleapis.com/vt?pb=!1m4!1m3!1i{zoom}!2i{x}!3i{y}!2m3!1e0!2sm!3i295124088!3m9!2s{lng}!3s{region}!5e18!12m1!1e47!12m3!1e37!2m1!1ssmartmaps!4e0",
                    extraFields = new TileProvider.IExtraField[]
                    {
                        new TileProvider.ExtraField("Region", "region", "US"),
                    }
                }
            }
        };

        /// <summary>
        /// Mapbox tile provider.
        /// </summary>
        public static TileProvider mapbox { get; } = new TileProvider("Mapbox")
        {
            labelsEnabled = false,

            types = new[]
            {
                new MapType("Map")
                {
                    urlWithLabels = "https://api.mapbox.com/styles/v1/{userid}/{mapid}/tiles/256/{z}/{x}/{y}?events=true&access_token={accesstoken}",
                    extraFields = new TileProvider.IExtraField[]
                    {
                        new TileProvider.ExtraField("User ID", "userid"),
                        new TileProvider.ExtraField("Map ID", "mapid"),
                    }
                },
                new MapType("Satellite")
                {
                    urlWithoutLabels = "https://api.mapbox.com/v4/mapbox.satellite/{z}/{x}/{y}.png?events=true&access_token={accesstoken}"
                }
            },

            extraFields = new TileProvider.IExtraField[]
            {
                new TileProvider.ExtraField("Access Token", "accesstoken"),
            }
        };

        /// <summary>
        /// Mapbox Classic tile provider.
        /// </summary>
        public static TileProvider mapboxClassic { get; } = new TileProvider("Mapbox classic")
        {
            url = "https://b.tiles.mapbox.com/v4/{mapid}/{zoom}/{x}/{y}.png?events=true&access_token={accesstoken}",
            labelsEnabled = true,

            types = new[]
            {
                new MapType("Map"),
            },

            extraFields = new TileProvider.IExtraField[]
            {
                new TileProvider.ExtraField("Map ID", "mapid"),
                new TileProvider.ExtraField("Access Token", "accesstoken"),
            }
        };

        /// <summary>
        /// MapQuest tile provider.
        /// </summary>
        public static TileProvider mapQuest { get; } = new TileProvider("MapQuest")
        {
            url = "https://a.tiles.mapbox.com/v4/{variant}/{zoom}/{x}/{y}.png?access_token={accesstoken}",
            types = new[]
            {
                new MapType(SATELLITE) { variantWithoutLabels = "mapquest.satellite" },
                new MapType("Streets") { variantWithLabels = "mapquest.streets" },
            },
            extraFields = new TileProvider.IExtraField[]
            {
                new TileProvider.ExtraField("Access Token", "accesstoken")
            },
        };

        /// <summary>
        /// Mapy.CZ tile provider.
        /// </summary>
        public static TileProvider mapy { get; } = new TileProvider("mapy", "Mapy.CZ")
        {
            url = "https://m{rnd0-4}.mapserver.mapy.cz/{variant}/{zoom}-{x}-{y}",
            types = new[]
            {
                new MapType(SATELLITE) { variantWithoutLabels = "ophoto-m" },
                new MapType("Travel") { variantWithLabels = "wturist-m" },
                new MapType("Winter") { variantWithLabels = "wturist_winter-m" },
                new MapType("Geographic") { variantWithLabels = "zemepis-m" },
                new MapType("Summer") { variantWithLabels = "turist_aquatic-m" },
                new MapType("19century", "19th century") { variantWithLabels = "army2-m" },
            }
        };

        public static TileProvider maptiler { get; } = new TileProvider("maptiler", "MapTiler")
        {
            url = "https://api.maptiler.com/maps/{variant}/256/{z}/{x}/{y}.{ext}?key={apikey}",
            ext = "png",
            types = new[]
            {
                new MapType("Aquarelle") { variantWithoutLabels = "aquarelle", },
                new MapType("Backdrop") { variantWithLabels = "backdrop", },
                new MapType("Basic") { variantWithLabels = "basic-v2", },
                new MapType("Bright") { variantWithLabels = "bright-v2", },
                new MapType("Dataviz") { variantWithLabels = "dataviz", },
                new MapType("Landscape") { variantWithLabels = "landscape", },
                new MapType("Ocean") { variantWithLabels = "ocean", },
                new MapType("OpenStreetMap")
                {
                    variantWithoutLabels = "openstreetmap",
                    ext = "jpg",
                },
                new MapType("Outdoor") { variantWithLabels = "outdoor-v2", },
                new MapType(SATELLITE)
                {
                    variantWithoutLabels = "satellite",
                    ext = "jpg",
                },
                new MapType("Streets") { variantWithLabels = "streets-v2", },
                new MapType("Toner") { variantWithLabels = "toner-v2", },
                new MapType("Topo") { variantWithLabels = "topo-v2", },
                new MapType("Winter") { variantWithLabels = "winter-v2", },
            },
            extraFields = new TileProvider.IExtraField[]
            {
                new TileProvider.ExtraField("API key", "apikey")
            }
        };

        /// <summary>
        /// National Map tile provider.
        /// </summary>
        public static TileProvider nationalmap { get; } = new TileProvider("nationalmap", "National Map")
        {
            url = "https://basemap.nationalmap.gov/arcgis/rest/services/{variant}/MapServer/tile/{z}/{y}/{x}",
            types = new[]
            {
                new MapType("USGSHydroCached")
                {
                    variantWithLabels = "USGSHydroCached"
                },
                new MapType("USGSImagery")
                {
                    variantWithoutLabels = "USGSImageryOnly",
                    variantWithLabels = "USGSImageryTopo"
                },
                new MapType("USGSShadedReliefOnly")
                {
                    variantWithoutLabels = "USGSShadedReliefOnly"
                },
                new MapType("USGSTopo")
                {
                    variantWithLabels = "USGSTopo"
                }
            }
        };

        /// <summary>
        /// Nokia Maps tile provider.
        /// </summary>
        public static TileProvider nokia { get; } = new TileProvider("nokia", "Nokia Maps (here.com)")
        {
            url = "https://{rnd1-4}.{prop2}.maps.ls.hereapi.com/maptile/2.1/{prop}/newest/{variant}/{zoom}/{x}/{y}/256/png8?lg={lng}&{auth}",
            twoLetterLanguage = false,
            hasLanguage = true,
            labelsEnabled = true,
            prop = "maptile",
            prop2 = "base",
            OnReplaceToken = delegate(MapType type, string token)
            {
                if (token != "auth") return null;

                string api = "apikey";
                if (type.TryUseExtraFields(ref api) && !string.IsNullOrEmpty(api)) return "apiKey=" + api;

                string id = "appid", code = "appcode";
                type.TryUseExtraFields(ref id);
                type.TryUseExtraFields(ref code);
                return "app_id=" + id + "&app_code=" + code;
            },

            types = new[]
            {
                new MapType(SATELLITE)
                {
                    variantWithLabels = "hybrid.day",
                    variantWithoutLabels = "satellite.day",
                    prop2 = "aerial",
                },
                new MapType(TERRAIN)
                {
                    variant = "terrain.day",
                    propWithoutLabels = "basetile",
                    prop2 = "aerial",
                },
                new MapType(MAP)
                {
                    variant = "normal.day",
                    propWithoutLabels = "basetile",
                },
                new MapType("normalDayCustom")
                {
                    variant = "normal.day.custom",
                    propWithoutLabels = "basetile",
                },
                new MapType("normalDayGrey")
                {
                    variant = "normal.day.grey",
                    propWithoutLabels = "basetile",
                },
                new MapType("normalDayMobile")
                {
                    variant = "normal.day.mobile",
                    propWithoutLabels = "basetile",
                },
                new MapType("normalDayGreyMobile")
                {
                    variant = "normal.day.grey.mobile",
                    propWithoutLabels = "basetile",
                },
                new MapType("normalDayTransit")
                {
                    variant = "normal.day.transit",
                    propWithoutLabels = "basetile",
                },
                new MapType("normalDayTransitMobile")
                {
                    variant = "normal.day.transit.mobile",
                    propWithoutLabels = "basetile",
                },
                new MapType("normalNight")
                {
                    variant = "normal.night",
                    propWithoutLabels = "basetile",
                },
                new MapType("normalNightMobile")
                {
                    variant = "normal.night.mobile",
                    propWithoutLabels = "basetile",
                },
                new MapType("normalNightGrey")
                {
                    variant = "normal.night.grey",
                    propWithoutLabels = "basetile",
                },
                new MapType("normalNightGreyMobile")
                {
                    variant = "normal.night.grey.mobile",
                    propWithoutLabels = "basetile",
                },
                new MapType("pedestrianDay")
                {
                    variantWithLabels = "pedestrian.day"
                },
                new MapType("pedestrianNight")
                {
                    variantWithLabels = "pedestrian.night"
                },
            },

            extraFields = new TileProvider.IExtraField[]
            {
                new TileProvider.ExtraField("Api Key", "apikey", ""),
                new TileProvider.LabelField("- or -"),
                new TileProvider.ExtraField("App ID", "appid", ""),
                new TileProvider.ExtraField("App Code", "appcode", ""),
            }
        };

        /// <summary>
        /// OpenStreetMap tile provider.
        /// </summary>
        public static TileProvider openStreetMap { get; } = new TileProvider("osm", "OpenStreetMap")
        {
            types = new[]
            {
                new MapType("Mapnik") { urlWithLabels = "https://a.tile.openstreetmap.org/{zoom}/{x}/{y}.png" },
                new MapType("DE") { urlWithLabels = "https://a.tile.openstreetmap.de/tiles/osmde/{zoom}/{x}/{y}.png" },
                new MapType("France") { urlWithLabels = "https://a.tile.openstreetmap.fr/osmfr/{zoom}/{x}/{y}.png" },
                new MapType("HOT") { urlWithLabels = "https://a.tile.openstreetmap.fr/hot/{zoom}/{x}/{y}.png" },
            }
        };

        /// <summary>
        /// OpenTopoMap tile provider.
        /// </summary>
        public static TileProvider openTopoMap { get; } = new TileProvider("OpenTopoMap")
        {
            types = new[]
            {
                new MapType("OpenTopoMap") { urlWithLabels = "https://a.tile.opentopomap.org/{z}/{x}/{y}.png" },
            }
        };

        /// <summary>
        /// OpenWeatherMap tile provider.
        /// </summary>
        public static TileProvider openWeatherMap { get; } = new TileProvider("OpenWeatherMap")
        {
            url = "https://tile.openweathermap.org/map/{variant}/{z}/{x}/{y}.png?appid={apikey}",
            types = new[]
            {
                new MapType("Clouds") { variantWithoutLabels = "clouds" },
                new MapType("CloudsClassic") { variantWithoutLabels = "clouds_cls" },
                new MapType("Precipitation") { variantWithoutLabels = "precipitation" },
                new MapType("PrecipitationClassic") { variantWithoutLabels = "precipitation_cls" },
                new MapType("Rain") { variantWithoutLabels = "rain" },
                new MapType("RainClassic") { variantWithoutLabels = "rain_cls" },
                new MapType("Pressure") { variantWithoutLabels = "pressure" },
                new MapType("PressureContour") { variantWithoutLabels = "pressure_cntr" },
                new MapType("Wind") { variantWithoutLabels = "wind" },
                new MapType("Temperature") { variantWithoutLabels = "temp" },
                new MapType("Snow") { variantWithoutLabels = "snow" },
            },
            extraFields = new TileProvider.IExtraField[]
            {
                new TileProvider.ExtraField("API key", "apikey"),
            }
        };

        /// <summary>
        /// Stamen tile provider.
        /// </summary>
        public static TileProvider stamen { get; } = new TileProvider("Stamen")
        {
            url = "https://stamen-tiles-a.a.ssl.fastly.net/{variant}/{z}/{x}/{y}.png",
            types = new[]
            {
                new MapType("Toner") { variantWithLabels = "toner" },
                new MapType("TonerBackground") { variantWithoutLabels = "toner-background" },
                new MapType("TonerHybrid") { variantWithLabels = "toner-hybrid" },
                new MapType("TonerLines") { variantWithLabels = "toner-lines" },
                new MapType("TonerLabels") { variantWithLabels = "toner-labels" },
                new MapType("TonerLite") { variantWithLabels = "toner-lite" },
                new MapType("Watercolor") { variantWithoutLabels = "watercolor" },
            }
        };

        /// <summary>
        /// Thunderforest tile provider.
        /// </summary>
        public static TileProvider thunderforest { get; } = new TileProvider("Thunderforest")
        {
            url = "https://a.tile.thunderforest.com/{variant}/{z}/{x}/{y}.png?apikey={apikey}",
            types = new[]
            {
                new MapType("OpenCycleMap") { variantWithLabels = "cycle" },
                new MapType("Transport") { variantWithLabels = "transport" },
                new MapType("TransportDark") { variantWithLabels = "transport-dark" },
                new MapType("SpinalMap") { variantWithLabels = "spinal-map" },
                new MapType("Landscape") { variantWithLabels = "landscape" },
                new MapType("Outdoors") { variantWithLabels = "outdoors" },
                new MapType("Pioneer") { variantWithLabels = "pioneer" },
            },

            extraFields = new TileProvider.IExtraField[]
            {
                new TileProvider.ExtraField("API key", "apikey")
            }
        };

        /// <summary>
        /// TianDiTu tile provider.
        /// </summary>
        public static TileProvider tianDiTu { get; } = new TileProvider("TianDiTu")
        {
            types = new[]
            {
                new MapType("Normal")
                {
                    urlWithoutLabels = "https://t{rnd0-7}.tianditu.gov.cn/DataServer?T=vec_w&x={x}&y={y}&l={z}&tk={apikey}"
                },
                new MapType(SATELLITE)
                {
                    urlWithoutLabels = "https://t{rnd0-7}.tianditu.gov.cn/img_w/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=img&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles&TILECOL={x}&TILEROW={y}&TILEMATRIX={z}&tk={apikey}"
                },
                new MapType(TERRAIN)
                {
                    urlWithoutLabels = "https://t{rnd0-7}.tianditu.gov.cn/DataServer?T=ter_w&x={x}&y={y}&l={z}&tk={apikey}"
                },
            },

            extraFields = new TileProvider.IExtraField[]
            {
                new TileProvider.ExtraField("API key", "apikey")
            },
        };

        public static TileProvider tomtom { get; } = new TileProvider("TomTom")
        {
            url = "https://api.tomtom.com/map/1/tile/{variant}/{prop}/{zoom}/{X}/{Y}.{ext}?key={apikey}",
            prop = "main",
            ext = "png",
            types = new[]
            {
                new MapType("Map") { variantWithLabels = "basic" },
                new MapType("Map Night")
                {
                    variantWithLabels = "basic",
                    propWithLabels = "night"
                },
                new MapType(SATELLITE)
                {
                    variantWithoutLabels = "sat",
                    ext = "jpg"
                },
                new MapType("Hillshade")
                {
                    variantWithoutLabels = "sat",
                    ext = "jpg"
                },
            },

            extraFields = new TileProvider.IExtraField[]
            {
                new TileProvider.ExtraField("API key", "apikey")
            }
        };

        /// <summary>
        /// Virtual Earth (Bing Maps) tile provider.
        /// </summary>
        public static TileProvider virtualEarth { get; } = new TileProvider("virtualearth", "Virtual Earth (Bing Maps)")
        {
            hasLanguage = true,
            types = new[]
            {
                new MapType("Aerial")
                {
                    urlWithoutLabels = "https://t{rnd0-4}.ssl.ak.tiles.virtualearth.net/tiles/a{quad}.jpeg?mkt={lng}&g=1457&n=z",
                    urlWithLabels = "https://t{rnd0-4}.ssl.ak.dynamic.tiles.virtualearth.net/comp/ch/{quad}?mkt={lng}&it=A,G,L,LA&og=30&n=z"
                },
                new MapType("Road")
                {
                    urlWithLabels = "https://t{rnd0-4}.ssl.ak.dynamic.tiles.virtualearth.net/comp/ch/{quad}?mkt={lng}&it=G,VE,BX,L,LA&og=30&n=z"
                }
            }
        };

        /// <summary>
        /// Other tile providers.
        /// </summary>
        public static TileProvider other { get; } = new TileProvider("Other")
        {
            types = new[]
            {
                new MapType("AMap Satellite") { urlWithoutLabels = "https://webst02.is.autonavi.com/appmaptile?style=6&x={x}&y={y}&z={zoom}" },
                new MapType("AMap Terrain") { urlWithLabels = "https://webrd03.is.autonavi.com/appmaptile?lang=zh_cn&size=1&scale=1&style=8&x={x}&y={y}&z={zoom}" },
                new MapType("MtbMap") { urlWithLabels = "https://tile.mtbmap.cz/mtbmap_tiles/{z}/{x}/{y}.png" },
                new MapType("HikeBike") { urlWithLabels = "https://a.tiles.wmflabs.org/hikebike/{z}/{x}/{y}.png" },
                new MapType("Waze") { urlWithLabels = "https://worldtiles{rnd1-4}.waze.com/tiles/{z}/{x}/{y}.png" },
            }
        };
    }
}