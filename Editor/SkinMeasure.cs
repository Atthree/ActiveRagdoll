using System.Collections.Generic;
using UnityEngine;

namespace Atthree.Ragdoll.Editor
{
    /// <summary>
    /// Collider boyutlarını karakterin MESH'inden ölçer.
    ///
    /// Her vertex'in en çok etkilendiği kemiği bulur ve vertex'i dünya uzayına taşır
    /// (bone.localToWorld * bindpose * vertex). Böylece "bu kemiğe ait mesh ne kadar kalın,
    /// nereye kadar uzanıyor" sorusu cevaplanabilir. Mesh yoksa çağıranlar tahmine geri döner.
    /// </summary>
    public class SkinMeasure
    {
        const int MinPoints = 20;

        readonly Dictionary<Transform, List<Vector3>> pointsByBone = new Dictionary<Transform, List<Vector3>>();
        readonly HashSet<Transform> bodyBones;

        public SkinMeasure(GameObject root, RagdollBuilder.BoneMap map)
        {
            bodyBones = new HashSet<Transform>(map.parts.Values);

            // Sadece görünen mesh'ler (Synty prefab'ında 10 karakter var, biri aktif).
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                var mesh = smr.sharedMesh;
                if (mesh == null) continue;
                var vertices = mesh.vertices;
                var weights = mesh.boneWeights;
                var bindposes = mesh.bindposes;
                var bones = smr.bones;
                if (weights.Length != vertices.Length) continue;

                for (int i = 0; i < vertices.Length; i++)
                {
                    int b = weights[i].boneIndex0; // en yüksek ağırlıklı kemik
                    if (b < 0 || b >= bones.Length || b >= bindposes.Length || bones[b] == null) continue;
                    Vector3 world = bones[b].localToWorldMatrix.MultiplyPoint3x4(bindposes[b].MultiplyPoint3x4(vertices[i]));
                    if (!pointsByBone.TryGetValue(bones[b], out var list))
                        pointsByBone[bones[b]] = list = new List<Vector3>();
                    list.Add(world);
                }
            }
        }

        /// <summary>
        /// Kemiğin ve altındaki (başka bir ragdoll parçasına ulaşana kadar) kemiklerin vertex'leri.
        /// Ör. kalça için: Hips + Spine_01, ama bacaklar ve göğüs hariç.
        /// </summary>
        List<Vector3> PointsUnder(Transform bone)
        {
            var result = new List<Vector3>();
            Collect(bone, true, result);
            return result;
        }

        void Collect(Transform t, bool isStart, List<Vector3> result)
        {
            if (!isStart && bodyBones.Contains(t)) return;
            if (pointsByBone.TryGetValue(t, out var list)) result.AddRange(list);
            foreach (Transform child in t) Collect(child, false, result);
        }

        /// <summary>Bölgenin, verilen rotasyona hizalı sınır kutusu (dünya merkezi + boyut).</summary>
        public bool TryBox(Transform bone, Quaternion frame, out Vector3 center, out Vector3 size)
        {
            center = size = Vector3.zero;
            var points = PointsUnder(bone);
            if (points.Count < MinPoints) return false;

            Quaternion inverse = Quaternion.Inverse(frame);
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (var p in points)
            {
                Vector3 local = inverse * p;
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }
            center = frame * ((min + max) * 0.5f);
            size = max - min;
            return true;
        }

        /// <summary>
        /// Kemiğe ait vertex'lerin [from-to] doğru parçasına uzaklıklarının %70'lik dilimi.
        /// Ortalama yerine yüzdelik dilim: birkaç taşan vertex (kıyafet ucu vb.) yarıçapı şişirmesin.
        /// </summary>
        public float CapsuleRadius(Transform bone, Vector3 from, Vector3 to, float fallback)
        {
            if (!pointsByBone.TryGetValue(bone, out var points) || points.Count < MinPoints) return fallback;

            Vector3 axis = to - from;
            float lengthSq = Mathf.Max(axis.sqrMagnitude, 0.000001f);
            var distances = new List<float>(points.Count);
            foreach (var p in points)
            {
                float t = Mathf.Clamp01(Vector3.Dot(p - from, axis) / lengthSq);
                distances.Add(Vector3.Distance(p, from + axis * t));
            }
            distances.Sort();
            float radius = distances[(int)(distances.Count * 0.7f)];
            // Saçma sonuçlara karşı tahminin 0.4x - 2.5x aralığında tut.
            return Mathf.Clamp(radius, fallback * 0.4f, fallback * 2.5f);
        }
    }
}
