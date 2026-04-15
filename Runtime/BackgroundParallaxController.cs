using System;
using System.Collections.Generic;
using UnityEngine;

namespace Interactme.Parallax
{
    /// <summary>
    /// Parallax background controller.
    /// Layers are ordered from bottom to top:
    /// element 0 is the lowest layer.
    /// </summary>
    public sealed class BackgroundParallaxController : MonoBehaviour
    {
        public enum LayerPositionMode
        {
            Auto = 0,
            World = 1,
            AnchoredUI = 2
        }

        public enum CycleAxisMode
        {
            X = 0,
            Y = 1,
            Both = 2
        }

        [Serializable]
        public sealed class ParallaxLayer
        {
            [SerializeField] private bool _enabled = true;
            [SerializeField] private Transform _target;
            [SerializeField] private LayerPositionMode _positionMode = LayerPositionMode.Auto;
            [SerializeField] private float _speedMultiplier = 0.2f;

            [Header("Infinite Cycle")]
            [SerializeField] private bool _infiniteCycle = true;
            [SerializeField] private CycleAxisMode _cycleAxis = CycleAxisMode.X;

            [NonSerialized] private Vector3 _startWorldPosition;
            [NonSerialized] private Vector2 _startAnchoredPosition;
            [NonSerialized] private Vector2 _runtimeOffset;
            [NonSerialized] private float _runtimeCycleSizeX;
            [NonSerialized] private float _runtimeCycleSizeY;
            [NonSerialized] private RectTransform _rectTransform;
            [NonSerialized] private bool _useAnchoredPosition;

            public bool Enabled => _enabled;
            public Transform Target => _target;
            public float SpeedMultiplier => _speedMultiplier;
            public bool InfiniteCycle => _infiniteCycle;
            public CycleAxisMode CycleAxis => _cycleAxis;

            public void CaptureStartState()
            {
                if (_target == null)
                {
                    return;
                }

                _rectTransform = _target as RectTransform;
                _useAnchoredPosition = ResolveUseAnchoredPosition();
                _startWorldPosition = _target.position;
                _startAnchoredPosition = _rectTransform != null ? _rectTransform.anchoredPosition : Vector2.zero;
                _runtimeOffset = Vector2.zero;
                _runtimeCycleSizeX = 0f;
                _runtimeCycleSizeY = 0f;

                if (_useAnchoredPosition && _rectTransform != null)
                {
                    var cycleSize = GetRectTransformCycleSize(_rectTransform);
                    var sizeX = cycleSize.x;
                    var sizeY = cycleSize.y;

                    if (sizeX > 0.01f)
                    {
                        _runtimeCycleSizeX = sizeX;
                    }

                    if (sizeY > 0.01f)
                    {
                        _runtimeCycleSizeY = sizeY;
                    }

                    return;
                }

                var spriteRenderer = _target.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    var boundsSize = spriteRenderer.bounds.size;
                    if (boundsSize.x > 0.01f)
                    {
                        _runtimeCycleSizeX = boundsSize.x;
                    }

                    if (boundsSize.y > 0.01f)
                    {
                        _runtimeCycleSizeY = boundsSize.y;
                    }
                }
            }

            public void ApplyDelta(Vector2 motionDelta, float globalSpeed)
            {
                if (!_enabled || _target == null)
                {
                    return;
                }

                var layerDelta = motionDelta * (globalSpeed * _speedMultiplier);
                _runtimeOffset += layerDelta;

                if (_infiniteCycle)
                {
                    if (UsesHorizontalCycle())
                    {
                        _runtimeOffset.x = WrapSigned(_runtimeOffset.x, _runtimeCycleSizeX);
                    }

                    if (UsesVerticalCycle())
                    {
                        _runtimeOffset.y = WrapSigned(_runtimeOffset.y, _runtimeCycleSizeY);
                    }
                }

                if (_useAnchoredPosition && _rectTransform != null)
                {
                    _rectTransform.anchoredPosition = _startAnchoredPosition + _runtimeOffset;
                    return;
                }

                var nextPosition = _startWorldPosition + (Vector3)_runtimeOffset;
                _target.position = new Vector3(nextPosition.x, nextPosition.y, _target.position.z);
            }

