using UnityEngine;
using UnityEngine.InputSystem;

namespace Atthree.Ragdoll
{
    /// <summary>
    /// Oyuncu girdisini (Input System) okuyup <see cref="RagdollCommands"/>'a yazar.
    /// Hareket kameraya göredir: W = kameranın baktığı yön.
    ///
    /// Varsayılan tuşlar:
    ///   WASD / Sol stick     : yürü
    ///   Space / Gamepad A    : zıpla
    ///   Sol tık / Sol tetik  : sol kolu uzat + tut
    ///   Sağ tık / Sağ tetik  : sağ kolu uzat + tut
    /// Tuşlar Inspector'dan değiştirilebilir.
    /// </summary>
    [RequireComponent(typeof(ActiveRagdoll))]
    public class PlayerRagdollInput : MonoBehaviour
    {
        [Tooltip("Boşsa Camera.main kullanılır.")]
        public Transform cameraTransform;

        public InputAction move = new InputAction("Move", InputActionType.Value);
        public InputAction jump = new InputAction("Jump", InputActionType.Button);
        public InputAction grabLeft = new InputAction("GrabLeft", InputActionType.Button);
        public InputAction grabRight = new InputAction("GrabRight", InputActionType.Button);

        [Tooltip("Nişan ışınının en uzak mesafesi. Hiçbir şeye çarpmazsa bu uzaklıktaki nokta hedeflenir.")]
        public float maxAimDistance = 30f;
        [Tooltip("Kameraya bundan yakın çarpmalar yok sayılır (kamera ile karakter arasındakiler).")]
        public float minAimDistance = 2f;

        ActiveRagdoll ragdoll;
        readonly RaycastHit[] aimHits = new RaycastHit[16];

        // Component eklendiğinde (veya Inspector'da Reset) varsayılan tuşları kur.
        void Reset() => AddDefaultBindings();

        void Awake()
        {
            ragdoll = GetComponent<ActiveRagdoll>();
            if (move.bindings.Count == 0) AddDefaultBindings();
        }

        void AddDefaultBindings()
        {
            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick");

            jump = new InputAction("Jump", InputActionType.Button);
            jump.AddBinding("<Keyboard>/space");
            jump.AddBinding("<Gamepad>/buttonSouth");

            grabLeft = new InputAction("GrabLeft", InputActionType.Button);
            grabLeft.AddBinding("<Mouse>/leftButton");
            grabLeft.AddBinding("<Gamepad>/leftTrigger");

            grabRight = new InputAction("GrabRight", InputActionType.Button);
            grabRight.AddBinding("<Mouse>/rightButton");
            grabRight.AddBinding("<Gamepad>/rightTrigger");
        }

        void OnEnable()
        {
            move.Enable(); jump.Enable(); grabLeft.Enable(); grabRight.Enable();
        }

        void OnDisable()
        {
            move.Disable(); jump.Disable(); grabLeft.Disable(); grabRight.Disable();
        }

        void Update()
        {
            Transform cam = cameraTransform != null ? cameraTransform
                : (Camera.main != null ? Camera.main.transform : null);

            // Kameranın yatay ileri/sağ yönleri.
            Vector3 forward = cam != null ? cam.forward : Vector3.forward;
            Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            if (flatForward.sqrMagnitude < 0.001f) flatForward = Vector3.forward;
            Vector3 flatRight = Vector3.Cross(Vector3.up, flatForward);

            Vector2 input = move.ReadValue<Vector2>();
            var cmd = ragdoll.commands;
            cmd.move = Vector3.ClampMagnitude(flatForward * input.y + flatRight * input.x, 1f);
            cmd.lookDirection = cam != null ? AimDirection(cam) : forward;
            cmd.grabLeft = grabLeft.IsPressed();
            cmd.grabRight = grabRight.IsPressed();
            // Update'te basıldı -> FixedUpdate'te tüketilecek. (false yazmıyoruz, kaybolmasın.)
            if (jump.WasPressedThisFrame()) cmd.jumpRequested = true;
        }

        /// <summary>
        /// Ekranın ortasının (kameranın) gösterdiği noktayı bulur ve göğüsten o noktaya olan yönü döndürür.
        /// Kamera karakterin arkasında olduğu için sadece kamera yönünü kullansaydık kollar
        /// nişan noktasının biraz yanına/altına uzanırdı.
        /// </summary>
        Vector3 AimDirection(Transform cam)
        {
            Vector3 aimPoint = cam.position + cam.forward * maxAimDistance;
            int count = Physics.RaycastNonAlloc(cam.position, cam.forward, aimHits, maxAimDistance, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (ragdoll.IsOwnCollider(aimHits[i].collider)) continue;
                // Kameranın önünde ama karakterin gerisinde kalan şeyleri (ör. kameraya yakın duvar) atla.
                if (aimHits[i].distance < minAimDistance) continue;
                if (aimHits[i].distance < best) { best = aimHits[i].distance; aimPoint = aimHits[i].point; }
            }

            var chest = ragdoll.Chest != null ? ragdoll.Chest : ragdoll.Hips;
            Vector3 dir = aimPoint - chest.Body.position;
            return dir.sqrMagnitude > 0.01f ? dir.normalized : cam.forward;
        }
    }
}
