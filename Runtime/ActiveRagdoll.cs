using System;
using System.Collections.Generic;
using UnityEngine;

namespace Atthree.Ragdoll
{
    /// <summary>
    /// Active ragdoll'un BEYNİ. Karakter kökünde durur.
    ///
    /// Bu script üç şey yapar:
    ///  1) GÜÇ: Tüm joint'lere "dinlenme pozunu koru" gücü verir (slerp drive).
    ///  2) DENGE: Kalçaya ve göğse, dik durmaları ve bakış yönüne dönmeleri için tork uygular.
    ///  3) YÜKSEKLİK: Kalçayı bir yay gibi yerden belli yükseklikte tutar ("havada süzülen kalça").
    ///     Bacaklar ağırlığı taşımaz, sadece görsel olarak adım atar. Bu, Human Fall Flat tarzı
    ///     oyunlarda kullanılan en sağlam yöntemdir.
    ///
    /// Bayılma (knockout) olduğunda güç sıfıra iner, karakter tam ragdoll olur; süre bitince
    /// güç yavaşça geri gelir ve denge + yükseklik kuvvetleri onu tekrar ayağa kaldırır.
    ///
    /// Hareket: <see cref="ActiveRagdollLocomotion"/>, Kollar: <see cref="ActiveRagdollArms"/>.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class ActiveRagdoll : MonoBehaviour
    {
        [Header("Joint Gücü")]
        [Tooltip("Joint'lerin pozu koruma gücü (kütle başına).")]
        public float jointSpring = 400f;
        public float jointDamper = 8f;
        [Tooltip("Bayılmışken kalan güç oranı (0 = tamamen gevşek).")]
        [Range(0f, 1f)] public float limpStrength = 0.03f;

        [Header("Denge (dik durma torku)")]
        public float balanceSpring = 1000f;
        public float balanceDamper = 60f;
        [Tooltip("Göğsün denge torku kalçaya göre oranı.")]
        [Range(0f, 1f)] public float chestBalanceRatio = 1f;

        [Header("Yükseklik (kalça yayı)")]
        [Tooltip("Ayakta durma yüksekliğine eklenecek fark. Pozitif = ayaklar yere sürtmesin diye hafif süzülür.")]
        public float standHeightOffset = 0.02f;
        public float heightSpring = 150f;
        public float heightDamper = 20f;
        [Tooltip("Kalça bu mesafeden daha yakınsa karakter 'yerde' sayılır (ayakta yükseklik + bu).")]
        public float groundTolerance = 0.35f;
        public float groundCheckRadius = 0.12f;
        public LayerMask groundMask = ~0;
        [Tooltip("Bacak collider'ları sürtünmesiz olsun. Yürürken yere değen ayaklar gövdeyi " +
                 "geride tutup öne devirmesin diye (karakteri zaten kalça yayı taşıyor).")]
        public bool frictionlessLegs = true;

        [Header("Bayılma")]
        public bool autoKnockout = true;
        [Tooltip("Kalça bu açıdan fazla eğilirse ve süre dolarsa bayılır.")]
        public float knockoutTiltAngle = 65f;
        public float knockoutTiltTime = 0.4f;
        [Tooltip("Bu hızdan sert çarpışma bayıltır.")]
        public float knockoutImpactSpeed = 11f;
        public float knockoutDuration = 2f;
        [Tooltip("Gücün 0'dan 1'e dönme süresi.")]
        public float recoveryDuration = 1.2f;
        [Tooltip("Knockout() kodla çağrılınca üst gövdeye verilen sendeleme hızı (m/s). " +
                 "Bu olmadan dengede duran karakter gevşese bile yığılmayabilir.")]
        public float knockoutStumble = 1.5f;

        [Header("Komutlar (giriş scripti doldurur)")]
        public RagdollCommands commands = new RagdollCommands();

        // ---- Dışarıya açık durum ----
        public RagdollBone Hips { get; private set; }
        public RagdollBone Chest { get; private set; }
        public IReadOnlyList<RagdollBone> Bones => bones;
        public float TotalMass { get; private set; }
        public float StandHeight { get; private set; }
        public bool IsGrounded { get; private set; }
        public float GroundDistance { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        public bool IsKnockedOut => knockoutTimer > 0f;
        /// <summary>0 = tamamen gevşek, 1 = tam güç.</summary>
        public float Strength { get; private set; } = 1f;
        public Quaternion FacingRotation => Quaternion.Euler(0f, facingYaw, 0f);
        public Vector3 Velocity => Hips.Body.linearVelocity;

        /// <summary>Göğsün ileri eğilme açısı (derece). Locomotion koşarken ayarlar.</summary>
        [NonSerialized] public float chestLean;

        public event Action Knocked;
        public event Action Recovered;

        readonly List<RagdollBone> bones = new List<RagdollBone>();
        readonly Dictionary<BodyPart, RagdollBone> byPart = new Dictionary<BodyPart, RagdollBone>();
        readonly HashSet<Collider> ownColliders = new HashSet<Collider>();
        readonly RaycastHit[] hitBuffer = new RaycastHit[16];

        Quaternion hipsRestOffset;   // kalçanın, karakter köküne göre başlangıç rotasyonu
        Quaternion chestRestOffset;
        float facingYaw;
        float knockoutTimer;
        float tiltTimer;
        float heightSuspendTimer;

        // =====================================================================
        void Awake()
        {
            Quaternion rootRotation = transform.rotation;

            GetComponentsInChildren(bones);
            foreach (var bone in bones)
            {
                bone.Initialize(this, rootRotation);
                byPart[bone.Part] = bone;
                TotalMass += bone.Body.mass;
            }

            if (!byPart.TryGetValue(BodyPart.Hips, out var hips))
            {
                Debug.LogError($"[ActiveRagdoll] '{name}' altında Hips bulunamadı. Önce Tools > Active Ragdoll > Builder ile kurun.", this);
                enabled = false;
                return;
            }
            Hips = hips;
            byPart.TryGetValue(BodyPart.Chest, out var chest);
            Chest = chest;

            // Kendi collider'larımız birbirine çarpmasın (kol gövdeye takılmasın vs).
            foreach (var bone in bones)
                foreach (var c in bone.GetComponentsInChildren<Collider>())
                    if (c.attachedRigidbody == bone.Body) ownColliders.Add(c);
            var list = new List<Collider>(ownColliders);
            for (int i = 0; i < list.Count; i++)
                for (int j = i + 1; j < list.Count; j++)
                    Physics.IgnoreCollision(list[i], list[j], true);

            if (frictionlessLegs) ApplyFrictionlessLegs();

            // Ayakta durma yüksekliği: kalçanın, köke (ayak tabanı) olan dikey mesafesi.
            StandHeight = Vector3.Dot(Hips.transform.position - transform.position, Vector3.up) + standHeightOffset;

            hipsRestOffset = Quaternion.Inverse(rootRotation) * Hips.transform.rotation;
            if (Chest != null) chestRestOffset = Quaternion.Inverse(rootRotation) * Chest.transform.rotation;
            facingYaw = rootRotation.eulerAngles.y;

            // Karakteri canlandıran Animator varsa fiziği ezer; kapat.
            var animator = GetComponent<Animator>();
            if (animator != null) animator.enabled = false;
        }

        // =====================================================================
        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            UpdateGround();
            UpdateKnockout(dt);
            UpdateJointDrives();

            if (Strength > 0f)
            {
                ApplyBalance();
                ApplyHeight(dt);
            }
        }

        // ---- 1) GÜÇ -----------------------------------------------------------
        void UpdateJointDrives()
        {
            float s = Mathf.Lerp(limpStrength, 1f, Strength);
            foreach (var bone in bones)
                bone.SetDrive(jointSpring * s, jointDamper * Mathf.Max(s, 0.3f));
        }

        // ---- 2) DENGE ---------------------------------------------------------
        void ApplyBalance()
        {
            // İstenen rotasyon = bakış yönü * başlangıçtaki ofset.
            Quaternion hipsTarget = FacingRotation * hipsRestOffset;
            ApplyUprightTorque(Hips.Body, hipsTarget, Strength);

            if (Chest != null)
            {
                Quaternion lean = Quaternion.AngleAxis(chestLean, FacingRotation * Vector3.right);
                Quaternion chestTarget = lean * FacingRotation * chestRestOffset;
                ApplyUprightTorque(Chest.Body, chestTarget, Strength * chestBalanceRatio);
            }
        }

        /// <summary>
        /// PD kontrolcü: hata açısı kadar yay torku (P) - açısal hız kadar sönüm (D).
        /// ForceMode.Acceleration kullandığımız için kütleden bağımsızdır.
        /// </summary>
        void ApplyUprightTorque(Rigidbody body, Quaternion target, float strength)
        {
            Quaternion error = target * Quaternion.Inverse(body.rotation);
            error.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (float.IsNaN(axis.x) || Mathf.Abs(angle) < 0.01f) axis = Vector3.zero;

            Vector3 torque = axis * (angle * Mathf.Deg2Rad * balanceSpring) - body.angularVelocity * balanceDamper;
            body.AddTorque(torque * strength, ForceMode.Acceleration);
        }

        // ---- 3) YÜKSEKLİK -----------------------------------------------------
        void UpdateGround()
        {
            Vector3 origin = Hips.Body.position + Vector3.up * groundCheckRadius;
            float maxDistance = StandHeight + groundTolerance + groundCheckRadius;

            int count = Physics.SphereCastNonAlloc(origin, groundCheckRadius, Vector3.down, hitBuffer,
                maxDistance, groundMask, QueryTriggerInteraction.Ignore);

            float best = float.MaxValue;
            Vector3 normal = Vector3.up;
            for (int i = 0; i < count; i++)
            {
                var hit = hitBuffer[i];
                if (ownColliders.Contains(hit.collider)) continue;
                if (hit.distance <= 0f) continue; // başlangıçta içinde olduğumuz collider
                float vertical = Hips.Body.position.y - hit.point.y;
                if (vertical < best) { best = vertical; normal = hit.normal; }
            }

            GroundDistance = best;
            GroundNormal = normal;
            IsGrounded = best <= StandHeight + groundTolerance;
        }

        void ApplyHeight(float dt)
        {
            if (heightSuspendTimer > 0f) { heightSuspendTimer -= dt; return; }
            if (!IsGrounded) return;

            float error = StandHeight - GroundDistance;
            float verticalVelocity = Hips.Body.linearVelocity.y;
            float acceleration = error * heightSpring - verticalVelocity * heightDamper;

            // Tüm vücudun ağırlığını kalçadan taşı (+ yerçekimini telafi et).
            float force = TotalMass * (acceleration + Mathf.Abs(Physics.gravity.y)) * Strength;
            force = Mathf.Clamp(force, 0f, TotalMass * Mathf.Abs(Physics.gravity.y) * 4f);
            Hips.Body.AddForce(Vector3.up * force, ForceMode.Force);
        }

        // ---- BAYILMA ----------------------------------------------------------
        void UpdateKnockout(float dt)
        {
            if (knockoutTimer > 0f)
            {
                knockoutTimer -= dt;
                Strength = 0f;
                if (knockoutTimer <= 0f) knockoutTimer = 0f;
                return;
            }

            if (Strength < 1f)
            {
                Strength = Mathf.MoveTowards(Strength, 1f, dt / Mathf.Max(0.01f, recoveryDuration));
                if (Strength >= 1f) Recovered?.Invoke();
                return; // kalkarken eğim kontrolü yapma
            }

            if (!autoKnockout) return;
            float tilt = Vector3.Angle(Hips.transform.rotation * Quaternion.Inverse(hipsRestOffset) * Vector3.up, Vector3.up);
            tiltTimer = tilt > knockoutTiltAngle ? tiltTimer + dt : 0f;
            if (tiltTimer > knockoutTiltTime) Knockout(knockoutDuration);
        }

        /// <summary>
        /// Karakteri belirtilen süre boyunca tam ragdoll yapar.
        /// Üst gövdeye rastgele yatay yönde <see cref="knockoutStumble"/> hızında sendeleme verir.
        /// </summary>
        public void Knockout(float duration)
        {
            Vector2 dir = UnityEngine.Random.insideUnitCircle.normalized;
            Knockout(duration, new Vector3(dir.x, 0f, dir.y) * knockoutStumble);
        }

        /// <summary>Bayıltır ve üst gövdeye verilen hız kadar itme uygular (Vector3.zero = itme yok).</summary>
        public void Knockout(float duration, Vector3 push)
        {
            bool wasStanding = !IsKnockedOut;
            knockoutTimer = Mathf.Max(knockoutTimer, duration);
            Strength = 0f;
            tiltTimer = 0f;
            if (!wasStanding) return;

            if (push != Vector3.zero)
            {
                // Sadece üst gövde itilir: ayaklar yerinde kalır, karakter devrilir.
                if (Chest != null) Chest.Body.AddForce(push, ForceMode.VelocityChange);
                var head = Get(BodyPart.Head);
                if (head != null) head.Body.AddForce(push, ForceMode.VelocityChange);
            }
            Knocked?.Invoke();
        }

        /// <summary>RagdollBone çarpışmaları buraya iletir.</summary>
        public void ReportCollision(RagdollBone bone, Collision collision)
        {
            if (!autoKnockout || IsKnockedOut) return;
            if (ownColliders.Contains(collision.collider)) return;
            // Sadece kafa / gövde / kalçaya gelen darbeler bayıltır. Kol ve bacaklar sayılmaz:
            // yoksa hızla savrulan kendi elin bir kutuya çarpınca bayılırdın.
            if (bone.Part != BodyPart.Head && bone.Part != BodyPart.Chest && bone.Part != BodyPart.Hips) return;
            // Çarpışmanın kendisi zaten momentum veriyor; ekstra itme yok.
            if (collision.relativeVelocity.magnitude > knockoutImpactSpeed)
                Knockout(knockoutDuration, Vector3.zero);
        }

        void ApplyFrictionlessLegs()
        {
            var material = new PhysicsMaterial("RagdollLegs")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
            };
            foreach (var part in new[] { BodyPart.LeftUpperLeg, BodyPart.LeftLowerLeg, BodyPart.RightUpperLeg, BodyPart.RightLowerLeg })
            {
                var bone = Get(part);
                if (bone == null) continue;
                foreach (var c in bone.GetComponentsInChildren<Collider>())
                    if (c.attachedRigidbody == bone.Body) c.sharedMaterial = material;
            }
        }

        // ---- Diğer scriptler için yardımcılar ----------------------------------
        public RagdollBone Get(BodyPart part) => byPart.TryGetValue(part, out var b) ? b : null;

        public bool IsOwnCollider(Collider c) => ownColliders.Contains(c);

        /// <summary>Karakteri yatay düzlemde verilen yöne doğru döndürür.</summary>
        public void TurnTowards(Vector3 worldDirection, float degreesPerSecond, float dt)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.0001f) return;
            float targetYaw = Mathf.Atan2(worldDirection.x, worldDirection.z) * Mathf.Rad2Deg;
            facingYaw = Mathf.MoveTowardsAngle(facingYaw, targetYaw, degreesPerSecond * dt);
        }

        /// <summary>Kalça yayını kısa süre kapatır (zıplarken yay karakteri yere çekmesin).</summary>
        public void SuspendHeight(float seconds) => heightSuspendTimer = seconds;

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || Hips == null) return;
            Gizmos.color = IsGrounded ? Color.green : Color.red;
            Vector3 p = Hips.Body.position;
            Gizmos.DrawLine(p, p + Vector3.down * Mathf.Min(GroundDistance, StandHeight + groundTolerance));
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(p, FacingRotation * Vector3.forward);
        }
#endif
    }
}