            private static float WrapSigned(float value, float period)
            {
                if (period <= 0.01f)
                {
                    return value;
                }

                var half = period * 0.5f;
                return Mathf.Repeat(value + half, period) - half;
            }

            private static Vector2 GetRectTransformCycleSize(RectTransform rectTransform)
            {
                var worldCorners = new Vector3[4];
                rectTransform.GetWorldCorners(worldCorners);

                var horizontalStep = worldCorners[3] - worldCorners[0];
                var verticalStep = worldCorners[1] - worldCorners[0];
                var parent = rectTransform.parent;

                if (parent != null)
                {
                    horizontalStep = parent.InverseTransformVector(horizontalStep);
                    verticalStep = parent.InverseTransformVector(verticalStep);
                }

                return new Vector2(horizontalStep.magnitude, verticalStep.magnitude);
            }

            private bool UsesHorizontalCycle()
            {
                return _cycleAxis == CycleAxisMode.X || _cycleAxis == CycleAxisMode.Both;
            }

            private bool UsesVerticalCycle()
            {
                return _cycleAxis == CycleAxisMode.Y || _cycleAxis == CycleAxisMode.Both;
            }

            private bool ResolveUseAnchoredPosition()
            {
                switch (_positionMode)
                {
                    case LayerPositionMode.World:
                        return false;
                    case LayerPositionMode.AnchoredUI:
                        return _rectTransform != null;
                    default:
                        return _rectTransform != null;
                }
            }
        }

        [Header("General")]
        [SerializeField] private Camera _targetCamera;
        [SerializeField] private bool _useCameraMotion = true;

        [Header("Auto Scroll")]
        [SerializeField] private bool _useAutoScroll = true;
        [SerializeField] private Vector2 _autoScrollDirection = Vector2.left;
        [SerializeField, Min(0f)] private float _autoScrollSpeed = 0.8f;
        [SerializeField] private bool _useUnscaledTime;

        [Header("Speed")]
        [SerializeField, Min(0f)] private float _effectSpeed = 1f;

        [Header("Layers (0 = lowest)")]
        [SerializeField] private List<ParallaxLayer> _layers = new();

        private Vector3 _previousCameraPosition;
        private bool _isInitialized;
        private bool _cameraWarningShown;

        private void OnEnable()
        {
            Initialize();
        }

        private void Start()
        {
            if (!_isInitialized)
            {
                Initialize();
            }
        }

        private void LateUpdate()
        {
            if (!_isInitialized)
            {
                return;
            }

            var motionDelta = Vector2.zero;

            if (_useCameraMotion && _targetCamera != null)
            {
                var cameraPosition = _targetCamera.transform.position;
                var cameraDelta = cameraPosition - _previousCameraPosition;
                motionDelta += new Vector2(cameraDelta.x, cameraDelta.y);
                _previousCameraPosition = cameraPosition;
            }

            if (_useAutoScroll && _autoScrollDirection.sqrMagnitude > 0.0001f && _autoScrollSpeed > 0f)
            {
                var dt = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                var direction = _autoScrollDirection.normalized;
                motionDelta += direction * (_autoScrollSpeed * dt);
            }

            if (motionDelta.sqrMagnitude <= 0f)
            {
                return;
            }

            for (var i = 0; i < _layers.Count; i++)
            {
                _layers[i]?.ApplyDelta(motionDelta, _effectSpeed);
            }
        }

        [ContextMenu("Reinitialize Parallax")]
        public void Initialize()
        {
            if (_targetCamera == null)
            {
                _targetCamera = Camera.main;
            }

            if (_useCameraMotion && _targetCamera == null && !_cameraWarningShown)
            {
                Debug.LogWarning("BackgroundParallaxController: target camera is not assigned.", this);
                _cameraWarningShown = true;
            }

            for (var i = 0; i < _layers.Count; i++)
            {
                _layers[i]?.CaptureStartState();
            }

            _previousCameraPosition = _targetCamera != null ? _targetCamera.transform.position : Vector3.zero;
            _isInitialized = true;
        }
    }
}
