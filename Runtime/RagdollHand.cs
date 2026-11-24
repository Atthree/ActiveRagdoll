using UnityEngine;

namespace Atthree.Ragdoll
{
    /// <summary>
    /// El: tutma isteği varken bir şeye değerse, ona bir FixedJoint ile yapışır.
    /// - Değdiği şeyin Rigidbody'si varsa ona bağlanır (kutu taşıma, başka ragdoll'u tutma).
    /// - Yoksa dünyaya bağlanır (duvara / kenara tutunup asılma, tırmanma).
    /// Tuş bırakılınca joint silinir.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class RagdollHand : MonoBehaviour
    {
        [Tooltip("Bu kuvvetten fazla çekilirse tutuş kopar.")]
        public float breakForce = 8000f;

        [System.NonSerialized] public bool wantsToGrab;

        public bool IsHolding => grabJoint != null;
        public Rigidbody HeldBody => grabJoint != null ? grabJoint.connectedBody : null;

        Rigidbody body;
        FixedJoint grabJoint;
        RagdollBone bone;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            bone = GetComponent<RagdollBone>();
        }

        void FixedUpdate()
        {
            if (!wantsToGrab && grabJoint != null) Release();
        }

        void OnCollisionEnter(Collision c) => TryGrab(c);
        void OnCollisionStay(Collision c) => TryGrab(c);

        void TryGrab(Collision collision)
        {
            if (!wantsToGrab || grabJoint != null) return;
            if (bone != null && bone.Owner != null && bone.Owner.IsOwnCollider(collision.collider)) return;

            grabJoint = gameObject.AddComponent<FixedJoint>();
            grabJoint.connectedBody = collision.rigidbody; // null => dünyaya bağlan
            grabJoint.breakForce = breakForce;
            grabJoint.breakTorque = breakForce;
            grabJoint.enableCollision = false;
        }

        public void Release()
        {
            if (grabJoint != null) Destroy(grabJoint);
            grabJoint = null;
        }

        void OnJointBreak(float force)
        {
            grabJoint = null; // Unity joint'i kendisi siler
        }
    }
}
