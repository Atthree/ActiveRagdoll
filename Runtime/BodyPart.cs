namespace Atthree.Ragdoll
{
    /// <summary>
    /// Ragdoll'daki fiziksel parçalar. Her parça = 1 Rigidbody (+ Hips hariç 1 ConfigurableJoint).
    /// Sıralama önemli değil; kod parçaları isimle değil bu enum ile bulur.
    /// </summary>
    public enum BodyPart
    {
        Hips,
        Chest,
        Head,

        LeftUpperLeg,
        LeftLowerLeg,
        RightUpperLeg,
        RightLowerLeg,

        LeftUpperArm,
        LeftLowerArm,
        LeftHand,
        RightUpperArm,
        RightLowerArm,
        RightHand,
    }
}
