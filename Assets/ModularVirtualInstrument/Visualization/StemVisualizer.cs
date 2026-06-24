using UnityEngine;

namespace ModularVirtualInstrument
{
    /// <summary>
    /// Handles visual feedback for a single stem processor.
    /// Creates neon wireframe cubes with axis indicators and hand tracking visuals.
    /// </summary>
    public class StemVisualizer : MonoBehaviour
    {
        [Header("References")]
        public StemProcessor stemProcessor;
        
        [Header("Wireframe Settings")]
        [Tooltip("Show/hide wireframe cube visualization")]
        public bool showWireframe = false;
        
        [Tooltip("Material for wireframe rendering")]
        public Material wireframeMaterial;
        
        [Tooltip("Line width for wireframe")]
        [Range(0.001f, 0.1f)]
        public float lineWidth = 0.01f;
        
        [Header("Neon Glow")]
        [Tooltip("Emission intensity")]
        [Range(0f, 10f)]
        public float emissionIntensity = 2f;
        
        [Tooltip("Glow pulse speed")]
        [Range(0f, 5f)]
        public float pulseSpeed = 1f;
        
        [Header("Axis Indicators")]
        public bool showAxisIndicators = true;
        public float axisIndicatorLength = 0.2f;
        
        [Header("Hand Visualization")]
        public bool showHandTrail = true;
        public GameObject handTrailPrefab;
        
        [Header("Debug Visualization")]
        public bool showEffectValues = true;
        public bool showCollisionIndicator = true;
        public Color collisionColor = Color.yellow;
        
        [Header("Freeze State")]
        public Color frozenColor = Color.cyan;
        public float frozenPulseSpeed = 0.3f;
        private bool isFrozen = false;
        private float normalPulseSpeed;
        
        // Internal state
        private LineRenderer wireframeCube;   // single-stroke cube (replaces 12 line renderers)
        private LineRenderer[] axisLines;
        private GameObject handTrailInstance;
        private bool isInitialized = false;

        // One shared, additive line material reused by every stem. Per-stem color comes
        // from the LineRenderer vertex colors, so sharing one material is safe and avoids
        // allocating a material per stem (and per the 12-renderers-per-stem old design).
        private static Material s_sharedLineMaterial;

        // Single-stroke traversal of a cube's 12 edges (16 points). A cube graph has no
        // Eulerian path, so 3 edges are retraced; the overlap is invisible. One renderer
        // instead of twelve.
        private static readonly int[] CubeStrokeOrder =
        {
            0, 1, 2, 3, 0, 4, 5, 1, 5, 6, 2, 6, 7, 3, 7, 4
        };

        /// <summary>
        /// A URP-friendly additive line material shared by all stems. Falls back across
        /// shaders so it survives build stripping on Quest instead of rendering magenta.
        /// </summary>
        private static Material GetSharedLineMaterial()
        {
            if (s_sharedLineMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                                ?? Shader.Find("Sprites/Default")
                                ?? Shader.Find("Unlit/Color");
                s_sharedLineMaterial = new Material(shader != null ? shader : Shader.Find("Hidden/InternalErrorShader"))
                {
                    name = "MVI_WireframeLine (Shared)"
                };
            }
            return s_sharedLineMaterial;
        }
        
        /// <summary>
        /// Initialize visualizer with stem processor
        /// </summary>
        public void Initialize(StemProcessor processor)
        {
            stemProcessor = processor;
            
            if (stemProcessor == null || stemProcessor.stemData == null)
            {
                Debug.LogWarning("StemVisualizer: Cannot initialize without valid StemProcessor");
                return;
            }
            
            // Store normal pulse speed
            normalPulseSpeed = pulseSpeed;

            // Use the assigned material, otherwise the shared additive line material
            // (null-safe / stripping-safe). Color is per-stem via vertex colors.
            if (wireframeMaterial == null)
            {
                wireframeMaterial = GetSharedLineMaterial();
            }

            // Idempotent: tear down any visuals from a prior init so re-initializing
            // replaces them cleanly instead of stacking duplicate GameObjects.
            ClearVisuals();

            CreateWireframeCube();
            CreateAxisIndicators();

            isInitialized = true;
        }

        /// <summary>
        /// Destroy any existing wireframe/axis line GameObjects so Initialize can run again safely.
        /// </summary>
        private void ClearVisuals()
        {
            if (wireframeCube != null)
            {
                DestroyLineObject(wireframeCube);
                wireframeCube = null;
            }

            if (axisLines != null)
            {
                for (int i = 0; i < axisLines.Length; i++)
                {
                    if (axisLines[i] != null) DestroyLineObject(axisLines[i]);
                }
                axisLines = null;
            }
        }

        private void DestroyLineObject(LineRenderer lr)
        {
            if (lr == null || lr.gameObject == null) return;
            if (Application.isPlaying) Destroy(lr.gameObject);
            else DestroyImmediate(lr.gameObject);
        }

