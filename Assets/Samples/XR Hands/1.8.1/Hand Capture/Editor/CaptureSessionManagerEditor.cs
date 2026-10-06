#if TEXT_MESH_PRO_PRESENT || (UGUI_2_0_PRESENT && UNITY_6000_0_OR_NEWER)
using UnityEditor.XR.Hands.Capture;
using UnityEngine;
using UnityEngine.XR.Hands.Samples.Capture;

namespace UnityEditor.XR.Hands.Samples.Capture
{
    [CustomEditor(typeof(CaptureSessionManager))]
    public class XRHandCaptureEditorLauncher : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(10);

            if (GUILayout.Button("Open XR Hand Capture Editor"))
            {
                XRHandCaptureEditorWindow.ShowWindow();
            }
        }
    }
}
#endif
