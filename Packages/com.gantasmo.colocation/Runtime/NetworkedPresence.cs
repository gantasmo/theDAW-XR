using Unity.Netcode;
using UnityEngine;

namespace Gantasmo.Colocation
{
    /// <summary>
    /// Lightweight networked presence for co-located play: replicates the local
    /// player's head and two hands so peers can see each other in the shared
    /// space. Poses are written in world space, which is valid because the Meta
    /// Colocation building block aligns every headset's world origin to the same
    /// shared spatial anchor, so a given world coordinate is the same physical
    /// point on every device.
    ///
    /// Usage: this component sits on the NetworkManager's Player Prefab (a prefab
    /// root that also carries a NetworkObject). NGO spawns one instance per
    /// connected client; the owner drives its own pose from the local OVR rig and
    /// everyone else renders the replicated proxies. Full Meta Avatars are a later
    /// upgrade; head-plus-hands proxies are the v1 presence.
    ///
    /// The proxy meshes are created at runtime and share a single material, so no
    /// per-instance assets are spawned per player.
    /// </summary>
    [AddComponentMenu("GANTASMO/Colocation/Networked Presence")]
    public class NetworkedPresence : NetworkBehaviour
    {
        [Tooltip("Hide this player's own proxies locally. The owner already sees real hands through passthrough / hand visuals.")]
        public bool hideLocalAvatar = true;

        [Tooltip("Edge length of the head proxy cube, in metres.")]
        public float headSize = 0.18f;

        [Tooltip("Diameter of each hand proxy sphere, in metres.")]
        public float handSize = 0.09f;

        [Tooltip("Colour used for remote players' proxies.")]
        public Color proxyColor = new Color(0.36f, 0.78f, 0.95f);

        // Owner-written, everyone-readable pose state.
        readonly NetworkVariable<Vector3> _headPos = Pos();
        readonly NetworkVariable<Quaternion> _headRot = Rot();
        readonly NetworkVariable<Vector3> _lHandPos = Pos();
        readonly NetworkVariable<Quaternion> _lHandRot = Rot();
        readonly NetworkVariable<Vector3> _rHandPos = Pos();
        readonly NetworkVariable<Quaternion> _rHandRot = Rot();

        static NetworkVariable<Vector3> Pos() => new NetworkVariable<Vector3>(
            Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        static NetworkVariable<Quaternion> Rot() => new NetworkVariable<Quaternion>(
            Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        Transform _head, _lHand, _rHand;          // visual proxies (all clients)
        Transform _eyeAnchor, _lAnchor, _rAnchor; // local rig sources (owner only)
        static Material s_sharedMat;

        public override void OnNetworkSpawn()
        {
            BuildProxies();
            if (IsOwner) BindLocalRig();
            SetProxiesVisible(!(IsOwner && hideLocalAvatar));
        }

        void BindLocalRig()
        {
            var rig = Object.FindAnyObjectByType<OVRCameraRig>();
            if (rig == null) return;
            _eyeAnchor = rig.centerEyeAnchor;
            _lAnchor = rig.leftHandAnchor;
            _rAnchor = rig.rightHandAnchor;
        }

        void LateUpdate()
        {
            if (!IsSpawned) return;

            if (IsOwner && _eyeAnchor != null)
            {
                // NetworkVariable only syncs on actual change, so writing each
                // frame is cheap when the player is still.
                _headPos.Value = _eyeAnchor.position;
                _headRot.Value = _eyeAnchor.rotation;
                if (_lAnchor != null) { _lHandPos.Value = _lAnchor.position; _lHandRot.Value = _lAnchor.rotation; }
                if (_rAnchor != null) { _rHandPos.Value = _rAnchor.position; _rHandRot.Value = _rAnchor.rotation; }
            }

            Apply(_head, _headPos.Value, _headRot.Value);
            Apply(_lHand, _lHandPos.Value, _lHandRot.Value);
            Apply(_rHand, _rHandPos.Value, _rHandRot.Value);
        }

        static void Apply(Transform t, Vector3 pos, Quaternion rot)
        {
            if (t != null) t.SetPositionAndRotation(pos, rot);
        }

        void BuildProxies()
        {
            if (s_sharedMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                s_sharedMat = new Material(sh) { color = proxyColor };
            }
            _head = MakeProxy(PrimitiveType.Cube, headSize, "Head");
            _lHand = MakeProxy(PrimitiveType.Sphere, handSize, "LeftHand");
            _rHand = MakeProxy(PrimitiveType.Sphere, handSize, "RightHand");
        }

        Transform MakeProxy(PrimitiveType type, float size, string label)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = label;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);           // proxies must not interfere with interactables
            var rend = go.GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = s_sharedMat;
            var t = go.transform;
            t.SetParent(transform, false);
            t.localScale = Vector3.one * size;
            return t;
        }

        void SetProxiesVisible(bool visible)
        {
            if (_head != null) _head.gameObject.SetActive(visible);
            if (_lHand != null) _lHand.gameObject.SetActive(visible);
            if (_rHand != null) _rHand.gameObject.SetActive(visible);
        }
    }
}