        private void CreateWireframeCube()
        {
            Vector3 size = stemProcessor.stemData.cubeSize;
            Vector3 halfSize = size * 0.5f;

            // 8 cube corners
            Vector3[] vertices = new Vector3[8]
            {
                new Vector3(-halfSize.x, -halfSize.y, -halfSize.z),
                new Vector3( halfSize.x, -halfSize.y, -halfSize.z),
                new Vector3( halfSize.x, -halfSize.y,  halfSize.z),
                new Vector3(-halfSize.x, -halfSize.y,  halfSize.z),
                new Vector3(-halfSize.x,  halfSize.y, -halfSize.z),
                new Vector3( halfSize.x,  halfSize.y, -halfSize.z),
                new Vector3( halfSize.x,  halfSize.y,  halfSize.z),
                new Vector3(-halfSize.x,  halfSize.y,  halfSize.z),
            };

            GameObject lineObj = new GameObject("WireframeCube");
            lineObj.transform.SetParent(transform);
            lineObj.transform.localPosition = Vector3.zero;
            lineObj.transform.localRotation = Quaternion.identity;
            lineObj.transform.localScale = Vector3.one;

            wireframeCube = lineObj.AddComponent<LineRenderer>();
            wireframeCube.useWorldSpace = false;
            wireframeCube.loop = false;
            wireframeCube.positionCount = CubeStrokeOrder.Length;
            wireframeCube.startWidth = lineWidth;
            wireframeCube.endWidth = lineWidth;
            wireframeCube.numCapVertices = 1;
            wireframeCube.numCornerVertices = 1;
            wireframeCube.sharedMaterial = wireframeMaterial;
            wireframeCube.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            wireframeCube.receiveShadows = false;

            Color wireColor = stemProcessor.stemData.themeColor;
            wireframeCube.startColor = wireColor;
            wireframeCube.endColor = wireColor;

            for (int i = 0; i < CubeStrokeOrder.Length; i++)
            {
                wireframeCube.SetPosition(i, vertices[CubeStrokeOrder[i]]);
            }

            wireframeCube.gameObject.SetActive(showWireframe);
        }
        
        private void CreateAxisIndicators()
        {
            if (!showAxisIndicators) return;
            
            // Create 3 lines for X, Y, Z axes
            axisLines = new LineRenderer[3];
            Color[] axisColors = { Color.red, Color.green, Color.blue };
            string[] axisNames = { "X-Axis", "Y-Axis", "Z-Axis" };
            
            for (int i = 0; i < 3; i++)
            {
                GameObject lineObj = new GameObject($"AxisIndicator_{axisNames[i]}");
                lineObj.transform.SetParent(transform);
                lineObj.transform.localPosition = Vector3.zero;
                lineObj.transform.localRotation = Quaternion.identity;
                
                LineRenderer lr = lineObj.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.startWidth = lineWidth * 1.5f;
                lr.endWidth = 0f;
                lr.useWorldSpace = false;
                
                lr.startColor = axisColors[i];
                lr.endColor = axisColors[i];
                lr.numCapVertices = 1;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.sharedMaterial = wireframeMaterial;

                axisLines[i] = lr;
            }
            
            UpdateAxisIndicators(Vector3.one * 0.5f); // Default centered position
        }
        
        private void UpdateAxisIndicators(Vector3 normalizedPosition)
        {
            if (!showAxisIndicators || axisLines == null) return;
            
            Vector3 size = stemProcessor.stemData.cubeSize;
            Vector3 halfSize = size * 0.5f;
            
            // Convert normalized to local position
            Vector3 localPos = new Vector3(
                (normalizedPosition.x - 0.5f) * size.x,
                (normalizedPosition.y - 0.5f) * size.y,
                (normalizedPosition.z - 0.5f) * size.z
            );
            
            // X-axis indicator (red)
            axisLines[0].SetPosition(0, localPos);
            axisLines[0].SetPosition(1, localPos + Vector3.right * axisIndicatorLength);
            
            // Y-axis indicator (green)
            axisLines[1].SetPosition(0, localPos);
            axisLines[1].SetPosition(1, localPos + Vector3.up * axisIndicatorLength);
            
            // Z-axis indicator (blue)
            axisLines[2].SetPosition(0, localPos);
            axisLines[2].SetPosition(1, localPos + Vector3.forward * axisIndicatorLength);
        }
        
        private void Update()
        {
            if (!isInitialized) return;

            // Sync visibility only when it actually changes (avoids redundant SetActive
            // calls every frame).
            if (wireframeCube != null && wireframeCube.gameObject.activeSelf != showWireframe)
            {
                wireframeCube.gameObject.SetActive(showWireframe);
            }

            if (axisLines != null)
            {
                bool axisVisible = showWireframe && showAxisIndicators;
                for (int i = 0; i < axisLines.Length; i++)
                {
                    LineRenderer line = axisLines[i];
                    if (line != null && line.gameObject.activeSelf != axisVisible)
                    {
                        line.gameObject.SetActive(axisVisible);
                    }
                }
            }

            // When wireframes are hidden (the default), do no per-frame work at all.
            if (!showWireframe) return;

            UpdatePulseEffect();
            UpdateAxisColors();
        }
        
