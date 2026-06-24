using UnityEngine;

namespace ModularVirtualInstrument
{
    /// <summary>
    /// Enables mouse-based testing of MVI in editor play mode.
    /// Simulates hand tracking by raycasting mouse position into stems.
    /// </summary>
    public class MouseInputSimulator : MonoBehaviour
    {
        [Header("References")]
        public ModularSynthController synthController;
        public Camera targetCamera;
        
        [Header("Input Settings")]
        [Tooltip("Mouse button to use for interaction (0=Left, 1=Right, 2=Middle)")]
        public int mouseButton = 0;
        
        [Tooltip("Maximum raycast distance")]
        public float raycastDistance = 100f;
        
        [Header("Debug")]
        public bool showDebugLogs = true;
        public bool showDebugRays = true;
        
        private StemProcessor currentStem = null;
        private Vector3 lastHandPosition = Vector3.zero;
        
        private void Start()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
            
            if (synthController == null)
            {
                synthController = FindFirstObjectByType<ModularSynthController>();
            }
            
            if (showDebugLogs)
            {
                Debug.Log("[Mouse Simulator] Initialized - Hold mouse button and move over stems to interact");
            }
        }
        
        private void Update()
        {
            if (targetCamera == null) return;
            
            bool isInteracting = Input.GetMouseButton(mouseButton);
            
            if (isInteracting)
            {
                // Cast ray from mouse
                Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
                
                if (showDebugRays)
                {
                    Debug.DrawRay(ray.origin, ray.direction * raycastDistance, Color.yellow);
                }
                
                // Check for stem intersection
                StemProcessor hitStem = FindStemAtRay(ray);
                
                if (hitStem != null)
                {
                    // Calculate intersection point
                    if (RayIntersectsBounds(ray, hitStem.interactionBounds, out Vector3 hitPoint))
                    {
                        lastHandPosition = hitPoint;
                        
                        // Simulate hand enter/update
                        if (currentStem != hitStem)
                        {
                            // Exit previous stem
                            if (currentStem != null)
                            {
                                currentStem.OnHandExit();
                                if (showDebugLogs)
                                {
                                    Debug.Log($"[Mouse] Exited: {currentStem.stemData.stemName}");
                                }
                            }
                            
                            // Enter new stem
                            currentStem = hitStem;
                            currentStem.OnHandEnter(lastHandPosition);
                            
                            if (showDebugLogs)
                            {
                                Debug.Log($"[Mouse] Entered: {currentStem.stemData.stemName}");
                            }
                        }
                        
                        // Update hand position
                        currentStem.OnHandUpdate(lastHandPosition);
                    }
                }
                else
                {
                    // No stem hit - exit current if any
                    if (currentStem != null)
                    {
                        currentStem.OnHandExit();
                        if (showDebugLogs)
                        {
                            Debug.Log($"[Mouse] Exited: {currentStem.stemData.stemName}");
                        }
                        currentStem = null;
                    }
                }
            }
            else
            {
                // Mouse released - exit current stem
                if (currentStem != null)
                {
                    currentStem.OnHandExit();
                    if (showDebugLogs)
                    {
                        Debug.Log($"[Mouse] Released - Exited: {currentStem.stemData.stemName}");
                    }
                    currentStem = null;
                }
            }
        }
        
        private StemProcessor FindStemAtRay(Ray ray)
        {
            if (synthController == null) return null;
            
            float closestDistance = float.MaxValue;
            StemProcessor closestStem = null;
            
            // Check all stem processors
            foreach (Transform child in synthController.transform)
            {
                StemProcessor processor = child.GetComponent<StemProcessor>();
                if (processor == null) continue;
                
                if (RayIntersectsBounds(ray, processor.interactionBounds, out Vector3 hitPoint))
                {
                    float distance = Vector3.Distance(ray.origin, hitPoint);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestStem = processor;
                    }
                }
            }
            
            return closestStem;
        }
        
        private bool RayIntersectsBounds(Ray ray, Bounds bounds, out Vector3 hitPoint)
        {
            hitPoint = Vector3.zero;
            
            // Simple AABB ray intersection
            float tmin = (bounds.min.x - ray.origin.x) / ray.direction.x;
            float tmax = (bounds.max.x - ray.origin.x) / ray.direction.x;
            
            if (tmin > tmax)
            {
                float temp = tmin;
                tmin = tmax;
                tmax = temp;
            }
            
            float tymin = (bounds.min.y - ray.origin.y) / ray.direction.y;
            float tymax = (bounds.max.y - ray.origin.y) / ray.direction.y;
            
            if (tymin > tymax)
            {
                float temp = tymin;
                tymin = tymax;
                tymax = temp;
            }
            
            if ((tmin > tymax) || (tymin > tmax))
                return false;
            
            if (tymin > tmin)
                tmin = tymin;
            
            if (tymax < tmax)
                tmax = tymax;
            
            float tzmin = (bounds.min.z - ray.origin.z) / ray.direction.z;
            float tzmax = (bounds.max.z - ray.origin.z) / ray.direction.z;
            
            if (tzmin > tzmax)
            {
                float temp = tzmin;
                tzmin = tzmax;
                tzmax = temp;
            }
            
            if ((tmin > tzmax) || (tzmin > tmax))
                return false;
            
            float t = tmin;
            if (t < 0)
            {
                t = tmax;
                if (t < 0) return false;
            }
            
            hitPoint = ray.origin + ray.direction * t;
            return true;
        }
        
        private void OnGUI()
        {
            if (!showDebugLogs) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 400, 200));
            GUILayout.Box("MVI Mouse Simulator", GUILayout.Width(390));
            
            GUILayout.Label($"Mouse Button: {mouseButton} ({(Input.GetMouseButton(mouseButton) ? "HELD" : "Released")})");
            GUILayout.Label($"Current Stem: {(currentStem != null ? currentStem.stemData.stemName : "None")}");
            
            if (currentStem != null)
            {
                GUILayout.Label($"Hand Position: {currentStem.normalizedHandPosition:F2}");
                GUILayout.Label($"X-Axis: {currentStem.xAxis.currentValue:F2} - {(currentStem.xAxis.effect != null ? currentStem.xAxis.effect.name : "None")}");
                GUILayout.Label($"Y-Axis: {currentStem.yAxis.currentValue:F2} - {(currentStem.yAxis.effect != null ? currentStem.yAxis.effect.name : "None")}");
                GUILayout.Label($"Z-Axis: {currentStem.zAxis.currentValue:F2} - {(currentStem.zAxis.effect != null ? currentStem.zAxis.effect.name : "None")}");
            }
            
            GUILayout.EndArea();
        }
    }
}
