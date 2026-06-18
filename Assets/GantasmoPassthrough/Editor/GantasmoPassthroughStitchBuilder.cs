using Gantasmo.Passthrough;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gantasmo.Passthrough.EditorTools
{
    /// <summary>
    /// One-click setup for the passthrough stitch + its VJ stream. Drops a configured
    /// <see cref="GantasmoPassthroughStitch"/> AND a <see cref="GantasmoStitchStreamer"/> into the
    /// open scene and wires the shader + the streamer's stitch reference, so there is no manual
    /// component-adding or reference-dragging. The stitched RenderTexture, the two
    /// PassthroughCameraAccess cameras, and the in-headset preview quad are created by the stitch
    /// component itself at runtime (live objects driven by the cameras, so they only exist while
    /// playing / on-device, not as edit-time assets). The streamer MediaCodec-encodes that
    /// RenderTexture and pushes it to theDAW's VJ (the STITCH source) over adb reverse.
    ///
    /// Idempotent: re-running the menu on a scene that already has the stitch adds the streamer if
    /// it is missing (so older scenes built before the streamer existed get upgraded in one click).
    /// </summary>
    public static class GantasmoPassthroughStitchBuilder
    {
        [MenuItem("GANTASMO/Add Passthrough Stitch To Scene")]
        public static void AddToScene()
        {
            var existing = Object.FindAnyObjectByType<GantasmoPassthroughStitch>();
            if (existing != null)
            {
                // Upgrade path: ensure the streamer is present on the existing stitch.
                var streamer = existing.GetComponent<GantasmoStitchStreamer>();
                if (streamer == null)
                {
                    streamer = Undo.AddComponent<GantasmoStitchStreamer>(existing.gameObject);
                    streamer.stitch = existing;
                    EditorSceneManager.MarkSceneDirty(existing.gameObject.scene);
                    Debug.Log("[GANTASMO] Added the missing Passthrough Stitch STREAMER to the existing stitch. " +
                              "Build And Run, then pick the STITCH source in theDAW's VJ.");
                }
                else if (streamer.stitch == null)
                {
                    streamer.stitch = existing;
                    EditorSceneManager.MarkSceneDirty(existing.gameObject.scene);
                }

                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing);
                Debug.Log("[GANTASMO] A Passthrough Stitch is already in this scene — selected it. " +
                          "Build And Run to the headset to see the stitched preview and stream it to the VJ.");
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

            // Assign the composite shader as a real reference so it is never stripped from a
            // build (a Shader.Find-only shader can be). Drives the augmented (3D) composite.
            var composite = Shader.Find("Gantasmo/PassthroughComposite");
            if (composite != null) stitch.compositeShader = composite;

            // Wire the streamer that carries OutputTexture to theDAW's VJ STITCH source.
            var streamerNew = go.AddComponent<GantasmoStitchStreamer>();
            streamerNew.stitch = stitch;

            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("[GANTASMO] Added the Passthrough Stitch + Streamer. On Build And Run it creates the two " +
                      "passthrough cameras, the stitched 16:9 RenderTexture (GantasmoPassthroughStitch.OutputTexture), " +
                      "and a preview quad; the streamer H.264-encodes that texture to theDAW (pick the STITCH source " +
                      "in the VJ). Tune focalDistance / blendWidth / outputHorizontalFovDeg / flipY on the stitch, and " +
                      "streamResolution / streamFps / bitrateKbps / swapUV on the streamer.");
        }
    }
}
