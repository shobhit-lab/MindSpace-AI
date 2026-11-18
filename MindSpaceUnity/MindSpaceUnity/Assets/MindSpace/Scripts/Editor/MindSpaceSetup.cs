#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MindSpace.EditorTools
{
    /// Menu: MindSpace > Setup Scene — adds XR Origin + MindSpaceApp and allows HTTP for the LAN server.
    public static class MindSpaceSetup
    {
        [MenuItem("MindSpace/Setup Scene")]
        public static void Setup()
        {
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            var oldCam = GameObject.Find("Main Camera");
            if (oldCam && oldCam.transform.parent == null) Object.DestroyImmediate(oldCam);
            if (!Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>())
            {
                if (!EditorApplication.ExecuteMenuItem("GameObject/XR/XR Origin (VR)"))
                    Debug.LogWarning("MindSpace: add one via GameObject > XR > XR Origin (VR), then re-run.");
            }
            if (!Object.FindFirstObjectByType<MindSpaceApp>())
            {
                var go = new GameObject("MindSpace"); go.AddComponent<MindSpaceApp>();
                Selection.activeGameObject = go;
            }
            Debug.Log("MindSpace: scene ready. Set Server Url on the MindSpace object, then press Play.");
        }
    }
}
#endif
