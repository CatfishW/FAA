/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// 2D marker class.
    /// </summary>
    [Serializable]
    public class Marker2D : Marker
    {
        /// <summary>
        /// Marker texture align.
        /// </summary>
        public Align align = Align.Bottom;
        
        [SerializeField]
        private Texture2D _texture;
        
        private Color32[] _colors;
        private int _height;
        private Color32[] _rotatedColors;
        private int _textureHeight;
        private int _textureWidth;
        private int _width;
        private float _lastRotation;
        private float _lastScale;

        /// <summary>
        /// Gets the marker colors.
        /// </summary>
        /// <value>
        /// The colors.
        /// </value>
        public Color32[] colors
        {
            get
            {
                if (!map.control.resultIsTexture) return _colors;
                if (Math.Abs(_rotation) < float.Epsilon && Math.Abs(scale - 1) < float.Epsilon) return _colors;

                if (Math.Abs(_lastRotation - _rotation) > float.Epsilon || Math.Abs(_lastScale - _scale) > float.Epsilon) UpdateRotatedBuffer();
                return _rotatedColors;
            }
        }

        /// <summary>
        /// Gets or sets marker enabled.
        /// </summary>
        /// <value>
        /// true if enabled, false if not.
        /// </value>
        public override bool enabled
        {
            set
            {
                if (_enabled == value) return;
                
                _enabled = value;
                if (OnEnabledChange != null) OnEnabledChange(this);
                if (Utils.isPlaying) map.Redraw();
            }
        }

        /// <summary>
        /// Gets the marker height.
        /// </summary>
        /// <value>
        /// The height.
        /// </value>
        public int height => _height;

        /// <summary>
        /// Specifies to Buffer that the marker is available for drawing.<br/>
        /// Please do not use.
        /// </summary>
        public bool locked { get; set; }

        /// <summary>
        /// Relative area of activity of the marker.
        /// </summary>
        public Rect markerColliderRect { get; set; } = new Rect(-0.5f, -0.5f, 1, 1);

        /// <summary>
        /// Area of the screen, which is a marker after map will be redrawed.
        /// Note: When used as a source display Texture or Tileset, is not returned the correct value.
        /// </summary>
        /// <value>
        /// Marker rectangle.
        /// </value>
        public Rect realScreenRect
        {
            get
            {
                Rect controlRect = map.control.GetScreenRect();
                Rect uvRect = map.control.uvRect;
                controlRect.width /= uvRect.width;
                controlRect.height /= uvRect.height;
                controlRect.x -= controlRect.width * uvRect.x;
                controlRect.y -= controlRect.height * uvRect.y;
                
                TilePoint t = map.view.topLeftTile * Constants.TileSize;
                TilePoint mt = location.ToTile(map) * Constants.TileSize;

                Vector2 pos = GetAlignedPosition((int)mt.x, (int)mt.y);
                float scaleX = controlRect.width / map.control.width;
                float scaleY = controlRect.height / map.control.height;

                pos.x = Mathf.RoundToInt((float)(pos.x - t.x) * scaleX + controlRect.x);
                pos.y = Mathf.RoundToInt(controlRect.yMax - (float)(pos.y - t.y + height) * scaleY);

                return new Rect(pos.x, pos.y, width * scaleX, height * scaleY);
            }
        }

        /// <summary>
        /// Gets or sets the rotation of the marker in degree (0-360)
        /// </summary>
        /// <value>
        /// The rotation.
        /// </value>
        public override float rotation
        {
            set
            {
                if (Math.Abs(_rotation - value) <= float.Epsilon) return;
                
                _rotation = value;
                if (!Utils.isPlaying) return;
                if (!(map.control is TileSetControl)) UpdateRotatedBuffer();
                map.Redraw();
            }
        }

        public override float scale
        {
            get => _scale;
            set
            {
                if (Math.Abs(_scale - value) < float.Epsilon) return;

                _scale = value;
                if (Utils.isPlaying) UpdateRotatedBuffer();
            }
        }

        /// <summary>
        /// Area of the screen, which is a marker at the current map display.
        /// Note: When used as a source display Texture or Tileset, is not returned the correct value.
        /// </summary>
        /// <value>
        /// Marker rectangle.
        /// </value>
        public Rect screenRect => GetRect();

        /// <summary>
        /// Texture marker. <br/>
        /// Texture format: ARGB32.<br/>
        /// Must enable "Read / Write enabled".<br/>
        /// After changing the texture you need to call Marker2D.Init.
        /// </summary>
        public Texture2D texture
        {
            get => _texture;
            set
            {
                _texture = value;
                if (!map) return;
                
                Init();
                map.Redraw();
            }
        }

        /// <summary>
        /// Gets the marker width.
        /// </summary>
        /// <value>
        /// The width.
        /// </value>
        public int width => _width;

        public Marker2D()
        {
        
        }

        /// <summary>
        /// Gets aligned position.
        /// </summary>
        /// <param name="pos">
        /// Buffer position.
        /// </param>
        /// <returns>
        /// The aligned buffer position.
        /// </returns>
        public Vector2Int GetAlignedPosition(Vector2Int pos) => GetAlignedPosition(pos.x, pos.y);

        /// <summary>
        /// Gets aligned position.
        /// </summary>
        /// <param name="px">Buffer position X</param>
        /// <param name="py">Buffer position Y</param>
        /// <returns>The aligned buffer position.</returns>
        public Vector2Int GetAlignedPosition(int px, int py)
        {
            Vector2Int offset = GetAlignOffset();

            if (Math.Abs(_lastRotation - _rotation) > float.Epsilon || Math.Abs(_lastScale - _scale) > float.Epsilon) UpdateRotatedBuffer();
            if (Math.Abs(_rotation) < float.Epsilon && Math.Abs(scale - 1) < float.Epsilon) return new Vector2Int(px - offset.x, py - offset.y);

            float angle = 1 - Mathf.Repeat(_rotation * 360, 360);
            Matrix4x4 matrix = new Matrix4x4();

            matrix.SetTRS(new Vector3(_width >> 1, 0, _height >> 1), Quaternion.Euler(0, angle, 0), new Vector3(scale, scale, scale));
            Vector3 off = matrix.MultiplyPoint(new Vector3(offset.x - (_textureWidth >> 1), 0, offset.y - (_textureHeight >> 1)));
            px -= (int)off.x;
            py -= (int)off.z;

            return new Vector2Int(px, py);
        }

        /// <summary>
        /// Gets aligned offset (in pixels).
        /// </summary>
        /// <returns>Aligned offset.</returns>
        public Vector2Int GetAlignOffset()
        {
            Vector2Int offset = new Vector2Int();
            if (align == Align.BottomRight || align == Align.Right || align == Align.TopRight) offset.x = _textureWidth;
            else if (align == Align.Bottom || align == Align.Center || align == Align.Top) offset.x = _textureWidth / 2;

            if (align == Align.BottomRight || align == Align.Bottom || align == Align.BottomLeft) offset.y = _textureHeight;
            else if (align == Align.Left || align == Align.Center || align == Align.Right) offset.y = _textureHeight / 2;
            return offset;
        }

        private Rect GetRect()
        {
            Rect controlRect = map.control.GetScreenRect();
            Rect uvRect = map.control.uvRect;
            controlRect.width /= uvRect.width;
            controlRect.height /= uvRect.height;
            controlRect.x -= controlRect.width * uvRect.x;
            controlRect.y -= controlRect.height * uvRect.y;

            StateProps state = map.buffer.lastState;
            int countTiles = state.countTiles;
            bool isEntireWorld = countTiles == map.view.countTilesX;
            bool isBiggerThanBuffer = countTiles - 2 == map.view.countTilesX;

            float zoomFactor = map.view.zoomFactor;
            TilePoint tl = state.rect.topLeft.ToTile(map, state.intZoom);
            tl *= Constants.TileSize / zoomFactor;

            TilePoint t = GetTilePosition(state.intZoom) * Constants.TileSize / zoomFactor;

            Vector2 pos = GetAlignedPosition((int)t.x, (int)t.y);
            float scaleX = controlRect.width / map.control.width;
            float scaleY = controlRect.height / map.control.height;
            pos.x = Mathf.RoundToInt((float)(pos.x - tl.x) * scaleX + controlRect.x);
            pos.y = Mathf.RoundToInt(controlRect.yMax - (float)(pos.y - tl.y + height) * scaleY);

            if (isEntireWorld)
            {
                if (pos.x < controlRect.x) pos.x += controlRect.width / zoomFactor;
            }
            else if (isBiggerThanBuffer)
            {
                if (pos.x < controlRect.x) pos.x += controlRect.width * 2 / zoomFactor;
            }

            return new Rect(pos.x, pos.y, width * scaleX, height * scaleY);
        }

        /// <summary>
        /// Determines if the marker at the specified coordinates.
        /// </summary>
        /// <param name="location">The location</param>
        /// <param name="zoom">The zoom</param>
        /// <returns>True if the marker in position, false if not.</returns>
        public bool HitTest(GeoPoint location, int zoom)
        {
            float zoomFactor = map.buffer.renderState.zoomFactor;
            TilePoint t1 = this.location.ToTile(map, zoom) * Constants.TileSize / zoomFactor;

            float w = width;
            float h = height;

            Vector2Int pos = GetAlignedPosition((int)t1.x, (int)t1.y);
            TilePoint t2 = location.ToTile(map, zoom) * Constants.TileSize / zoomFactor - pos;

            return t2.x >= w * (markerColliderRect.x + 0.5f) 
                   && t2.x <= w * (markerColliderRect.xMax + 0.5f) 
                   && t2.y >= w * (markerColliderRect.y + 0.5f) 
                   && t2.y <= h * (markerColliderRect.yMax + 0.5f);
        }

        /// <summary>
        /// Initializes this marker.
        /// </summary>
        /// <param name="width">Width of the marker texture.</param>
        /// <param name="height">Height of the marker texture.</param>
        public void Init(int? width = null, int? height = null)
        {
            if (texture != null)
            {
                if (map.control.resultIsTexture) _colors = texture.GetPixels32();
                _width = _textureWidth = width ?? texture.width;
                _height = _textureHeight = height ?? texture.height;
            }
            else
            {
                Texture2D defaultTexture = (manager as Marker2DManager).defaultTexture;
                if (defaultTexture != null)
                {
                    if (map.control.resultIsTexture) _colors = defaultTexture.GetPixels32();
                    _width = _textureWidth = width ?? defaultTexture.width;
                    _height = _textureHeight = height ?? defaultTexture.height;
                }
            }
            if (Math.Abs(_rotation) > float.Epsilon || Math.Abs(scale - 1) > float.Epsilon) UpdateRotatedBuffer();
            if (OnInitComplete != null) OnInitComplete(this);
        }

        public override void LookToLocation(GeoPoint location, float rotationOffset = 90)
        {
            TilePoint t1 = location.ToTile(map, 20);
            TilePoint t2 = this.location.ToTile(map, 20);
            rotation = (float)(360 + rotationOffset - Geometry.Angle2D(t1, t2));
        }

        public override JSONItem ToJSON()
        {
            return base.ToJSON().AppendObject(new
            {
                align = (int)align,
                texture = texture ? texture.GetInstanceID() : 0,
                rotation
            });
        }

        private void UpdateRotatedBuffer()
        {
            _lastRotation = _rotation;
            _lastScale = _scale;

            if (!map.control.resultIsTexture || (Math.Abs(_rotation) < float.Epsilon && Math.Abs(scale - 1) < float.Epsilon))
            {
                _width = _textureWidth;
                _height = _textureHeight;
                return;
            }

#if !UNITY_WEBGL
            int maxLocked = 20;
            while (locked && maxLocked > 0)
            {
                Compatibility.ThreadSleep(1);
                maxLocked--;
            }
#endif

            locked = true;

            float angle = Mathf.Repeat(_rotation * 360, 360);
            Matrix4x4 matrix = new Matrix4x4();
            matrix.SetTRS(Vector3.zero, Quaternion.Euler(0, angle, 0), new Vector3(scale, scale, scale));
            Matrix4x4 inv = matrix.inverse;
            Vector3 p1 = matrix.MultiplyPoint3x4(new Vector3(_textureWidth, 0, 0));
            Vector3 p2 = matrix.MultiplyPoint3x4(new Vector3(0, 0, _textureHeight));
            Vector3 p3 = matrix.MultiplyPoint3x4(new Vector3(_textureWidth, 0, _textureHeight));

            float minX = Mathf.Min(0, p1.x, p2.x, p3.x);
            float minZ = Mathf.Min(0, p1.z, p2.z, p3.z);
            float maxX = Mathf.Max(0, p1.x, p2.x, p3.x);
            float maxZ = Mathf.Max(0, p1.z, p2.z, p3.z);

            _width = Mathf.RoundToInt(maxX - minX + 0.5f);
            _height = Mathf.RoundToInt(maxZ - minZ + 0.5f);

            Color32 emptyColor = new Color(0, 0, 0, 0);

            if (_rotatedColors == null || _rotatedColors.Length != _width * _height) _rotatedColors = new Color32[_width * _height];

            int tw = _textureWidth;
            int th = _textureHeight;

            Color32 c1, c2, c3, c4;

            for (int y = 0; y < _height; y++)
            {
                float ry = minZ + y;
                int cy = y * _width;
                for (int x = 0; x < _width; x++)
                {
                    float rx = minX + x;
                    Vector3 p = inv.MultiplyPoint3x4(new Vector3(rx, 0, ry));
                    int iz = (int)p.z;
                    int ix = (int)p.x;
                    float fx = p.x - ix;
                    float fz = p.z - iz;

                    if (ix + 1 >= 0 && ix < tw && iz + 1 >= 0 && iz < th)
                    {
                        if (ix >= 0 && iz >= 0) c1 = _colors[iz * tw + ix];
                        else c1 = emptyColor;

                        if (ix + 1 < tw && iz >= 0) c2 = _colors[iz * tw + ix + 1];
                        else c2 = emptyColor;

                        if (ix >= 0 && iz + 1 < th) c3 = _colors[(iz + 1) * tw + ix];
                        else c3 = emptyColor;

                        if (ix + 1 < tw && iz + 1 < th) c4 = _colors[(iz + 1) * tw + ix + 1];
                        else c4 = emptyColor;

                        c1 = Color32.Lerp(c1, c2, fx);
                        c3 = Color32.Lerp(c3, c4, fx);

                        _rotatedColors[cy + x] = Color32.Lerp(c1, c3, fz);
                    }
                    else _rotatedColors[cy + x] = emptyColor;
                }
            }

            locked = false;
        }
    }
}