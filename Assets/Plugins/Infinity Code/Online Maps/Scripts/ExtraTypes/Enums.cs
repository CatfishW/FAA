/*         INFINITY CODE         */
/*   https://infinity-code.com   */

namespace OnlineMaps
{
    /// <summary>
    /// Specifies the alignment options.
    /// </summary>
    public enum Align
    {
        /// <summary>
        /// Align to the top-left corner.
        /// </summary>
        TopLeft,

        /// <summary>
        /// Align to the top center.
        /// </summary>
        Top,

        /// <summary>
        /// Align to the top-right corner.
        /// </summary>
        TopRight,

        /// <summary>
        /// Align to the left center.
        /// </summary>
        Left,

        /// <summary>
        /// Align to the center.
        /// </summary>
        Center,

        /// <summary>
        /// Align to the right center.
        /// </summary>
        Right,

        /// <summary>
        /// Align to the bottom-left corner.
        /// </summary>
        BottomLeft,

        /// <summary>
        /// Align to the bottom center.
        /// </summary>
        Bottom,

        /// <summary>
        /// Align to the bottom-right corner.
        /// </summary>
        BottomRight
    }

    /// <summary>
    /// Type of altitude
    /// </summary>
    public enum AltitudeType
    {
        /// <summary>
        /// Altitude above sea level
        /// </summary>
        absolute,

        /// <summary>
        /// Altitude above ground level
        /// </summary>
        relative
    }

    /// <summary>
    /// Buffer status.
    /// </summary>
    public enum BufferStatus
    {
        /// <summary>
        /// Waiting for processing.
        /// </summary>
        wait,

        /// <summary>
        /// Currently being processed.
        /// </summary>
        working,

        /// <summary>
        /// Processing is complete.
        /// </summary>
        complete,

        /// <summary>
        /// Processing has started.
        /// </summary>
        start,

        /// <summary>
        /// Buffer has been disposed.
        /// </summary>
        disposed
    }

    /// <summary>
    /// Specifies the point at which the camera should look.
    /// </summary>
    public enum CameraAdjust
    {
        /// <summary>
        /// The camera looks at the maximum elevation in the area.
        /// </summary>
        maxElevationInArea,

        /// <summary>
        /// The camera looks at the center point elevation.
        /// </summary>
        centerPointElevation,

        /// <summary>
        /// The camera looks at a specified GameObject.
        /// </summary>
        gameObject,

        /// <summary>
        /// The camera looks at the average center of the area.
        /// </summary>
        averageCenter
    }

    /// <summary>
    /// Type of tileset map collider.
    /// </summary>
    public enum ColliderType
    {
        /// <summary>
        /// Box collider.
        /// </summary>
        box,

        /// <summary>
        /// Full mesh collider.
        /// </summary>
        fullMesh,

        /// <summary>
        /// Simple mesh collider.
        /// </summary>
        simpleMesh,

        /// <summary>
        /// Flat box collider.
        /// </summary>
        flatBox
    }

    /// <summary>
    /// The rule for calculating the lowest mesh point
    /// </summary>
    public enum ElevationBottomMode
    {
        /// <summary>
        /// Based on zero elevation
        /// </summary>
        zero,

        /// <summary>
        /// Based on the minimum value in the area
        /// </summary>
        minValue
    }

    /// <summary>
    /// OnlineMaps events.
    /// </summary>
    public enum Events
    {
        /// <summary>
        /// Event triggered when the position changes.
        /// </summary>
        changedPosition,

        /// <summary>
        /// Event triggered when the zoom level changes.
        /// </summary>
        changedZoom
    }

    /// <summary>
    /// Type of position range.
    /// </summary>
    public enum LocationRangeType
    {
        /// <summary>
        /// Center position range.
        /// </summary>
        center,

        /// <summary>
        /// Border position range.
        /// </summary>
        border
    }

    /// <summary>
    /// Source of map tiles.
    /// </summary>
    public enum MapSource
    {
        /// <summary>
        /// Map tiles are sourced online.
        /// </summary>
        Online,

        /// <summary>
        /// Map tiles are sourced from Resources.
        /// </summary>
        Resources,

        /// <summary>
        /// Map tiles are sourced from both Resources and online.
        /// </summary>
        ResourcesAndOnline,

        /// <summary>
        /// Map tiles are sourced from StreamingAssets.
        /// </summary>
        StreamingAssets,

