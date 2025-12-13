/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using OnlineMaps;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// This class is responsible for drawing the map. Please do not use it if you do not know what you're doing. Perform all operations with the map through other classes.
    /// </summary>
    public class Buffer
    {
        /// <summary>
        /// The default position of the buffer.
        /// </summary>
        public static readonly Vector2Int defaultBufferPosition = new Vector2Int(int.MinValue, int.MinValue);
        
        /// <summary>
        /// Indicates that the buffer can unload tiles.
        /// </summary>
        public bool allowUnloadTiles = true;

        /// <summary>
        /// Reference to OnlineMaps.
        /// </summary>
        public Map map;

        /// <summary>
        /// Position the tile, which begins buffer.
        /// </summary>
        public Vector2Int bufferPosition = defaultBufferPosition;

        /// <summary>
        /// Front buffer.
        /// </summary>
        public Color32[] frontBuffer;

        /// <summary>
        /// Position the tile, which begins front buffer.
        /// </summary>
        public Vector2Int frontBufferPosition;

        /// <summary>
        /// Height of the buffer.
        /// </summary>
        public int height;

        /// <summary>
        /// Indicates that the buffer should unload tiles.
        /// </summary>
        public bool needUnloadTiles;

        /// <summary>
        /// The current status of the buffer.
        /// </summary>
        public BufferStatus status = BufferStatus.wait;

        /// <summary>
        /// Width of the buffer.
        /// </summary>
        public int width;

        private Color32[] backBuffer;
        private List<Tile> newTiles;
        private bool disposed;
    
        /// <summary>
        /// The last state of the map.
        /// </summary>
        public StateProps lastState;
    
        /// <summary>
        /// The current state of the map.
        /// </summary>
        public StateProps renderState;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="map">Map</param>
        public Buffer(Map map)
        {
            this.map = map;
            renderState = lastState = new StateProps(map);
            newTiles = new List<Tile>();
        }

        private void ApplyNewTiles()
        {
            if (newTiles == null || newTiles.Count == 0) return;

            lock (newTiles)
            {
                foreach (Tile tile in newTiles)
                {
                    if (disposed) return;
                    if (tile.status == TileStatus.disposed) continue;

                    RasterTile rTile = tile as RasterTile;

#if !UNITY_WEBGL
                    int counter = 20;
                    while (rTile.colors.Length < Constants.SqrTileSize && counter > 0)
                    {
                        Compatibility.ThreadSleep(1);
                        counter--;
                    }
#endif
                    rTile.ApplyColorsToChildren();
                }
                if (newTiles.Count > 0) newTiles.Clear();
            }
        }

        /// <summary>
        /// Adds a tile into the buffer.
        /// </summary>
        /// <param name="tile">Tile</param>
        public void ApplyTile(Tile tile)
        {
            if (newTiles == null) newTiles = new List<Tile>();
            lock (newTiles)
            {
                newTiles.Add(tile);
            }
        }

        private List<Tile> CreateParents(List<Tile> tiles, int zoom)
        {
            List<Tile> newParentTiles = new List<Tile>(tiles.Count);

            for (int i = 0; i < tiles.Count; i++)
            {
                Tile tile = tiles[i];
                if (tile.parent == null) CreateTileParent(zoom, tile, newParentTiles);
                else newParentTiles.Add(tile.parent);

                tile.used = true;
                tile.parent.used = true;
            }

            return newParentTiles;
        }

        private void CreateTileParent(int zoom, Tile tile, List<Tile> newParentTiles)
        {
            int px = tile.x / 2;
            int py = tile.y / 2;

            Tile parent;
            if (!map.tileManager.GetTile(zoom, px, py, out parent))
            {
                parent = map.control.CreateTile(px, py, zoom);
                RasterTile rParent = parent as RasterTile;
                if (rParent != null) rParent.OnSetColor = OnTileSetColor;
            }

            newParentTiles.Add(parent);
            parent.used = true;
            tile.SetParent(parent);
        }

        /// <summary>
        /// Dispose of buffer.
        /// </summary>
        public void Dispose()
        {
            try
            {
                map.tileManager.Reset();

                frontBuffer = null;
                backBuffer = null;
                map = null;

                status = BufferStatus.disposed;
                newTiles = null;
                disposed = true;
            }
            catch (Exception exception)
            {
                Debug.Log(exception.Message);
            }
        }

        /// <summary>
        /// Generates the front buffer.
        /// </summary>
        public void GenerateFrontBuffer()
        {
            try
            {
                lastState = new StateProps(map);

                while (!disposed)
                {
#if !UNITY_WEBGL
                    while (status != BufferStatus.start && map.renderInThread)
                    {
                        if (disposed) return;
                        Compatibility.ThreadSleep(1);
                    }
#endif

                    status = BufferStatus.working;

                    renderState = new StateProps(map);

                    try
                    {
                        if (newTiles != null && map.control.resultIsTexture) ApplyNewTiles();

                        if (disposed) return;

                        UpdateBackBuffer();
                        if (disposed) return;

                        GenerateTexture();
                    }
                    catch (Exception exception)
                    {
                        if (disposed) return;
                        Debug.Log(exception.Message + "\n" + exception.StackTrace);
                    }

                    status = BufferStatus.complete;

                    lastState = renderState;
#if !UNITY_WEBGL
                    if (!map.renderInThread) break;
#else
                    break;
#endif
                }
            }
            catch
            {
            }
        }

        private void GenerateTexture()
        {
            if (!map.control.resultIsTexture) return;
            
            UpdateFrontBufferPosition();
            UpdateFrontBuffer();

            if (disposed) return;
            
            Vector2 currentBufferPosition = new Vector2(bufferPosition.x + (float)frontBufferPosition.x / Constants.TileSize, bufferPosition.y + (float)frontBufferPosition.y / Constants.TileSize);
            IMapDrawer mapDrawer = map.control.mapDrawer;

            foreach (DrawingElement element in map.control.drawingElementManager)
            {
                if (disposed) return;
                BufferElementDrawerBase.Draw(mapDrawer, element, frontBuffer, currentBufferPosition, renderState.width, renderState.height, renderState.zoom);
            }

            if (map.control.OnDrawMarkers != null) map.control.OnDrawMarkers();
        }

        /// <summary>
        /// Get corners of the map.
        /// </summary>
        public GeoRect GetCorners()
        {
            long maxResolution = (long)renderState.countTiles * Constants.TileSize;
            double hx = renderState.width / (double)maxResolution / 2;
            double hy = renderState.height / (double)maxResolution / 2;
            float factor = renderState.zoomFactor;

            GeoPoint p1 = renderState.centerMercator.Subtract(hx, hy).ToLocation(map);
            GeoPoint p2 = renderState.centerMercator.Add(hx, hy).ToLocation(map);
            
            if (maxResolution != renderState.width || Math.Abs(factor) > float.Epsilon)
            {
                return new GeoRect(p1, p2);
            }
            
            double lng = renderState.center.x + 180;
            p1.x = lng + 0.001;
            if (p1.x > 180) p1.x -= 360;

            p2.x = lng - 0.001;
            if (p2.x > 180) p2.x -= 360;

            return new GeoRect(p1, p2);
        }

        private void InitTile(int zoom, int maxY, List<Tile> newBaseTiles, int px, int py)
        {
            if (py < 0 || py >= maxY) return;

            Tile tile;

            if (!map.tileManager.GetTile(zoom, px, py, out tile))
            {
                Tile parent = null;

                if (renderState.intZoom - zoom > map.countParentLevels)
                {
                    int ptx = px / 2;
                    int pty = py / 2;
                    if (map.tileManager.GetTile(zoom - 1, ptx, pty, out parent)) parent.used = true;
                }

                tile = map.control.CreateTile(px, py, zoom);
                tile.parent = parent;
                if (tile is RasterTile) (tile as RasterTile).OnSetColor = OnTileSetColor;
            }

            newBaseTiles.Add(tile);
            tile.used = true;
        }

        private void InitTiles(int zoom, int countX, Vector2Int pos, int countY, int maxY, List<Tile> newBaseTiles)
        {
            int countTiles = renderState.countTiles;
            for (int x = 0; x < countX; x++)
            {
                int px = (x + pos.x + countTiles) % countTiles;

                for (int y = 0; y < countY; y++)
                {
                    InitTile(zoom, maxY, newBaseTiles, px, pos.y + y);
                }
            }
        }

        private void OnTileSetColor(RasterTile tile)
        {
            if (tile.zoom == renderState.intZoom) SetBufferTile(tile);
        }

        private Rect SetBufferTile(Tile tile, int? offsetX = null)
        {
            if (!map.control.resultIsTexture) return default;

            const int s = Constants.TileSize;
            int i = 0;
            int px = tile.x - bufferPosition.x;
            int py = tile.y - bufferPosition.y;

            int maxX = 1 << tile.zoom;

            if (px < 0) px += maxX;
            else if (px >= maxX) px -= maxX;

            if (renderState.width == maxX * s && px < 2 && !offsetX.HasValue) SetBufferTile(tile, maxX);

            if (offsetX.HasValue) px += offsetX.Value;

            px *= s;
            py *= s;

            if (px + s < 0 || py + s < 0 || px > width || py > height) return new Rect(0, 0, 0, 0);

            if (!tile.hasColors || tile.status != TileStatus.loaded)
            {
                const int hs = s / 2;
                int sx = tile.x % 2 * hs;
                int sy = tile.y % 2 * hs;
                if (SetBufferTileFromParent(tile, px, py, s / 2, sx, sy)) return new Rect(px, py, s, s);
            }

            RasterTile rTile = tile as RasterTile;
            Color32[] colors = rTile.colors;

            lock (colors)
            {
                int maxSize = width * height;

                for (int y = py + s - 1; y >= py; y--)
                {
                    int bp = y * width + px;
                    if (bp + s < 0 || bp >= maxSize) continue;
                    int l = s;
                    if (bp < 0)
                    {
                        l -= bp;
                        bp = 0;
                    }
                    else if (bp + s > maxSize)
                    {
                        l -= maxSize - (bp + s);
                        bp = maxSize - s - 1;
                    }

                    try
                    {
                        Array.Copy(colors, i, backBuffer, bp, l);
                    }
                    catch
                    {
                    }

                    i += s;
                }

                return new Rect(px, py, Constants.TileSize, Constants.TileSize);
            }
        }

        private bool SetBufferTileFromParent(Tile tile, int px, int py, int size, int sx, int sy)
        {
            Tile parent = tile.parent;
            if (parent == null) return false;

            const int s = Constants.TileSize;
            const int hs = s / 2;

            if (parent.status != TileStatus.loaded || !parent.hasColors)
            {
                sx = sx / 2 + parent.x % 2 * hs;
                sy = sy / 2 + parent.y % 2 * hs;
                return SetBufferTileFromParent(parent, px, py, size / 2, sx, sy);
            }

            RasterTile rParent = parent as RasterTile;
            Color32[] colors = rParent.colors;
            int scale = s / size;

            if (colors.Length != Constants.SqrTileSize) return false;

            int ry = s - sy - 1;

            lock (colors)
            {
                if (size == hs)
                {
                    for (int y = 0; y < hs; y++)
                    {
                        int oys = (ry - y) * s + sx;
                        int bp = (y * 2 + py) * width + px;
                        for (int x = 0; x < hs; x++)
                        {
                            Color32 clr = colors[oys + x];

                            backBuffer[bp] = clr;
                            backBuffer[bp + width] = clr;
                            backBuffer[++bp] = clr;
                            backBuffer[bp + width] = clr;
                            bp++;
                        }
                    }
                }
                else
                {
                    for (int y = 0; y < size; y++)
                    {
                        int oys = (ry - y) * s + sx;
                        int scaledY = y * scale + py;
                        for (int x = 0; x < size; x++)
                        {
                            Color32 clr = colors[oys + x];
                            int scaledX = x * scale + px;

                            for (int by = scaledY; by < scaledY + scale; by++)
                            {
                                int bpy = by * width + scaledX;
                                for (int bx = bpy; bx < bpy + scale; bx++) backBuffer[bx] = clr;
                            }
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Sets the color to the buffer.
        /// </summary>
        /// <param name="clr">Color</param>
        /// <param name="ip">Tile position</param>
        /// <param name="x">Pixel X</param>
        /// <param name="y">Pixel Y</param>
        public void SetColorToBuffer(Color clr, Vector2Int ip, int x, int y)
        {
            if (Math.Abs(clr.a) < float.Epsilon) return;
            
            int bufferIndex = (renderState.height - ip.y - y - 1) * renderState.width + ip.x + x;
            if (bufferIndex < 0 || bufferIndex >= frontBuffer.Length) return;
            
            if (clr.a < 1)
            {
                float alpha = clr.a;
                Color bufferColor = frontBuffer[bufferIndex];
                clr.a = 1;
                clr.r = Mathf.Lerp(bufferColor.r, clr.r, alpha);
                clr.g = Mathf.Lerp(bufferColor.g, clr.g, alpha);
                clr.b = Mathf.Lerp(bufferColor.b, clr.b, alpha);
            }
            frontBuffer[bufferIndex] = clr;
        }

        /// <summary>
        /// Unload old tiles.
        /// </summary>
        public void UnloadOldTiles()
        {
            needUnloadTiles = false;

#if !UNITY_WEBGL
            int count = 100;

            while (map.renderInThread && !allowUnloadTiles && count > 0)
            {
                Compatibility.ThreadSleep(1);
                count--;
            }

            if (count == 0) return;
#endif
            lock (Tile.lockTiles)
            {
                foreach (Tile tile in map.tileManager.tiles)
                {
                    if (!tile.used && !tile.isBlocked && tile.map == map) tile.Dispose();
                }
            }
        }

        /// <summary>
        /// Unload old types of tiles.
        /// </summary>
        public void UnloadOldTypes()
        {
            try
            {
                lock (Tile.lockTiles)
                {
                    foreach (Tile tile in map.tileManager.tiles)
                    {
                        RasterTile rt = tile as RasterTile;
                        if (rt != null && rt.map == map && map.activeType != rt.mapType)
                        {
                            tile.Dispose();
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.Log(exception.Message);
            }
        }

        private void UpdateBackBuffer()
        {
            const int s = Constants.TileSize;
            int countX = renderState.width / s + 2;
            int countY = renderState.height / s + 2;

            TilePoint t = renderState.centerMercator.ToTile(renderState.intZoom);
            Vector2Int pos = new Vector2Int((int)t.x - countX / 2, (int)t.y - countY / 2);

            int countTiles = renderState.countTiles;

            if (pos.y < 0) pos.y = 0;
            else if (pos.y >= countTiles - countY) pos.y = countTiles - countY;

            if (map.control.resultIsTexture)
            {
                if (frontBuffer == null || frontBuffer.Length != renderState.width * renderState.height) frontBuffer = new Color32[renderState.width * renderState.height];
                if (backBuffer == null || width != countX * s || height != countY * s)
                {
                    width = countX * s;
                    height = countY * s;
                    backBuffer = new Color32[height * width];
                }
            }

            bufferPosition = pos;

            List<Tile> newBaseTiles = new List<Tile>();

            lock (Tile.lockTiles)
            {
                List<Tile> tiles = map.tileManager.tiles;
                for (int i = 0; i < tiles.Count; i++) tiles[i].used = false;

                InitTiles(renderState.intZoom, countX, pos, countY, countTiles, newBaseTiles);

                if (map.countParentLevels > 0)
                {
                    List<Tile> newParentTiles = newBaseTiles;
                    int minZoom = Mathf.Max(renderState.intZoom - map.countParentLevels, Constants.MinZoom);
                    for (int z = renderState.intZoom - 1; z >= minZoom; z--)
                    {
                        newParentTiles = CreateParents(newParentTiles, z);
                    }
                }
            }

            if (map.control.resultIsTexture)
            {
                for (int i = 0; i < newBaseTiles.Count; i++)
                {
                    SetBufferTile(newBaseTiles[i]);
                }
            }

            needUnloadTiles = true;
        }

        private void UpdateFrontBuffer()
        {
            float zoomFactor = renderState.zoomFactor;
            int w = renderState.width;
            int h = renderState.height;
            int bufferSize = height * width;

            for (int y = 0; y < h; y++)
            {
                float fy = y * zoomFactor + frontBufferPosition.y;
                int iy1 = (int) fy;
                int iyw1 = iy1 * width;
                int iyw2 = iyw1 + width + 1;
                if (iyw2 >= bufferSize - 1) continue;
            
                int fby = (h - y - 1) * w;
                float fx = frontBufferPosition.x;

                for (int x = 0; x < w; x++)
                {
                    Color32 clr1 = backBuffer[iyw1 + (int)fx];
                    frontBuffer[fby++] = clr1;
                    fx += zoomFactor;
                }
            }
        }

        private void UpdateFrontBufferPosition()
        {
            int countX = renderState.width / Constants.TileSize / 2;
            int countY = renderState.height / Constants.TileSize / 2;
            
            float zoomFactor = renderState.zoomFactor;
            TilePoint t = renderState.centerMercator.ToTile(renderState.intZoom) - bufferPosition;
            t = t.Subtract(countX * zoomFactor, countY * zoomFactor) * Constants.TileSize;

            int y = Math.Clamp((int) t.y, 0, (int)(height - renderState.height * zoomFactor));
            frontBufferPosition = new Vector2Int((int)t.x, y);
        }
    }
}