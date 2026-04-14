using System.Collections.Generic;
using System.Linq;
using Interactme.Parallax;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Interactme.Parallax.Editor
{
    public sealed class ParallaxStudioWindow : EditorWindow
    {
        private const string UiControllerObjectName = "ParallaxImageBackground";
        private const string WorldControllerObjectName = "BackgroundParallax";

        private Transform _spawnParent;
        private ParallaxImageBackgroundController _uiController;
        private BackgroundParallaxController _worldController;
        private string _newSetId = "Set";

        [MenuItem("Tools/Parallax Studio")]
        public static void Open()
        {
            var window = GetWindow<ParallaxStudioWindow>("Parallax Studio");
            window.minSize = new Vector2(420f, 360f);
            window.Show();
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawCreateSection();
            DrawUiSection();
            DrawWorldSection();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Parallax Studio", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Window helper for configuring parallax controllers without editing serialized lists manually.",
                MessageType.Info);
        }

        private void DrawCreateSection()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Create", EditorStyles.boldLabel);

            _spawnParent = (Transform)EditorGUILayout.ObjectField(
                "Parent",
                _spawnParent,
                typeof(Transform),
                true);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create UI Controller"))
                {
                    _uiController = CreateController<ParallaxImageBackgroundController>(UiControllerObjectName);
                    Selection.activeObject = _uiController;
                }

                if (GUILayout.Button("Create World Controller"))
                {
                    _worldController = CreateController<BackgroundParallaxController>(WorldControllerObjectName);
                    Selection.activeObject = _worldController;
                }
            }
        }

        private void DrawUiSection()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("UI Background Controller", EditorStyles.boldLabel);

            _uiController = (ParallaxImageBackgroundController)EditorGUILayout.ObjectField(
                "Controller",
                _uiController,
                typeof(ParallaxImageBackgroundController),
                true);

            if (_uiController == null)
            {
                EditorGUILayout.HelpBox("Assign ParallaxImageBackgroundController to configure UI layers and sets.", MessageType.None);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Selected Images As Layers"))
                {
                    AssignSelectedImagesAsLayers(_uiController);
                }

                if (GUILayout.Button("Clear Layers"))
                {
                    ClearArrayProperty(_uiController, "_layers");
                }
            }

            EditorGUILayout.Space(4f);
            _newSetId = EditorGUILayout.TextField("New Set Id", _newSetId);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Set From Selected Sprites"))
                {
                    AddSetFromSelectedSprites(_uiController, _newSetId);
                }

                if (GUILayout.Button("Apply Configured Set"))
                {
                    _uiController.ApplyConfiguredSet();
                    EditorUtility.SetDirty(_uiController);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Random"))
                {
                    _uiController.ApplyRandomSet();
                    EditorUtility.SetDirty(_uiController);
                }

                if (GUILayout.Button("Refresh Seamless Tiling"))
                {
                    _uiController.EnsureTilingPrepared();
                    EditorUtility.SetDirty(_uiController);
                }
            }

            if (GUILayout.Button("Select Controller In Inspector"))
            {
                Selection.activeObject = _uiController;
                EditorGUIUtility.PingObject(_uiController);
            }
        }

        private void DrawWorldSection()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("World Parallax Controller", EditorStyles.boldLabel);

            _worldController = (BackgroundParallaxController)EditorGUILayout.ObjectField(
                "Controller",
                _worldController,
                typeof(BackgroundParallaxController),
                true);

            if (_worldController == null)
            {
                EditorGUILayout.HelpBox("Assign BackgroundParallaxController to auto-fill layer targets.", MessageType.None);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Selected Transforms As Layers"))
                {
                    AssignSelectedTransformsAsWorldLayers(_worldController);
                }

                if (GUILayout.Button("Reinitialize"))
                {
                    _worldController.Initialize();
                    EditorUtility.SetDirty(_worldController);
                }
            }

            if (GUILayout.Button("Select Controller In Inspector"))
            {
                Selection.activeObject = _worldController;
                EditorGUIUtility.PingObject(_worldController);
            }
        }

        private T CreateController<T>(string objectName) where T : Component
        {
            var parent = ResolveParent();
            var gameObject = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(gameObject, $"Create {objectName}");

            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }

            return Undo.AddComponent<T>(gameObject);
        }

        private Transform ResolveParent()
        {
            if (_spawnParent != null)
            {
                return _spawnParent;
            }

            if (Selection.activeTransform != null)
            {
                return Selection.activeTransform;
            }

            return null;
        }

        private static void AssignSelectedImagesAsLayers(ParallaxImageBackgroundController controller)
        {
            var selectedImages = Selection.gameObjects
                .Select(go => go.GetComponent<Image>())
                .Where(image => image != null)
                .Distinct()
                .ToList();

            if (selectedImages.Count == 0)
            {
                EditorUtility.DisplayDialog("Parallax Studio", "Select one or more UI objects with Image component.", "OK");
                return;
            }

            Undo.RecordObject(controller, "Assign UI Layers");
            var serialized = new SerializedObject(controller);
            var layersProperty = serialized.FindProperty("_layers");
            layersProperty.arraySize = selectedImages.Count;

            for (var i = 0; i < selectedImages.Count; i++)
            {
                layersProperty.GetArrayElementAtIndex(i).objectReferenceValue = selectedImages[i];
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);
        }

        private static void AddSetFromSelectedSprites(ParallaxImageBackgroundController controller, string setId)
        {
            var selectedSprites = Selection.objects
                .OfType<Sprite>()
                .Distinct()
                .ToList();

            if (selectedSprites.Count == 0)
            {
                EditorUtility.DisplayDialog("Parallax Studio", "Select one or more Sprite assets in Project window.", "OK");
                return;
            }

            Undo.RecordObject(controller, "Add Sprite Set");

            var serialized = new SerializedObject(controller);
            var setsProperty = serialized.FindProperty("_sets");
            var newIndex = setsProperty.arraySize;
            setsProperty.InsertArrayElementAtIndex(newIndex);

            var newSetProperty = setsProperty.GetArrayElementAtIndex(newIndex);
            newSetProperty.FindPropertyRelative("_id").stringValue = string.IsNullOrWhiteSpace(setId)
                ? $"Set {newIndex + 1}"
                : setId.Trim();

            var layerSpritesProperty = newSetProperty.FindPropertyRelative("_layerSprites");
            layerSpritesProperty.arraySize = selectedSprites.Count;
            for (var i = 0; i < selectedSprites.Count; i++)
            {
                layerSpritesProperty.GetArrayElementAtIndex(i).objectReferenceValue = selectedSprites[i];
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);
        }

        private static void ClearArrayProperty(Object target, string propertyName)
        {
            Undo.RecordObject(target, $"Clear {propertyName}");
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            property.arraySize = 0;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private static void AssignSelectedTransformsAsWorldLayers(BackgroundParallaxController controller)
        {
            var selectedTransforms = Selection.transforms
                .Where(transform => transform != null)
                .Distinct()
                .ToList();

            if (selectedTransforms.Count == 0)
            {
                EditorUtility.DisplayDialog("Parallax Studio", "Select one or more transforms in Hierarchy.", "OK");
                return;
            }

            Undo.RecordObject(controller, "Assign World Layers");

            var serialized = new SerializedObject(controller);
            var layersProperty = serialized.FindProperty("_layers");
            layersProperty.arraySize = selectedTransforms.Count;

            for (var i = 0; i < selectedTransforms.Count; i++)
            {
                var layer = layersProperty.GetArrayElementAtIndex(i);
                layer.FindPropertyRelative("_enabled").boolValue = true;
                layer.FindPropertyRelative("_target").objectReferenceValue = selectedTransforms[i];

                var speed = Mathf.Clamp(0.12f + i * 0.1f, 0.05f, 2f);
                layer.FindPropertyRelative("_speedMultiplier").floatValue = speed;
            }

            serialized.ApplyModifiedProperties();
            controller.Initialize();
            EditorUtility.SetDirty(controller);
        }
    }
}
