using UnityEngine;
using UnityEngine.InputSystem;

namespace Atthree.Ragdoll
{
    /// <summary>
    /// Basit 3. şahıs yörünge kamerası. Kameraya eklenir, hedef olarak ragdoll verilir.
    /// Fare / sağ stick ile döner, duvar arkasına geçmemek için SphereCast yapar.
    /// Esc imleci serbest bırakır, oyun penceresine tıklamak tekrar kilitler.
    /// </summary>
    public class RagdollCamera : MonoBehaviour
    {
        public ActiveRagdoll target;
        public Vector3 pivotOffset = new Vector3(0f, 0.6f, 0f);
        public float distance = 4f;
        public float mouseSensitivity = 0.12f;
        public float gamepadSensitivity = 180f;
        public float minPitch = -35f;
        public float maxPitch = 70f;
        [Tooltip("Takip yumuşatma süresi (sn).")]
        public float followSmoothTime = 0.08f;
        public float collisionRadius = 0.2f;
        public LayerMask collisionMask = ~0;

        public InputAction look = new InputAction("Look", InputActionType.Value);

        float yaw, pitch = 15f;
        Vector3 pivot, pivotVelocity;
        bool initialized;

        void Reset() => AddDefaultBindings();

        void AddDefaultBindings()
        {
            look = new InputAction("Look", InputActionType.Value);
            look.AddBinding("<Mouse>/delta");
            look.AddBinding("<Gamepad>/rightStick");
        }

        void Awake()
        {
            if (look.bindings.Count == 0) AddDefaultBindings();
            yaw = transform.eulerAngles.y;
        }

        void OnEnable()
        {
            look.Enable();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void OnDisable()
        {
            look.Disable();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void LateUpdate()
        {
            if (target == null || target.Hips == null) return;

            // --- Dönüş girdisi ---
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 delta = look.ReadValue<Vector2>();
                bool fromGamepad = look.activeControl != null && look.activeControl.device is Gamepad;
                float scale = fromGamepad ? gamepadSensitivity * Time.deltaTime : mouseSensitivity;
                yaw += delta.x * scale;
                pitch -= delta.y * scale;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }

            // --- Takip ---
            Vector3 desiredPivot = target.Hips.transform.position + pivotOffset;
            if (!initialized) { pivot = desiredPivot; initialized = true; }
            pivot = Vector3.SmoothDamp(pivot, desiredPivot, ref pivotVelocity, followSmoothTime);

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 back = rotation * Vector3.back;

            // --- Duvar çarpışması (kendi ragdoll'umuzu yok say) ---
            float d = distance;
            var hits = Physics.SphereCastAll(pivot, collisionRadius, back, distance, collisionMask, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (target.IsOwnCollider(hit.collider)) continue;
                if (hit.rigidbody != null && hit.rigidbody.GetComponentInParent<ActiveRagdoll>() == target) continue;
                if (hit.distance > 0f) d = Mathf.Min(d, hit.distance);
            }

            transform.SetPositionAndRotation(pivot + back * d, rotation);
        }
    }
}
