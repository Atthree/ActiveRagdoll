using UnityEngine;

namespace Atthree.Ragdoll
{
    /// <summary>
    /// ConfigurableJoint.targetRotation'ı "insan gibi" kullanabilmek için yardımcı.
    ///
    /// Sorun: targetRotation, joint'in KENDİ eksen uzayında (axis / secondaryAxis) ve
    /// joint oluşturulduğu andaki duruşa GÖRE tanımlanır. Yani "bacağı 30° öne al" demek için
    /// doğrudan Quaternion.Euler(30,0,0) yazamazsın.
    ///
    /// Bu fonksiyon: "kemiğin bağlı olduğu gövdeye göre şu rotasyonda olmasını istiyorum"
    /// bilgisini joint uzayına çevirir.
    /// </summary>
    public static class ConfigurableJointExtensions
    {
        /// <param name="targetLocalRotation">İstenen rotasyon (connectedBody'ye göre).</param>
        /// <param name="startLocalRotation">Joint oluşturulduğu andaki rotasyon (connectedBody'ye göre).</param>
        public static void SetTargetRotationLocal(this ConfigurableJoint joint,
            Quaternion targetLocalRotation, Quaternion startLocalRotation)
        {
            // 1) Joint uzayının eksenlerini kur: X = axis, Y = secondaryAxis'ten türetilen "up".
            Vector3 right = joint.axis;
            Vector3 forward = Vector3.Cross(joint.axis, joint.secondaryAxis).normalized;
            Vector3 up = Vector3.Cross(forward, right).normalized;
            Quaternion worldToJointSpace = Quaternion.LookRotation(forward, up);

            // 2) Başlangıca göre fark rotasyonunu joint uzayına taşı.
            //    (PhysX hedefi "tersten" yorumlar; bu yüzden Inverse(target) * start.)
            Quaternion result = Quaternion.Inverse(worldToJointSpace);
            result *= Quaternion.Inverse(targetLocalRotation) * startLocalRotation;
            result *= worldToJointSpace;

            joint.targetRotation = result;
        }
    }
}
