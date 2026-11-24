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

        ActiveRagdoll ragdoll;

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
            cmd.lookDirection = forward;
            cmd.grabLeft = grabLeft.IsPressed();
            cmd.grabRight = grabRight.IsPressed();
            // Update'te basıldı -> FixedUpdate'te tüketilecek. (false yazmıyoruz, kaybolmasın.)
            if (jump.WasPressedThisFrame()) cmd.jumpRequested = true;
        }
    }
}