        private void UpdatePulseEffect()
        {
            if (wireframeCube == null) return;

            // Shader-agnostic pulse: modulate the line's vertex-color brightness. No
            // material mutation, so the one shared material stays shared (no per-stem
            // material instances, no leak).
            float pulse = Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f;   // 0..1
            Color baseColor = isFrozen ? frozenColor : stemProcessor.stemData.themeColor;
            Color pulsed = baseColor * (0.6f + 0.4f * pulse);
            pulsed.a = baseColor.a;

            wireframeCube.startColor = pulsed;
            wireframeCube.endColor = pulsed;
        }
        
        private void UpdateAxisColors()
        {
            // Only update axis colors if both wireframes and axis indicators are visible
            if (!showWireframe || !showAxisIndicators || axisLines == null) return;
            
            // Update colors based on effect values
            if (stemProcessor.xAxis.effect != null)
            {
                axisLines[0].startColor = stemProcessor.xAxis.GetEffectColor();
            }
            
            if (stemProcessor.yAxis.effect != null)
            {
                axisLines[1].startColor = stemProcessor.yAxis.GetEffectColor();
            }
            
            if (stemProcessor.zAxis.effect != null)
            {
                axisLines[2].startColor = stemProcessor.zAxis.GetEffectColor();
            }
        }
        
        /// <summary>
        /// Called when hand enters the interaction volume
        /// </summary>
        public void OnHandEnter()
        {
            // Brighten wireframe
            if (wireframeCube != null)
            {
                emissionIntensity *= 1.5f;
            }
            
            // Create hand trail if needed
            if (showHandTrail && handTrailPrefab != null && handTrailInstance == null)
            {
                handTrailInstance = Instantiate(handTrailPrefab, transform);
            }
        }
        
        /// <summary>
        /// Called when hand position updates
        /// </summary>
        public void OnHandUpdate(Vector3 normalizedPosition)
        {
            UpdateAxisIndicators(normalizedPosition);
        }
        
        /// <summary>
        /// Called when hand exits the interaction volume
        /// </summary>
        public void OnHandExit()
        {
            // Reset emission
            emissionIntensity = stemProcessor.stemData.emissionIntensity;
            
            // Destroy hand trail
            if (handTrailInstance != null)
            {
                Destroy(handTrailInstance);
                handTrailInstance = null;
            }
        }
        
        /// <summary>
        /// Set the frozen state for visual feedback
        /// </summary>
        public void SetFrozen(bool frozen)
        {
            isFrozen = frozen;
            
            // Change pulse speed when frozen to indicate state
            pulseSpeed = frozen ? frozenPulseSpeed : normalPulseSpeed;
        }
        
        /// <summary>
        /// Toggle wireframe visibility
        /// </summary>
        public void SetWireframeVisible(bool visible)
        {
            showWireframe = visible;

            if (wireframeCube != null)
            {
                wireframeCube.gameObject.SetActive(visible);
            }

            if (axisLines != null)
            {
                foreach (var line in axisLines)
                {
                    if (line != null && line.gameObject != null)
                    {
                        line.gameObject.SetActive(visible && showAxisIndicators);
                    }
                }
            }
        }
        
#if UNITY_EDITOR
        // Editor/desktop debug overlay only. IMGUI does not render to a headset and
        // costs a WorldToScreenPoint + GUI pass per stem per frame, so it is compiled
        // out of player builds entirely.
        private void OnGUI()
        {
            if (!showEffectValues || stemProcessor == null || !stemProcessor.isHandInside)
                return;

            Camera cam = Camera.main;
            if (cam == null) return;

            // Show effect values as overlay when hand is inside
            Vector3 screenPos = cam.WorldToScreenPoint(transform.position + Vector3.up * 0.5f);
            if (screenPos.z > 0)
            {
                Rect labelRect = new Rect(screenPos.x - 100, Screen.height - screenPos.y - 100, 200, 100);
                
                string effectInfo = $"<b>{stemProcessor.stemData.stemName}</b>";
                if (isFrozen)
                {
                    effectInfo += " <color=cyan>[FROZEN]</color>";
                }
                effectInfo += "\n";
                effectInfo += $"X: {stemProcessor.xAxis.currentValue:F2} ({GetEffectName(stemProcessor.xAxis)})\n";
                effectInfo += $"Y: {stemProcessor.yAxis.currentValue:F2} ({GetEffectName(stemProcessor.yAxis)})\n";
                effectInfo += $"Z: {stemProcessor.zAxis.currentValue:F2} ({GetEffectName(stemProcessor.zAxis)})";
                
                GUI.Box(labelRect, "");
                GUI.Label(labelRect, effectInfo);
            }
        }
        
        private string GetEffectName(AxisEffectSlot slot)
        {
            if (slot.effect == null) return "None";
            return slot.effect.name.Replace("Effect", "");
        }
#endif

        private void OnDrawGizmos()
        {
            if (stemProcessor == null || stemProcessor.stemData == null)
                return;
            
            // Draw collision indicator
            if (showCollisionIndicator && stemProcessor.isHandInside)
            {
                Gizmos.color = collisionColor;
                Gizmos.DrawWireSphere(transform.position, stemProcessor.stemData.cubeSize.magnitude * 0.6f);
            }
        }
    }
}
