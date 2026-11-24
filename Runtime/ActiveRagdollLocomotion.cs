using UnityEngine;

namespace Atthree.Ragdoll
{
    /// <summary>
    /// Hareket: yürüme, dönme, zıplama ve PROSEDÜREL bacak animasyonu.
    ///
    /// - Gövde, kalçaya uygulanan yatay kuvvetle hareket eder (bacaklar itmez).
    /// - Bacaklar bir sinüs dalgasıyla ileri-geri sallanır; hıza göre genliği ve frekansı artar.
    ///   Böylece animasyon dosyası olmadan "yürüyormuş" gibi görünür.
    /// </summary>
    [RequireComponent(typeof(ActiveRagdoll))]
    public class ActiveRagdollLocomotion : MonoBehaviour
    {
        [Header("Hareket")]
        public float moveSpeed = 3.5f;
        [Tooltip("Hedef hıza ne kadar hızlı ulaşılır (1/sn).")]
        public float acceleration = 8f;
        public float maxAcceleration = 25f;
        [Range(0f, 1f)] public float airControl = 0.3f;
        public float turnSpeed = 540f;
        [Tooltip("Tutma tuşu basılıyken karakter kameranın baktığı yöne döner (yürürken de).")]
        public bool faceLookWhileGrabbing = true;

        [Header("Zıplama")]
        public float jumpSpeed = 5f;
        public float jumpCooldown = 0.5f;

        [Header("Yürüme Animasyonu")]
        [Tooltip("Tam hızda saniyedeki adım döngüsü sayısı.")]
        public float stepFrequency = 1.8f;
        [Tooltip("Kalçanın ileri-geri salınım açısı.")]
        public float strideAngle = 35f;
        [Tooltip("Öne gelen bacağın dizini bükme açısı.")]
        public float kneeBendAngle = 60f;
        [Tooltip("Koşarken göğsün öne eğilmesi.")]
        public float runLean = 10f;

        [Header("Havada")]
        public float airHipAngle = -25f;
        public float airKneeAngle = 45f;

        ActiveRagdoll ragdoll;
        RagdollBone leftUpperLeg, leftLowerLeg, rightUpperLeg, rightLowerLeg;
        float phase;
        float jumpTimer;

        /// <summary>0..1 arası yürüme hızı. Kollar sallanmak için okur.</summary>
        public float Speed01 { get; private set; }
        /// <summary>Adım döngüsünün fazı (radyan).</summary>
        public float Phase => phase;

        void Start()
        {
            ragdoll = GetComponent<ActiveRagdoll>();
            leftUpperLeg = ragdoll.Get(BodyPart.LeftUpperLeg);
            leftLowerLeg = ragdoll.Get(BodyPart.LeftLowerLeg);
            rightUpperLeg = ragdoll.Get(BodyPart.RightUpperLeg);
            rightLowerLeg = ragdoll.Get(BodyPart.RightLowerLeg);
        }

        void FixedUpdate()
        {
            if (ragdoll.Hips == null) return;
            float dt = Time.fixedDeltaTime;
            var cmd = ragdoll.commands;
            jumpTimer -= dt;

            if (ragdoll.IsKnockedOut)
            {
                cmd.jumpRequested = false;
                return;
            }

            Move(cmd, dt);
            Jump(cmd);
            AnimateLegs(dt);
        }

        void Move(RagdollCommands cmd, float dt)
        {
            Vector3 input = cmd.move;
            input.y = 0f;
            input = Vector3.ClampMagnitude(input, 1f);

            // Tutma tuşu basılıyken bakış (kamera) yönüne dön, yoksa yürüme yönüne.
            bool aiming = faceLookWhileGrabbing && (cmd.grabLeft || cmd.grabRight);
            if (aiming)
                ragdoll.TurnTowards(cmd.lookDirection, turnSpeed, dt);
            else if (input.sqrMagnitude > 0.01f)
                ragdoll.TurnTowards(input, turnSpeed, dt);

            Vector3 velocity = ragdoll.Velocity;
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 targetVelocity = input * moveSpeed;

            // Hedef hıza doğru ivme (input yoksa bu otomatik olarak fren olur).
            Vector3 accel = (targetVelocity - horizontal) * acceleration;
            accel = Vector3.ClampMagnitude(accel, maxAcceleration);
            if (!ragdoll.IsGrounded) accel *= airControl;

            // Kuvveti tüm kütleye göre kalçaya uygula; diğer parçalar joint'lerle peşinden gelir.
            ragdoll.Hips.Body.AddForce(accel * ragdoll.TotalMass * ragdoll.Strength, ForceMode.Force);

            Speed01 = Mathf.Clamp01(horizontal.magnitude / Mathf.Max(0.01f, moveSpeed));
            ragdoll.chestLean = runLean * Speed01;
        }

        void Jump(RagdollCommands cmd)
        {
            if (!cmd.jumpRequested) return;
            cmd.jumpRequested = false;
            if (!ragdoll.IsGrounded || jumpTimer > 0f || ragdoll.Strength < 0.9f) return;

            jumpTimer = jumpCooldown;
            ragdoll.SuspendHeight(0.3f);

            // Her parçaya aynı hız değişimini ver: vücut bir bütün olarak sıçrar.
            foreach (var bone in ragdoll.Bones)
            {
                Vector3 v = bone.Body.linearVelocity;
                v.y = Mathf.Max(v.y, 0f) + jumpSpeed;
                bone.Body.linearVelocity = v;
            }
        }

        void AnimateLegs(float dt)
        {
            if (!ragdoll.IsGrounded)
            {
                // Havada bacakları hafifçe topla.
                SetLeg(leftUpperLeg, leftLowerLeg, airHipAngle, airKneeAngle);
                SetLeg(rightUpperLeg, rightLowerLeg, airHipAngle, airKneeAngle);
                return;
            }

            // Hız arttıkça adım hızlanır. Dururken faz ilerlemez, genlik 0'a iner.
            phase += dt * stepFrequency * Mathf.PI * 2f * Mathf.Lerp(0.5f, 1f, Speed01) * (Speed01 > 0.05f ? 1f : 0f);
            if (phase > Mathf.PI * 2f) phase -= Mathf.PI * 2f;

            float sin = Mathf.Sin(phase);
            float cos = Mathf.Cos(phase);
            float stride = strideAngle * Speed01;
            float knee = kneeBendAngle * Speed01;

            // Negatif açı = bacak öne. Sol ve sağ zıt fazda.
            // Öne doğru savrulan bacağın dizi bükülür (ayak yere sürtmesin).
            SetLeg(leftUpperLeg, leftLowerLeg, -sin * stride, Mathf.Max(0f, cos) * knee);
            SetLeg(rightUpperLeg, rightLowerLeg, sin * stride, Mathf.Max(0f, -cos) * knee);
        }

        static void SetLeg(RagdollBone upper, RagdollBone lower, float hipAngle, float kneeAngle)
        {
            if (upper != null) upper.SetPitch(hipAngle);
            if (lower != null) lower.SetPitch(kneeAngle);
        }
    }
}
