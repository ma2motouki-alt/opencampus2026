using UnityEngine;

namespace LittlePeopleWorld.Unity
{
    [DisallowMultipleComponent]
    public sealed class LittlePeopleWorldBootstrap : MonoBehaviour
    {
        void Awake()
        {
            if (Camera.main == null)
            {
                var cameraObject = new GameObject("Main Camera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.015f, 0.014f, 0.02f, 1f);
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.transform.rotation = Quaternion.identity;
            }

            if (GetComponent<LittlePeopleWorld.Input.MouseInputProviderBehaviour>() == null)
            {
                gameObject.AddComponent<LittlePeopleWorld.Input.MouseInputProviderBehaviour>();
            }

            if (GetComponent<LittlePeopleWorldController>() == null)
            {
                gameObject.AddComponent<LittlePeopleWorldController>();
            }
        }
    }
}
