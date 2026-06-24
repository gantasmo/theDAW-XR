using UnityEngine;

namespace Gantasmo
{
    /// <summary>
    /// Stops the camera rig from falling in a stationary, hand-tracked MR app. The BuildingBlock
    /// Camera Rig can carry a Rigidbody with gravity on; with no floor collider in the scene it
    /// then drops forever the moment the scene opens (you "fall through the ground" / the world
    /// flies up). Drop this on the Camera Rig root (or any rig object) and it neutralises gravity
    /// on that Rigidbody and its children at startup — kinematic, gravity off, zero velocity — so
    /// the rig stays exactly where it spawns. It does NOT delete the components a future locomotion
    /// setup might want, so it is easy to remove if you ever add real movement.
    /// </summary>
    [DefaultExecutionOrder(-1000)] // before any locomotor/Update runs
    [AddComponentMenu("GANTASMO/Keep Rig Grounded")]
    public class KeepRigGrounded : MonoBehaviour
    {
        [Tooltip("Also neutralise Rigidbodies on child objects of this rig.")]
        public bool includeChildren = true;

        void Awake() => Ground();
        void OnEnable() => Ground();

        void Ground()
        {
            var bodies = includeChildren
                ? GetComponentsInChildren<Rigidbody>(true)
                : GetComponents<Rigidbody>();

            foreach (var rb in bodies)
            {
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;   // Unity 6: was 'velocity'
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;              // guarantees no physics fall, even if a locomotor sets velocity
            }
        }
    }
}
