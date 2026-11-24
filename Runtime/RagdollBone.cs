using UnityEngine;

namespace Atthree.Ragdoll
{
    /// <summary>
    /// Ragdoll'un tek bir parçası (ör. sol üst bacak).
    /// Rigidbody + ConfigurableJoint'i sarar ve "bu kemik şu pozda dursun" demeyi kolaylaştırır.
    ///
    /// Önemli fikir: Tüm hedef pozlar DİNLENME POZUNA (sahnede durduğu ilk poz) göre bir
    /// "offset" olarak verilir. Böylece kod, karakterin kemik eksenlerinin nasıl döndüğünü
    /// bilmek zorunda kalmaz (Synty, Mixamo vb. hepsinde aynı kod çalışır).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class RagdollBone : MonoBehaviour
    {
        [SerializeField] BodyPart part;

        [Tooltip("Joint yay gücü çarpanı. Kollar uzanırken bunu ActiveRagdollArms artırır.")]
        public float strengthMultiplier = 1f;

        public BodyPart Part => part;
        public Rigidbody Body { get; private set; }
        public ConfigurableJoint Joint { get; private set; }
        public ActiveRagdoll Owner { get; private set; }

        /// <summary>Oyun başındaki dünya pozisyonu (yön hesapları için).</summary>
        public Vector3 RestPosition { get; private set; }

        // Joint'in bağlı olduğu gövdeye göre başlangıç rotasyonu.
        Quaternion startLocalRotation;
        // Bağlı gövdenin başlangıçtaki dünya rotasyonu.
        Quaternion parentRestRotation;
        // Karakter kökünün başlangıçtaki dünya rotasyonu.
        Quaternion rootRestRotation;

        /// <summary>Editor builder parçayı bu metotla atar.</summary>
        public void SetPart(BodyPart value) => part = value;

        /// <summary>
        /// ActiveRagdoll.Awake tarafından, fizik daha hiç çalışmadan çağrılır.
        /// Bu anda karakter dinlenme pozunda olduğu için tüm "rest" verileri burada alınır.
        /// </summary>
        public void Initialize(ActiveRagdoll owner, Quaternion rootRotation)
        {
            Owner = owner;
            Body = GetComponent<Rigidbody>();
            Joint = GetComponent<ConfigurableJoint>();
            RestPosition = transform.position;
            rootRestRotation = rootRotation;

            if (Joint != null && Joint.connectedBody != null)
            {
                parentRestRotation = Joint.connectedBody.rotation;
                startLocalRotation = Quaternion.Inverse(parentRestRotation) * transform.rotation;
            }
            else
            {
                parentRestRotation = Quaternion.identity;
                startLocalRotation = transform.rotation;
            }
        }

        /// <summary>
        /// "Karakter uzayındaki" bir yönü (x = sağ, y = yukarı, z = ileri)
        /// bağlı olduğu gövdenin (parent) yerel uzayına çevirir.
        /// </summary>
        public Vector3 CharacterToParent(Vector3 characterDirection)
        {
            return Quaternion.Inverse(parentRestRotation) * (rootRestRotation * characterDirection);
        }

        /// <summary>Dünya uzayındaki bir yönü, DİNLENME anındaki parent uzayına çevirir.</summary>
        public Vector3 RestWorldToParent(Vector3 worldDirection)
        {
            return Quaternion.Inverse(parentRestRotation) * worldDirection;
        }

        /// <summary>
        /// Kemiği dinlenme pozundan "offset" kadar döndür. Offset, parent uzayında verilir.
        /// Quaternion.identity = dinlenme pozu.
        /// </summary>
        public void SetTargetOffset(Quaternion offsetInParent)
        {
            if (Joint == null) return;
            Joint.SetTargetRotationLocal(offsetInParent * startLocalRotation, startLocalRotation);
        }

        /// <summary>
        /// Karakterin sağ ekseni etrafında döndürür (yürüme için ideal).
        /// Pozitif açı = kemik GERİYE döner (ör. diz bükülmesi), negatif = ileri.
        /// </summary>
        public void SetPitch(float degrees)
        {
            SetTargetOffset(Quaternion.AngleAxis(degrees, CharacterToParent(Vector3.right)));
        }

        /// <summary>Joint'in açısal yay gücünü ayarlar (kütleye göre ölçeklenir).</summary>
        public void SetDrive(float spring, float damper)
        {
            if (Joint == null) return;
            float mass = Body.mass * strengthMultiplier;
            var drive = new JointDrive
            {
                positionSpring = spring * mass,
                positionDamper = damper * mass,
                maximumForce = float.MaxValue,
            };
            Joint.slerpDrive = drive;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (Owner != null) Owner.ReportCollision(this, collision);
        }
    }
}
