using UnityEngine;

namespace Gantasmo.XrViz
{
    /// <summary>
    /// Draws theDAW's live waveform as a 3D ribbon via a LineRenderer, updated
    /// from <see cref="XrVizReceiver"/> each time a frame arrives (~30 Hz). The
    /// LineRenderer is local-space, so move or rotate this GameObject to place
    /// the waveform in the scene (the design ruleset wants it up top).
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    [AddComponentMenu("GANTASMO/Visuals/XR Waveform Ribbon")]
    public class XrWaveformRibbon : MonoBehaviour
    {
        [Tooltip("Source of decoded waveform data. Auto-found when empty.")]
        public XrVizReceiver receiver;

        [Tooltip("Total ribbon width in meters (local X).")]
        public float width = 1.2f;

        [Tooltip("Amplitude scale in meters (local Y) for a full-scale sample.")]
        public float height = 0.18f;

        LineRenderer _lr;

        void Awake()
        {
            _lr = GetComponent<LineRenderer>();
            _lr.useWorldSpace = false;
            if (receiver == null) receiver = FindAnyObjectByType<XrVizReceiver>();
        }

        void OnEnable()
        {
            if (receiver != null) receiver.WaveformUpdated += Render;
        }

        void OnDisable()
        {
            if (receiver != null) receiver.WaveformUpdated -= Render;
        }

        void Render()
        {
            float[] w = receiver.Waveform;
            int n = w.Length;
            if (n < 2) return;
            if (_lr.positionCount != n) _lr.positionCount = n;
            float inv = 1f / (n - 1);
            for (int i = 0; i < n; i++)
            {
                float x = (i * inv - 0.5f) * width;
                _lr.SetPosition(i, new Vector3(x, w[i] * height, 0f));
            }
        }
    }
}
