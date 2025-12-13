/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// 3D marker instance class.
    /// </summary>
    [AddComponentMenu("")]
    public class Marker3DInstance : MarkerInstanceBase
    {
        private GeoPoint _location;
        private float _scale;
        private float lastZoom;

        private Marker3D _marker;

        public override Marker marker
        {
            get => _marker;
            set => _marker = value as Marker3D;
        }

        private void Awake()
        {
            Collider cl = GetComponent<Collider>();
            if (cl == null) cl  = gameObject.AddComponent<BoxCollider>();
            cl.isTrigger = true;
        }

        private void LateUpdate()
        {
            if (!(marker is Marker3D)) 
            {
                Utils.Destroy(gameObject);
                return;
            }

            UpdateBaseProps();
        }

        private void Start()
        {
            _location = marker.location;
            _scale = marker.scale;
            Marker3D marker3D = marker as Marker3D;
            transform.localRotation = marker3D.localRotation;

            UpdateScale(marker3D);
        }

        private void UpdateBaseProps()
        {
            GeoPoint p = marker.location;
            Marker3D marker3D = marker as Marker3D;

            if ((_location - p).sqrMagnitude > double.Epsilon)
            {
                _location = p;
                marker.Update();
            }

            if (marker3D.sizeType == Marker3D.SizeType.realWorld && Math.Abs(lastZoom - _marker.manager.map.view.zoom) > float.Epsilon) UpdateScale(marker3D);

            if (Math.Abs(_scale - marker.scale) > float.Epsilon)
            {
                _scale = marker.scale;
                UpdateScale(marker3D);
            }
        }

        private void UpdateScale(Marker3D marker3D)
        {
            MapView view = _marker.manager.map.view;
            
            if (marker3D.sizeType == Marker3D.SizeType.scene) transform.localScale = new Vector3(_scale, _scale, _scale);
            else if (marker3D.sizeType == Marker3D.SizeType.realWorld)
            {
                float factor = (1 << (Constants.MaxZoom - view.intZoom)) * view.zoomFactor;
                float s = _scale / factor;
                transform.localScale = new Vector3(s, s, s);
            }

            lastZoom = view.zoom;
        }
    }
}