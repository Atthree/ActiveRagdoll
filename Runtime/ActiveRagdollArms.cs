using UnityEngine;

namespace Atthree.Ragdoll
{
    /// <summary>
    /// Kollar: boştayken aşağı sarkar ve yürürken sallanır; tutma tuşuna basılınca
    /// bakış yönüne uzanır ve eli değdiği şeyi tutar (<see cref="RagdollHand"/>).
    ///
    /// Kol pozları "yön" ile verilir: "üst kol şu yöne baksın" deriz, gereken rotasyonu
    /// Quaternion.FromToRotation hesaplar. Bu sayede karakter T-pose da olsa A-pose da olsa çalışır.
    /// </summary>
    [RequireComponent(typeof(ActiveRagdoll))]
    public class ActiveRagdollArms : MonoBehaviour
    {
        [Header("Boşta")]
        [Tooltip("Kolların yana açılma miktarı (0 = gövdeye yapışık).")]
        public float idleSpread = 0.25f;
        [Tooltip("Yürürken kolların ileri-geri sallanması.")]
        public float walkSwing = 0.5f;

        [Header("Uzanma")]
        [Tooltip("Uzanırken iki kol arasındaki yatay açıklık.")]
        public float reachSpread = 0.15f;
        [Tooltip("Uzanan kolun joint güç çarpanı.")]
        public float reachStrength = 2.5f;
        [Tooltip("Bakış yönünün dikey açısı bu aralıkta sınırlanır.")]
        public float minReachPitch = -60f;
        public float maxReachPitch = 80f;
        [Tooltip("Kolların gövdeye göre en fazla kaç derece yana uzanabileceği.")]
        public float maxReachYaw = 60f;

        ActiveRagdoll ragdoll;
        ActiveRagdollLocomotion locomotion;
        Arm left, right;

        class Arm
        {
            public RagdollBone upper, lower, hand;
            public RagdollHand grabber;
            public Vector3 restDirectionInParent; // üst kolun dinlenmedeki yönü (göğüs uzayında)
            public float side;                    // sol = -1, sağ = +1
        }

        void Start()
        {
            ragdoll = GetComponent<ActiveRagdoll>();
            locomotion = GetComponent<ActiveRagdollLocomotion>();
            left = CreateArm(BodyPart.LeftUpperArm, BodyPart.LeftLowerArm, BodyPart.LeftHand, -1f);
            right = CreateArm(BodyPart.RightUpperArm, BodyPart.RightLowerArm, BodyPart.RightHand, 1f);
        }

        Arm CreateArm(BodyPart upperPart, BodyPart lowerPart, BodyPart handPart, float side)
        {
            var arm = new Arm
            {
                upper = ragdoll.Get(upperPart),
                lower = ragdoll.Get(lowerPart),
                hand = ragdoll.Get(handPart),
                side = side,
            };
            if (arm.upper == null || arm.lower == null) return null;

            Vector3 restWorld = (arm.lower.RestPosition - arm.upper.RestPosition).normalized;
            arm.restDirectionInParent = arm.upper.RestWorldToParent(restWorld);

            var grabSource = arm.hand != null ? arm.hand : arm.lower;
            arm.grabber = grabSource.GetComponent<RagdollHand>();
            if (arm.grabber == null) arm.grabber = grabSource.gameObject.AddComponent<RagdollHand>();
            return arm;
        }

        void FixedUpdate()
        {
            if (ragdoll.Hips == null) return;
            var cmd = ragdoll.commands;
            bool knocked = ragdoll.IsKnockedOut;

            UpdateArm(left, cmd.grabLeft, knocked);
            UpdateArm(right, cmd.grabRight, knocked);
        }

        void UpdateArm(Arm arm, bool grab, bool knocked)
        {
            if (arm == null) return;

            arm.grabber.wantsToGrab = grab;
            if (knocked) return; // bayılınca güç zaten sıfır, poz önemsiz

            Vector3 direction; // karakter uzayında (x sağ, y yukarı, z ileri)
            if (grab)
            {
                direction = ReachDirection() + Vector3.right * (arm.side * reachSpread);
                SetStrength(arm, reachStrength);
            }
            else
            {
                float speed = locomotion != null ? locomotion.Speed01 : 0f;
                float swing = locomotion != null ? Mathf.Sin(locomotion.Phase) * walkSwing * speed : 0f;
                // Sol bacak öndeyken (sin > 0) sol kol geride olur: doğal yürüyüş.
                direction = new Vector3(arm.side * idleSpread, -1f, arm.side * swing);
                SetStrength(arm, 1f);
            }

            Vector3 desiredInParent = arm.upper.CharacterToParent(direction.normalized);
            arm.upper.SetTargetOffset(Quaternion.FromToRotation(arm.restDirectionInParent, desiredInParent));
            arm.lower.SetTargetOffset(Quaternion.identity); // dirsek düz
            if (arm.hand != null) arm.hand.SetTargetOffset(Quaternion.identity);
        }

        /// <summary>
        /// Bakış yönünü karakter uzayına çevirir; dikey ve yatay açısını sınırlar.
        /// Karakter dönerken gecikse bile kollar hedefe doğru (en fazla ±maxReachYaw) uzanır.
        /// </summary>
        Vector3 ReachDirection()
        {
            Vector3 look = Quaternion.Inverse(ragdoll.FacingRotation) * ragdoll.commands.lookDirection;
            if (look.sqrMagnitude < 0.0001f) look = Vector3.forward;
            look.Normalize();

            float pitch = Mathf.Asin(Mathf.Clamp(look.y, -1f, 1f)) * Mathf.Rad2Deg;
            pitch = Mathf.Clamp(pitch, minReachPitch, maxReachPitch);
            float yaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
            yaw = Mathf.Clamp(yaw, -maxReachYaw, maxReachYaw); // kollar arkaya uzanmasın
            return Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
        }

        static void SetStrength(Arm arm, float value)
        {
            arm.upper.strengthMultiplier = value;
            arm.lower.strengthMultiplier = value;
            if (arm.hand != null) arm.hand.strengthMultiplier = value;
        }
    }
}
