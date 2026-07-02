using LittlePeopleWorld.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LittlePeopleWorld.Editor
{
    public static class LittlePeopleWorldSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/LittlePeopleWorldMvp.unity";

        [MenuItem("Little People World/Create MVP Scene")]
        public static void CreateMvpScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;
            camera.backgroundColor = new Color(0.015f, 0.014f, 0.02f, 1f);

            var worldObject = new GameObject("Little People World");
            worldObject.AddComponent<LittlePeopleWorldBootstrap>();

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        }
    }
}
