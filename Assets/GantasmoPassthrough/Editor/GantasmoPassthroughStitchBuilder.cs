using Gantasmo.Passthrough;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gantasmo.Passthrough.EditorTools
{
    /// <summary>
    /// One-click setup for the passthrough stitch. Drops a configured
    /// <see cref="GantasmoPassthroughStitch"/> into the open scene and wires the shader,
    /// so there is no manual component-adding or reference-dragging. The stitched
    /// RenderTexture, the two PassthroughCameraAccess cameras, and the in-headset preview
    /// quad are created by the component itself at runtime (they are live objects driven by
    /// the cameras, so they only exist while playing / on-device, not as edit-time assets).
    /// </summary>
    public static class GantasmoPassthroughStitchBuilder
    {
        [MenuItem("GANTASMO/Add Passthrough Stitch To Scene")]
        public static void AddToScene()
        {
            var existing = Object.FindAnyObjectByType<GantasmoPassthroughStitch>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing);
                Debug.Log("[GANTASMO] A Passthrough Stitch is already in this scene — selected it. " +
                          "Build And Run to the headset to see the stitched preview.");
                return;
            }

            var go = new GameObject("GANTASMO Passthrough Stitch");
            Undo.RegisterCreatedObjectUndo(go, "Add Passthrough Stitch");
            go.transform.position = new Vector3(0f, 1.2f, 0f);

            var stitch = go.AddComponent<GantasmoPassthroughStitch>();
            var shader = Shader.Find("Gantasmo/PassthroughStitch");
            if (shader != null)
            {
                stitch.stitchShader = shader;
            }
            else
            {
                Debug.LogWarning("[GANTASMO] 'Gantasmo/PassthroughStitch' shader not found yet — it will resolve " +
                                 "once the project recompiles. Reopen this menu if the reference stays empty.");
            }

            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("[GANTASMO] Added the Passthrough Stitch. On Build And Run it creates the two passthrough " +
                      "cameras, the stitched 16:9 RenderTexture (GantasmoPassthroughStitch.OutputTexture), and a " +
                      "preview quad in front of you. Tune focalDistance / blendWidth / outputHorizontalFovDeg / flipY " +
                      "on the component. That OutputTexture is the handoff point for the encoder/stream stage.");
        }
    }
}
