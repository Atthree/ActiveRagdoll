using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Atthree.Ragdoll.Editor
{
    /// <summary>
    /// Sahnedeki bir Humanoid karakteri tek tıkla active ragdoll'a çevirir:
    /// kemiklere Rigidbody + Collider + ConfigurableJoint + RagdollBone ekler,
    /// köke ActiveRagdoll / Locomotion / Arms / PlayerRagdollInput koyar.
    ///
    /// Kemikleri önce Animator'ın Humanoid avatar'ından bulur, yoksa isimden tahmin eder.
    /// Tüm işlemler Undo'ya kaydedilir (Ctrl+Z ile geri alınabilir).
    /// </summary>
    public static class RagdollBuilder
    {
        public const string ColliderObjectName = "RagdollCollider";

        public class Options
        {
            public float totalMass = 50f;
            public bool addPlayerInput = true;
            public bool setupCamera = true;
        }

        /// <summary>Bulunan kemikler. Ekstra noktalar sadece collider boyutu için kullanılır.</summary>
        public class BoneMap
        {
            public readonly Dictionary<BodyPart, Transform> parts = new Dictionary<BodyPart, Transform>();
            public Transform neck, leftFoot, rightFoot, leftFinger, rightFinger;

            public Transform this[BodyPart p] => parts.TryGetValue(p, out var t) ? t : null;
        }

        // Her parçanın vücut kütlesindeki payı (toplam ~1).
        static readonly Dictionary<BodyPart, float> MassShare = new Dictionary<BodyPart, float>
        {
            { BodyPart.Hips, 0.16f }, { BodyPart.Chest, 0.22f }, { BodyPart.Head, 0.08f },
            { BodyPart.LeftUpperLeg, 0.10f }, { BodyPart.RightUpperLeg, 0.10f },
            { BodyPart.LeftLowerLeg, 0.07f }, { BodyPart.RightLowerLeg, 0.07f },
            { BodyPart.LeftUpperArm, 0.04f }, { BodyPart.RightUpperArm, 0.04f },
            { BodyPart.LeftLowerArm, 0.03f }, { BodyPart.RightLowerArm, 0.03f },
            { BodyPart.LeftHand, 0.015f }, { BodyPart.RightHand, 0.015f },
        };

        // Hangi parça hangisine bağlı (joint'in connectedBody'si).
        static readonly Dictionary<BodyPart, BodyPart> ParentOf = new Dictionary<BodyPart, BodyPart>
        {
            { BodyPart.Chest, BodyPart.Hips }, { BodyPart.Head, BodyPart.Chest },
            { BodyPart.LeftUpperLeg, BodyPart.Hips }, { BodyPart.LeftLowerLeg, BodyPart.LeftUpperLeg },
            { BodyPart.RightUpperLeg, BodyPart.Hips }, { BodyPart.RightLowerLeg, BodyPart.RightUpperLeg },
            { BodyPart.LeftUpperArm, BodyPart.Chest }, { BodyPart.LeftLowerArm, BodyPart.LeftUpperArm },
            { BodyPart.LeftHand, BodyPart.LeftLowerArm },
            { BodyPart.RightUpperArm, BodyPart.Chest }, { BodyPart.RightLowerArm, BodyPart.RightUpperArm },
            { BodyPart.RightHand, BodyPart.RightLowerArm },
        };

        // ===================================================================== KEMİK BULMA
        public static BoneMap FindBones(GameObject root, out string error)
        {
            var map = new BoneMap();
            var animator = root.GetComponentInChildren<Animator>();

            if (animator != null && animator.isHuman)
            {
                Transform B(HumanBodyBones b) => animator.GetBoneTransform(b);
                Set(map, BodyPart.Hips, B(HumanBodyBones.Hips));
                Set(map, BodyPart.Chest, B(HumanBodyBones.Chest) ?? B(HumanBodyBones.Spine));
                Set(map, BodyPart.Head, B(HumanBodyBones.Head));
                Set(map, BodyPart.LeftUpperLeg, B(HumanBodyBones.LeftUpperLeg));
                Set(map, BodyPart.LeftLowerLeg, B(HumanBodyBones.LeftLowerLeg));
                Set(map, BodyPart.RightUpperLeg, B(HumanBodyBones.RightUpperLeg));
                Set(map, BodyPart.RightLowerLeg, B(HumanBodyBones.RightLowerLeg));
                Set(map, BodyPart.LeftUpperArm, B(HumanBodyBones.LeftUpperArm));
                Set(map, BodyPart.LeftLowerArm, B(HumanBodyBones.LeftLowerArm));
                Set(map, BodyPart.LeftHand, B(HumanBodyBones.LeftHand));
                Set(map, BodyPart.RightUpperArm, B(HumanBodyBones.RightUpperArm));
                Set(map, BodyPart.RightLowerArm, B(HumanBodyBones.RightLowerArm));
                Set(map, BodyPart.RightHand, B(HumanBodyBones.RightHand));
                map.neck = B(HumanBodyBones.Neck);
                map.leftFoot = B(HumanBodyBones.LeftFoot);
                map.rightFoot = B(HumanBodyBones.RightFoot);
                map.leftFinger = B(HumanBodyBones.LeftMiddleProximal) ?? B(HumanBodyBones.LeftIndexProximal);
                map.rightFinger = B(HumanBodyBones.RightMiddleProximal) ?? B(HumanBodyBones.RightIndexProximal);
            }
            else
            {
                // Humanoid değilse isimden tahmin et (Synty / Mixamo / Unity wizard isimleri).
                var all = root.GetComponentsInChildren<Transform>(true);
                Set(map, BodyPart.Hips, Find(all, "Hips", "Pelvis", "mixamorig:Hips"));
                Set(map, BodyPart.Chest, Find(all, "Spine_02", "Spine2", "Chest", "Spine1", "Spine_01", "Spine"));
                Set(map, BodyPart.Head, Find(all, "Head"));
                Set(map, BodyPart.LeftUpperLeg, Find(all, "UpperLeg_L", "LeftUpLeg", "Thigh_L", "L Thigh", "LeftUpperLeg"));
                Set(map, BodyPart.LeftLowerLeg, Find(all, "LowerLeg_L", "LeftLeg", "Calf_L", "L Calf", "LeftLowerLeg"));
                Set(map, BodyPart.RightUpperLeg, Find(all, "UpperLeg_R", "RightUpLeg", "Thigh_R", "R Thigh", "RightUpperLeg"));
                Set(map, BodyPart.RightLowerLeg, Find(all, "LowerLeg_R", "RightLeg", "Calf_R", "R Calf", "RightLowerLeg"));
                Set(map, BodyPart.LeftUpperArm, Find(all, "Shoulder_L", "LeftArm", "UpperArm_L", "L UpperArm", "LeftUpperArm"));
                Set(map, BodyPart.LeftLowerArm, Find(all, "Elbow_L", "LeftForeArm", "LowerArm_L", "L Forearm", "LeftLowerArm"));
                Set(map, BodyPart.LeftHand, Find(all, "Hand_L", "LeftHand", "L Hand"));
                Set(map, BodyPart.RightUpperArm, Find(all, "Shoulder_R", "RightArm", "UpperArm_R", "R UpperArm", "RightUpperArm"));
                Set(map, BodyPart.RightLowerArm, Find(all, "Elbow_R", "RightForeArm", "LowerArm_R", "R Forearm", "RightLowerArm"));
                Set(map, BodyPart.RightHand, Find(all, "Hand_R", "RightHand", "R Hand"));
                map.neck = Find(all, "Neck");
                map.leftFoot = Find(all, "Ankle_L", "LeftFoot", "Foot_L", "L Foot");
                map.rightFoot = Find(all, "Ankle_R", "RightFoot", "Foot_R", "R Foot");
            }

            var missing = new List<string>();
            foreach (BodyPart p in System.Enum.GetValues(typeof(BodyPart)))
            {
                // Eller opsiyonel; diğerleri şart.
                if (map[p] == null && p != BodyPart.LeftHand && p != BodyPart.RightHand) missing.Add(p.ToString());
            }
            error = missing.Count > 0 ? "Bulunamayan kemikler: " + string.Join(", ", missing) : null;
            return map;
        }

        static void Set(BoneMap map, BodyPart part, Transform t)
        {
            if (t != null) map.parts[part] = t;
        }

        static Transform Find(Transform[] all, params string[] names)
        {
            foreach (var n in names)
                foreach (var t in all)
                    if (string.Equals(t.name, n, System.StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }

        // ===================================================================== KURULUM
        public static ActiveRagdoll Build(GameObject root, Options options)
        {
            var map = FindBones(root, out string error);
            if (error != null)
            {
                Debug.LogError("[RagdollBuilder] " + error, root);
                return null;
            }

            Undo.SetCurrentGroupName("Build Active Ragdoll");
            int group = Undo.GetCurrentGroup();

            Remove(root); // tekrar kurulumda eskisini temizle

            Transform r = root.transform;
            Vector3 up = r.up, right = r.right, forward = r.forward;

            // ---- Ölçüler (her şey kemik mesafelerinden türetilir; ölçekten bağımsız) ----
            Vector3 P(BodyPart p) => map[p].position;
            float hipWidth = Vector3.Distance(P(BodyPart.LeftUpperLeg), P(BodyPart.RightUpperLeg));
            float shoulderWidth = Vector3.Distance(P(BodyPart.LeftUpperArm), P(BodyPart.RightUpperArm));
            Vector3 neckPos = map.neck != null ? map.neck.position : Vector3.Lerp(P(BodyPart.Chest), P(BodyPart.Head), 0.7f);
            Vector3 leftFoot = map.leftFoot != null ? map.leftFoot.position : P(BodyPart.LeftLowerLeg) - up * hipWidth * 2f;
            Vector3 rightFoot = map.rightFoot != null ? map.rightFoot.position : P(BodyPart.RightLowerLeg) - up * hipWidth * 2f;
            float floorY = Vector3.Dot(r.position, up);

            // ---- 1) Rigidbody'ler ----
            var bodies = new Dictionary<BodyPart, Rigidbody>();
            foreach (var pair in map.parts)
            {
                var rb = Undo.AddComponent<Rigidbody>(pair.Value.gameObject);
                rb.mass = options.totalMass * MassShare[pair.Key];
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.solverIterations = 12;
                rb.solverVelocityIterations = 4;
                rb.maxAngularVelocity = 30f;
                rb.angularDamping = 0.5f;
                bodies[pair.Key] = rb;
            }

            // ---- 2) Collider'lar ----
            // Önce mesh'in vertex'lerinden ölçü almayı dener (SkinMeasure); mesh yoksa
            // kemik mesafelerinden tahmin eder.
            var skin = new SkinMeasure(root, map);

            // Kalça: mesh'teki kalça bölgesi, yoksa kalçanın biraz altından göğse kadar kutu.
            if (!skin.TryBox(map[BodyPart.Hips], r.rotation, out Vector3 hipsCenter, out Vector3 hipsSize))
            {
                float bottom = Vector3.Dot(P(BodyPart.Hips), up) - hipWidth * 0.4f;
                float top = Vector3.Dot(P(BodyPart.Chest), up);
                hipsCenter = P(BodyPart.Hips) + up * ((bottom + top) * 0.5f - Vector3.Dot(P(BodyPart.Hips), up));
                hipsSize = new Vector3(hipWidth * 1.9f, top - bottom, hipWidth * 1.2f);
            }
            AddBox(map[BodyPart.Hips], hipsCenter, r.rotation, hipsSize);

            // Göğüs.
            if (!skin.TryBox(map[BodyPart.Chest], r.rotation, out Vector3 chestCenter, out Vector3 chestSize))
            {
                float height = Mathf.Max(Vector3.Dot(neckPos - P(BodyPart.Chest), up), hipWidth);
                chestCenter = P(BodyPart.Chest) + up * height * 0.5f;
                chestSize = new Vector3(shoulderWidth * 0.8f, height, hipWidth * 1.4f);
            }
            AddBox(map[BodyPart.Chest], chestCenter, r.rotation, chestSize);

            // Kafa: küre.
            if (skin.TryBox(map[BodyPart.Head], r.rotation, out Vector3 headCenter, out Vector3 headSize))
            {
                AddSphere(map[BodyPart.Head], headCenter, (headSize.x + headSize.y + headSize.z) / 6f);
            }
            else
            {
                float radius = Mathf.Max(Vector3.Distance(neckPos, P(BodyPart.Head)) * 1.1f, shoulderWidth * 0.28f);
                AddSphere(map[BodyPart.Head], P(BodyPart.Head) + up * radius * 0.8f, radius);
            }

            // Bacaklar: kapsül + ayak kutusu (yere basan kısım).
            AddLeg(map, skin, BodyPart.LeftUpperLeg, BodyPart.LeftLowerLeg, map.leftFoot, leftFoot, hipWidth, floorY, up, forward, r.rotation);
            AddLeg(map, skin, BodyPart.RightUpperLeg, BodyPart.RightLowerLeg, map.rightFoot, rightFoot, hipWidth, floorY, up, forward, r.rotation);
            // Kollar.
            AddArm(map, skin, BodyPart.LeftUpperArm, BodyPart.LeftLowerArm, BodyPart.LeftHand, map.leftFinger, r.rotation);
            AddArm(map, skin, BodyPart.RightUpperArm, BodyPart.RightLowerArm, BodyPart.RightHand, map.rightFinger, r.rotation);

            // ---- 3) Joint'ler ----
            //          parça                        eksen  ikincil   lowX  highX   Y    Z
            AddJoint(map, bodies, BodyPart.Chest,         right, up,     -35f,  35f,  30f, 25f);
            AddJoint(map, bodies, BodyPart.Head,          right, up,     -50f,  50f,  60f, 30f);
            AddJoint(map, bodies, BodyPart.LeftUpperLeg,  right, up,     -30f, 100f,  20f, 35f);
            AddJoint(map, bodies, BodyPart.RightUpperLeg, right, up,     -30f, 100f,  20f, 35f);
            AddJoint(map, bodies, BodyPart.LeftLowerLeg,  right, up,    -130f,   5f,  10f, 10f);
            AddJoint(map, bodies, BodyPart.RightLowerLeg, right, up,    -130f,   5f,  10f, 10f);
            AddJoint(map, bodies, BodyPart.LeftUpperArm,  up, forward,  -110f, 110f,  90f, 60f);
            AddJoint(map, bodies, BodyPart.RightUpperArm, up, forward,  -110f, 110f,  90f, 60f);
            AddJoint(map, bodies, BodyPart.LeftLowerArm,  up, forward,  -140f, 140f,  10f, 30f);
            AddJoint(map, bodies, BodyPart.RightLowerArm, up, forward,  -140f, 140f,  10f, 30f);
            AddJoint(map, bodies, BodyPart.LeftHand,      up, forward,   -60f,  60f,  40f, 40f);
            AddJoint(map, bodies, BodyPart.RightHand,     up, forward,   -60f,  60f,  40f, 40f);

            // ---- 4) RagdollBone işaretleri ----
            foreach (var pair in map.parts)
            {
                var bone = Undo.AddComponent<RagdollBone>(pair.Value.gameObject);
                var so = new SerializedObject(bone);
                so.FindProperty("part").enumValueIndex = (int)pair.Key;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // ---- 5) Kök scriptleri ----
            var ragdoll = GetOrAdd<ActiveRagdoll>(root);
            GetOrAdd<ActiveRagdollLocomotion>(root);
            GetOrAdd<ActiveRagdollArms>(root);
            if (options.addPlayerInput) GetOrAdd<PlayerRagdollInput>(root);

            // Animator kemikleri ezer: kapat.
            var animator = root.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                Undo.RecordObject(animator, "Disable Animator");
                animator.enabled = false;
            }

            if (options.setupCamera) SetupCamera(ragdoll);

            Undo.CollapseUndoOperations(group);
            EditorUtility.SetDirty(root);
            Debug.Log($"[RagdollBuilder] '{root.name}' active ragdoll olarak kuruldu ({map.parts.Count} parça, {options.totalMass} kg).", root);
            return ragdoll;
        }

        // ---------------------------------------------------------------- collider yardımcıları
        static void AddLeg(BoneMap map, SkinMeasure skin, BodyPart upperPart, BodyPart lowerPart, Transform footBone,
            Vector3 foot, float hipWidth, float floorY, Vector3 up, Vector3 forward, Quaternion rootRotation)
        {
            Transform upper = map[upperPart], lower = map[lowerPart];
            AddCapsule(upper, upper.position, lower.position,
                skin.CapsuleRadius(upper, upper.position, lower.position, hipWidth * 0.45f));
            AddCapsule(lower, lower.position, foot,
                skin.CapsuleRadius(lower, lower.position, foot, hipWidth * 0.35f));

            // Ayak: mesh'ten ölç, yoksa ayak bileğinden yere kadar öne uzanan kutu.
            if (footBone != null && skin.TryBox(footBone, rootRotation, out Vector3 center, out Vector3 size))
            {
                AddBox(lower, center, rootRotation, size);
                return;
            }
            float ankleHeight = Mathf.Max(Vector3.Dot(foot, up) - floorY, hipWidth * 0.3f);
            float length = hipWidth * 1.3f;
            center = foot - up * (ankleHeight * 0.5f) + forward * (length * 0.25f);
            AddBox(lower, center, rootRotation, new Vector3(hipWidth * 0.55f, ankleHeight, length));
        }

        static void AddArm(BoneMap map, SkinMeasure skin, BodyPart upperPart, BodyPart lowerPart, BodyPart handPart,
            Transform finger, Quaternion rootRotation)
        {
            Transform upper = map[upperPart], lower = map[lowerPart], hand = map[handPart];
            float upperLength = Vector3.Distance(upper.position, lower.position);
            AddCapsule(upper, upper.position, lower.position,
                skin.CapsuleRadius(upper, upper.position, lower.position, upperLength * 0.22f));

            if (hand == null)
            {
                Vector3 end = lower.position + (lower.position - upper.position);
                AddCapsule(lower, lower.position, end, upperLength * 0.2f);
                return;
            }

            AddCapsule(lower, lower.position, hand.position,
                skin.CapsuleRadius(lower, lower.position, hand.position, upperLength * 0.2f));

            if (skin.TryBox(hand, rootRotation, out Vector3 handCenter, out Vector3 handSize))
            {
                AddSphere(hand, handCenter, (handSize.x + handSize.y + handSize.z) / 6f);
                return;
            }
            Vector3 handDirection = (hand.position - lower.position).normalized;
            Vector3 tip = finger != null ? finger.position : hand.position + handDirection * upperLength * 0.3f;
            float radius = Mathf.Max(Vector3.Distance(hand.position, tip) * 0.6f, upperLength * 0.15f);
            AddSphere(hand, Vector3.Lerp(hand.position, tip, 0.6f), radius);
        }

        static GameObject CreateColliderObject(Transform bone, Vector3 worldPosition, Quaternion worldRotation)
        {
            var go = new GameObject(ColliderObjectName);
            Undo.RegisterCreatedObjectUndo(go, "Ragdoll Collider");
            go.transform.SetParent(bone, false);
            go.transform.SetPositionAndRotation(worldPosition, worldRotation);
            // Dünya ölçeği 1 olsun ki collider boyutları metre cinsinden olsun.
            Vector3 s = bone.lossyScale;
            go.transform.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
            go.layer = bone.gameObject.layer;
            return go;
        }

        static void AddCapsule(Transform bone, Vector3 from, Vector3 to, float radius)
        {
            Vector3 dir = to - from;
            float length = dir.magnitude;
            if (length < 0.0001f) return;
            Vector3 upHint = Mathf.Abs(Vector3.Dot(dir.normalized, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up;
            var go = CreateColliderObject(bone, (from + to) * 0.5f, Quaternion.LookRotation(dir, upHint));
            var c = go.AddComponent<CapsuleCollider>();
            c.direction = 2; // Z
            c.radius = radius;
            c.height = length + radius; // uçlar komşu parçaya biraz binsin
        }

        static void AddBox(Transform bone, Vector3 center, Quaternion rotation, Vector3 size)
        {
            var go = CreateColliderObject(bone, center, rotation);
            go.AddComponent<BoxCollider>().size = size;
        }

        static void AddSphere(Transform bone, Vector3 center, float radius)
        {
            var go = CreateColliderObject(bone, center, Quaternion.identity);
            go.AddComponent<SphereCollider>().radius = radius;
        }

        // ---------------------------------------------------------------- joint yardımcısı
        static void AddJoint(BoneMap map, Dictionary<BodyPart, Rigidbody> bodies, BodyPart part,
            Vector3 worldAxis, Vector3 worldSecondaryAxis, float lowX, float highX, float limitY, float limitZ)
        {
            if (!bodies.TryGetValue(part, out var body)) return;
            if (!bodies.TryGetValue(ParentOf[part], out var parent)) return;

            var t = map[part];
            var joint = Undo.AddComponent<ConfigurableJoint>(t.gameObject);
            joint.connectedBody = parent;
            joint.axis = t.InverseTransformDirection(worldAxis).normalized;
            joint.secondaryAxis = t.InverseTransformDirection(worldSecondaryAxis).normalized;

            // Pozisyon kilitli (kemikler ayrılmaz), rotasyon sınırlı.
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Limited;
            joint.lowAngularXLimit = new SoftJointLimit { limit = lowX };
            joint.highAngularXLimit = new SoftJointLimit { limit = highX };
            joint.angularYLimit = new SoftJointLimit { limit = limitY };
            joint.angularZLimit = new SoftJointLimit { limit = limitZ };

            // Hedef rotasyonu tek bir "slerp" yayı ile takip et (ActiveRagdoll gücünü ayarlar).
            joint.rotationDriveMode = RotationDriveMode.Slerp;
            joint.enablePreprocessing = false; // ragdoll'larda titremeyi azaltır
            joint.enableCollision = false;
        }

        // ---------------------------------------------------------------- diğer
        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : Undo.AddComponent<T>(go);
        }

        static void SetupCamera(ActiveRagdoll ragdoll)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                go.tag = "MainCamera";
                Undo.RegisterCreatedObjectUndo(go, "Create Camera");
                cam = go.GetComponent<Camera>();
            }
            var follow = GetOrAdd<RagdollCamera>(cam.gameObject);
            Undo.RecordObject(follow, "Assign Ragdoll Camera");
            follow.target = ragdoll;
            cam.transform.SetPositionAndRotation(
                ragdoll.transform.position + ragdoll.transform.rotation * new Vector3(0f, 2f, -4f),
                Quaternion.LookRotation(ragdoll.transform.forward + Vector3.down * 0.3f));
        }

        // ===================================================================== KALDIRMA
        public static void Remove(GameObject root)
        {
            // Sadece bizim kurduğumuz kemiklere dokun (RagdollBone olanlar); kullanıcının
            // kendi Rigidbody'lerini (silah vb.) silmeyelim.
            var boneTransforms = new List<Transform>();
            foreach (var bone in root.GetComponentsInChildren<RagdollBone>(true))
                boneTransforms.Add(bone.transform);

            foreach (var t in boneTransforms)
            {
                DestroyAll<RagdollHand>(t);
                DestroyAll<RagdollBone>(t);
                DestroyAll<FixedJoint>(t);
                DestroyAll<ConfigurableJoint>(t);
            }
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name == ColliderObjectName) Undo.DestroyObjectImmediate(t.gameObject);
            foreach (var t in boneTransforms)
                DestroyAll<Rigidbody>(t);

            DestroyAll<PlayerRagdollInput>(root.transform);
            DestroyAll<ActiveRagdollArms>(root.transform);
            DestroyAll<ActiveRagdollLocomotion>(root.transform);
            DestroyAll<ActiveRagdoll>(root.transform);
        }

        static void DestroyAll<T>(Transform t) where T : Component
        {
            foreach (var c in t.GetComponents<T>()) Undo.DestroyObjectImmediate(c);
        }
    }
}