        /// <summary>
        /// Map tiles are sourced from both StreamingAssets and online.
        /// </summary>
        StreamingAssetsAndOnline
    }

    /// <summary>
    /// Target type for the map.
    /// </summary>
    public enum MapTarget
    {
        /// <summary>
        /// Map target is a texture.
        /// </summary>
        texture,

        /// <summary>
        /// Map target is a mesh.
        /// </summary>
        mesh,

        /// <summary>
        /// Map target is a tileset.
        /// </summary>
        tileset,

        /// <summary>
        /// Map target is a spriteset.
        /// </summary>
        spriteset,
    }

    /// <summary>
    /// Mode of the 2D marker.
    /// </summary>
    public enum Marker2DMode
    {
        /// <summary>
        /// Marker will be drawn as a plane.
        /// </summary>
        flat,

        /// <summary>
        /// Marker will be drawn as a billboard.
        /// </summary>
        billboard
    }

    /// <summary>
    /// Server of Open Street Map Overpass API.
    /// </summary>
    public enum OSMOverpassServer
    {
        main = 0,
        main2 = 1,
        french = 2,
        taiwan = 3,
        kumiSystems = 4,
    }


    /// <summary>
    /// Status of the request to a webservice.
    /// </summary>
    public enum RequestStatus
    {
        /// <summary>
        /// The request is idle.
        /// </summary>
        idle,

        /// <summary>
        /// The request is currently downloading.
        /// </summary>
        downloading,

        /// <summary>
        /// The request was successful.
        /// </summary>
        success,

        /// <summary>
        /// The request encountered an error.
        /// </summary>
        error,

        /// <summary>
        /// The request has been disposed.
        /// </summary>
        disposed
    }

    /// <summary>
    /// When need to show marker tooltip.
    /// </summary>
    public enum ShowMarkerTooltip
    {
        /// <summary>
        /// Show tooltip when hovering over the marker.
        /// </summary>
        onHover,

        /// <summary>
        /// Show tooltip when pressing the marker.
        /// </summary>
        onPress,

        /// <summary>
        /// Always show the tooltip.
        /// </summary>
        always,

        /// <summary>
        /// Never show the tooltip.
        /// </summary>
        none
    }

    /// <summary>
    /// Tile state
    /// </summary>
    public enum TileStatus
    {
        /// <summary>
        /// Idle
        /// </summary>
        idle,

        /// <summary>
        /// Tile is currently loading.
        /// </summary>
        loading,

        /// <summary>
        /// Tile has been loaded.
        /// </summary>
        loaded,

        /// <summary>
        /// Tile encountered an error.
        /// </summary>
        error,

        /// <summary>
        /// Tile has been disposed.
        /// </summary>
        disposed
    }

    /// <summary>
    /// Type of checking 2D markers on visibility.
    /// </summary>
    public enum TileSetCheckMarker2DVisibility
    {
        /// <summary>
        /// Will be checked only coordinates of markers. Faster.
        /// </summary>
        pivot,

        /// <summary>
        /// Will be checked all the border of marker. If the marker is located on the map at least one point, then it will be shown.
        /// </summary>
        bounds
    }

    /// <summary>
    /// Mode of drawing elements on the tileset.
    /// </summary>
    public enum TileSetDrawingMode
    {
        /// <summary>
        /// Draw elements as meshes.
        /// </summary>
        meshes,

        /// <summary>
        /// Draw elements as overlay.
        /// </summary>
        overlay
    }

    /// <summary>
    /// Type of the marker.
    /// </summary>
    public enum UserLocationMarkerType
    {
        /// <summary>
        /// 2D marker.
        /// </summary>
        twoD = 0,

        /// <summary>
        /// 3D marker.
        /// </summary>
        threeD = 1
    }

    /// <summary>
    /// Mode of smooth zoom.
    /// </summary>
    public enum ZoomMode
    {
        /// <summary>
        /// Zoom at touch point.
        /// </summary>
        target,

        /// <summary>
        /// Zoom at center of map.
        /// </summary>
        center
    }

    /// <summary>
    /// Events that trigger zoom actions.
    /// </summary>
    public enum ZoomEvent
    {
        /// <summary>
        /// Zoom triggered by double-clicking.
        /// </summary>
        doubleClick,

        /// <summary>
        /// Zoom triggered by using the mouse wheel.
        /// </summary>
        wheel,

        /// <summary>
        /// Zoom triggered by a gesture.
        /// </summary>
        gesture
    }
}