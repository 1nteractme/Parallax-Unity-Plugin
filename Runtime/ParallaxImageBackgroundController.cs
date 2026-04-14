using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Interactme.Parallax
{
    /// <summary>
    /// Applies sprite sets to UI Image layers used in parallax background.
    /// </summary>
    public sealed class ParallaxImageBackgroundController : MonoBehaviour
    {
        private const string TileObjectPrefix = "__Tile_";

        public enum SetSelectionMode
        {
            Random = 0,
            Manual = 1
        }

        [Serializable]
        public sealed class SpriteSet
        {
            [SerializeField] private string _id = "Set";
            [SerializeField] private List<Sprite> _layerSprites = new();

            public string Id => _id;
            public IReadOnlyList<Sprite> LayerSprites => _layerSprites;
        }

        [Header("Layers (0 = lowest)")]
        [SerializeField] private List<Image> _layers = new();

        [Header("Sprite Sets")]
        [SerializeField] private List<SpriteSet> _sets = new();

        [Header("Selection")]
        [SerializeField] private SetSelectionMode _selectionMode = SetSelectionMode.Random;
        [SerializeField, Min(0)] private int _manualSetIndex;
        [SerializeField] private bool _applyOnEnable = true;
        [SerializeField] private bool _disableLayerIfSpriteMissing = true;

        [Header("Seamless Tiling")]
        [SerializeField] private bool _enableSeamlessTiling = true;
        [SerializeField] private bool _tileX = true;
        [SerializeField] private bool _tileY;
        [SerializeField] private bool _syncTileLayoutEveryFrame = true;

        private readonly List<LayerTileState> _tileStates = new();
        private readonly List<Vector2Int> _requiredOffsets = new();
        private readonly Vector3[] _worldCornersBuffer = new Vector3[4];
        private int _currentSetIndex = -1;

        public int CurrentSetIndex => _currentSetIndex;

        private void OnEnable()
        {
            if (_applyOnEnable)
            {
                ApplyConfiguredSet();
            }

            if (_enableSeamlessTiling)
            {
                EnsureTilingPrepared();
            }
        }

        private void LateUpdate()
        {
            if (!_enableSeamlessTiling || !_syncTileLayoutEveryFrame)
            {
                return;
            }

            for (var i = 0; i < _tileStates.Count; i++)
            {
                RefreshLayerTiles(_tileStates[i]);
            }
        }

        [ContextMenu("Apply Configured Set")]
        public void ApplyConfiguredSet()
        {
            if (_sets == null || _sets.Count == 0)
            {
                Debug.LogWarning("ParallaxImageBackgroundController: no sprite sets configured.", this);
                return;
            }

            var targetIndex = _selectionMode == SetSelectionMode.Manual
                ? Mathf.Clamp(_manualSetIndex, 0, _sets.Count - 1)
                : UnityEngine.Random.Range(0, _sets.Count);

            ApplySet(targetIndex);
        }

        [ContextMenu("Apply Random Set")]
        public void ApplyRandomSet()
        {
            _selectionMode = SetSelectionMode.Random;
            ApplyConfiguredSet();
        }

        [ContextMenu("Apply Manual Set")]
        public void ApplyManualSet()
        {
            _selectionMode = SetSelectionMode.Manual;
            ApplyConfiguredSet();
        }

        public bool ApplySet(int setIndex)
        {
            if (_sets == null || _sets.Count == 0)
            {
                return false;
            }

            if (setIndex < 0 || setIndex >= _sets.Count)
            {
                Debug.LogWarning($"ParallaxImageBackgroundController: invalid set index {setIndex}.", this);
                return false;
            }

            var set = _sets[setIndex];
            var sprites = set.LayerSprites;

            for (var i = 0; i < _layers.Count; i++)
            {
                var layerImage = _layers[i];
                if (layerImage == null)
                {
                    continue;
                }

                var sprite = i < sprites.Count ? sprites[i] : null;
                layerImage.sprite = sprite;

                if (_disableLayerIfSpriteMissing)
                {
                    layerImage.enabled = sprite != null;
                }

                if (_enableSeamlessTiling)
                {
                    var state = EnsureLayerTileState(layerImage);
                    ApplySourceToTiles(state);
                    RefreshLayerTiles(state);
                }
            }

            if (!_enableSeamlessTiling)
            {
                ClearAllTiles();
            }
            else
            {
                RemoveOrphanTileStates();
            }

            _currentSetIndex = setIndex;
            _manualSetIndex = setIndex;
            return true;
        }

        public bool SelectSet(int setIndex, bool applyImmediately = true)
        {
            if (_sets == null || _sets.Count == 0)
            {
                return false;
            }

            _selectionMode = SetSelectionMode.Manual;
            _manualSetIndex = Mathf.Clamp(setIndex, 0, _sets.Count - 1);

            return !applyImmediately || ApplySet(_manualSetIndex);
        }

        [ContextMenu("Refresh Seamless Tiling")]
        public void EnsureTilingPrepared()
        {
            if (!_enableSeamlessTiling)
            {
                ClearAllTiles();
                return;
            }

            for (var i = 0; i < _layers.Count; i++)
            {
                var layerImage = _layers[i];
                if (layerImage == null)
                {
                    continue;
                }

                var state = EnsureLayerTileState(layerImage);
                ApplySourceToTiles(state);
                RefreshLayerTiles(state);
            }

            RemoveOrphanTileStates();
        }

        private LayerTileState EnsureLayerTileState(Image sourceImage)
        {
            for (var i = 0; i < _tileStates.Count; i++)
            {
                if (_tileStates[i].Source == sourceImage)
                {
                    return _tileStates[i];
                }
            }

            var sourceRect = sourceImage.rectTransform;
            var state = new LayerTileState(sourceImage, sourceRect);
            _tileStates.Add(state);
            return state;
        }

        private void RefreshLayerTiles(LayerTileState state)
        {
            if (state == null || state.Source == null || state.SourceRect == null)
            {
                return;
            }

            BuildRequiredOffsets(_requiredOffsets);
            EnsureTileEntries(state, _requiredOffsets);

            GetTileStepVectors(state.SourceRect, out var stepX, out var stepY);
            for (var i = 0; i < state.Tiles.Count; i++)
            {
                var tile = state.Tiles[i];
                if (tile.Image == null || tile.Rect == null)
                {
                    continue;
                }

                CopyRectLayout(state.SourceRect, tile.Rect);
                tile.Rect.position = state.SourceRect.position
                    + (stepX * tile.Offset.x)
                    + (stepY * tile.Offset.y);
                CopyImageVisuals(state.Source, tile.Image);
            }
        }

        private void EnsureTileEntries(LayerTileState state, IReadOnlyList<Vector2Int> requiredOffsets)
        {
            for (var i = state.Tiles.Count - 1; i >= 0; i--)
            {
                if (ContainsOffset(requiredOffsets, state.Tiles[i].Offset))
                {
                    continue;
                }

                state.Tiles[i].Destroy();
                state.Tiles.RemoveAt(i);
            }

            for (var i = 0; i < requiredOffsets.Count; i++)
            {
                var offset = requiredOffsets[i];
                if (TryFindTile(state, offset, out _))
                {
                    continue;
                }

                var entry = CreateTile(state.SourceRect, offset);
                state.Tiles.Add(entry);
            }
        }

        private TileEntry CreateTile(RectTransform sourceRect, Vector2Int offset)
        {
            var clone = new GameObject(
                $"{TileObjectPrefix}{offset.x}_{offset.y}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            var cloneRect = clone.GetComponent<RectTransform>();
            var tileParent = sourceRect.parent != null ? sourceRect.parent : sourceRect;
            cloneRect.SetParent(tileParent, false);
            cloneRect.SetSiblingIndex(sourceRect.GetSiblingIndex());

            var cloneImage = clone.GetComponent<Image>();
            cloneImage.raycastTarget = false;

            return new TileEntry(offset, cloneImage, cloneRect);
        }

        private void ApplySourceToTiles(LayerTileState state)
        {
            if (state == null || state.Source == null)
            {
                return;
            }

            for (var i = 0; i < state.Tiles.Count; i++)
            {
                var tile = state.Tiles[i];
                if (tile.Image == null)
                {
                    continue;
                }

                CopyImageVisuals(state.Source, tile.Image);
            }
        }

        private void RemoveOrphanTileStates()
        {
            for (var i = _tileStates.Count - 1; i >= 0; i--)
            {
                var state = _tileStates[i];
                if (state.Source != null && ContainsLayer(state.Source))
                {
                    continue;
                }

                state.DestroyAllTiles();
                _tileStates.RemoveAt(i);
            }
        }

        private void ClearAllTiles()
        {
            for (var i = 0; i < _tileStates.Count; i++)
            {
                _tileStates[i].DestroyAllTiles();
            }

            _tileStates.Clear();
        }

        private bool ContainsLayer(Image image)
        {
            for (var i = 0; i < _layers.Count; i++)
            {
                if (_layers[i] == image)
                {
                    return true;
                }
            }

            return false;
        }

        private void BuildRequiredOffsets(List<Vector2Int> targetOffsets)
        {
            targetOffsets.Clear();

            if (!_tileX && !_tileY)
            {
                return;
            }

            if (_tileX && _tileY)
            {
                for (var y = -1; y <= 1; y++)
                {
                    for (var x = -1; x <= 1; x++)
                    {
                        if (x == 0 && y == 0)
                        {
                            continue;
                        }

                        targetOffsets.Add(new Vector2Int(x, y));
                    }
                }

                return;
            }

            if (_tileX)
            {
                targetOffsets.Add(new Vector2Int(-1, 0));
                targetOffsets.Add(new Vector2Int(1, 0));
            }

            if (_tileY)
            {
                targetOffsets.Add(new Vector2Int(0, -1));
                targetOffsets.Add(new Vector2Int(0, 1));
            }
        }

        private static bool TryFindTile(LayerTileState state, Vector2Int offset, out TileEntry tile)
        {
            for (var i = 0; i < state.Tiles.Count; i++)
            {
                if (state.Tiles[i].Offset == offset)
                {
                    tile = state.Tiles[i];
                    return true;
                }
            }

            tile = null;
            return false;
        }

        private static bool ContainsOffset(IReadOnlyList<Vector2Int> offsets, Vector2Int offset)
        {
            for (var i = 0; i < offsets.Count; i++)
            {
                if (offsets[i] == offset)
                {
                    return true;
                }
            }

            return false;
        }

        private static void CopyImageVisuals(Image source, Image target)
        {
            target.sprite = source.sprite;
            target.color = source.color;
            target.material = source.material;
            target.type = source.type;
            target.preserveAspect = source.preserveAspect;
            target.fillCenter = source.fillCenter;
            target.fillMethod = source.fillMethod;
            target.fillAmount = source.fillAmount;
            target.fillClockwise = source.fillClockwise;
            target.fillOrigin = source.fillOrigin;
            target.enabled = source.enabled && source.sprite != null;
            target.raycastTarget = false;
        }

        private void GetTileStepVectors(RectTransform sourceRect, out Vector3 stepX, out Vector3 stepY)
        {
            sourceRect.GetWorldCorners(_worldCornersBuffer);
            stepX = _worldCornersBuffer[3] - _worldCornersBuffer[0];
            stepY = _worldCornersBuffer[1] - _worldCornersBuffer[0];
        }

        private static void CopyRectLayout(RectTransform source, RectTransform target)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.sizeDelta = source.sizeDelta;
            target.localScale = source.localScale;
            target.localRotation = source.localRotation;
        }

        [Serializable]
        private sealed class TileEntry
        {
            public TileEntry(Vector2Int offset, Image image, RectTransform rect)
            {
                Offset = offset;
                Image = image;
                Rect = rect;
            }

            public Vector2Int Offset { get; }
            public Image Image { get; }
            public RectTransform Rect { get; }

            public void Destroy()
            {
                if (Rect == null)
                {
                    return;
                }

                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(Rect.gameObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(Rect.gameObject);
                }
            }
        }

        [Serializable]
        private sealed class LayerTileState
        {
            public LayerTileState(Image source, RectTransform sourceRect)
            {
                Source = source;
                SourceRect = sourceRect;
            }

            public Image Source { get; }
            public RectTransform SourceRect { get; }
            public List<TileEntry> Tiles { get; } = new();

            public void DestroyAllTiles()
            {
                for (var i = Tiles.Count - 1; i >= 0; i--)
                {
                    Tiles[i].Destroy();
                }

                Tiles.Clear();
            }
        }
    }
}
