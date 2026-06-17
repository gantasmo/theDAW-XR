using UnityEngine;

namespace HologramVFXDemo
{
    public class OrbitCamera : MonoBehaviour
    {
        public Transform target;   // The object or point to orbit around
        public float speed = 20f;    // Rotation speed (degrees per second)

        void Update()
        {
            if (!target) return;

            // Rotate around the target’s Y axis
            transform.RotateAround(target.position, Vector3.up, speed * Time.deltaTime);

            // Always look at the target
            transform.LookAt(target);
        }
    }
}