using UnityEngine;

namespace Atthree.Ragdoll
{
    /// <summary>
    /// Ragdoll'a "ne yapmak istediğini" söyleyen veri.
    /// Oyuncu girişi (PlayerRagdollInput) ya da bir yapay zeka bu alanları doldurur;
    /// hareket ve kol scriptleri sadece buradan okur. Böylece girdi ile fizik birbirinden ayrı kalır.
    /// </summary>
    [System.Serializable]
    public class RagdollCommands
    {
        [Tooltip("Dünya uzayında, yatay hareket yönü. Uzunluk 0..1 (1 = tam hız).")]
        public Vector3 move;

        [Tooltip("Dünya uzayında bakış yönü (genelde kameranın forward'ı). Kollar buraya uzanır.")]
        public Vector3 lookDirection = Vector3.forward;

        [Tooltip("Sol / sağ kolu uzat ve dokunduğunu tut.")]
        public bool grabLeft;
        public bool grabRight;

        /// <summary>Zıplama isteği. Hareket scripti tüketince false yapar.</summary>
        public bool jumpRequested;
    }
}
