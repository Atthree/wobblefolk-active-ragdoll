namespace Wobblefolk.Ragdoll
{
    /// <summary>Humanoid bones driven by the active ragdoll. Values are used as array indices.</summary>
    public enum BoneId
    {
        Pelvis = 0,
        Chest = 1,
        Head = 2,
        UpperArmLeft = 3,
        LowerArmLeft = 4,
        HandLeft = 5,
        UpperArmRight = 6,
        LowerArmRight = 7,
        HandRight = 8,
        UpperLegLeft = 9,
        LowerLegLeft = 10,
        FootLeft = 11,
        UpperLegRight = 12,
        LowerLegRight = 13,
        FootRight = 14,
    }

    public enum BodySide
    {
        Left = 0,
        Right = 1,
    }

    public static class BoneIds
    {
        public const int Count = 15;

        public static BoneId Foot(BodySide side) => side == BodySide.Left ? BoneId.FootLeft : BoneId.FootRight;
        public static BoneId UpperLeg(BodySide side) => side == BodySide.Left ? BoneId.UpperLegLeft : BoneId.UpperLegRight;
        public static BoneId LowerLeg(BodySide side) => side == BodySide.Left ? BoneId.LowerLegLeft : BoneId.LowerLegRight;
        public static BoneId UpperArm(BodySide side) => side == BodySide.Left ? BoneId.UpperArmLeft : BoneId.UpperArmRight;
        public static BoneId LowerArm(BodySide side) => side == BodySide.Left ? BoneId.LowerArmLeft : BoneId.LowerArmRight;
        public static BoneId Hand(BodySide side) => side == BodySide.Left ? BoneId.HandLeft : BoneId.HandRight;

        /// <summary>Parent bone in the default humanoid hierarchy. The pelvis returns itself.</summary>
        public static BoneId Parent(BoneId id)
        {
            switch (id)
            {
                case BoneId.Chest: return BoneId.Pelvis;
                case BoneId.Head: return BoneId.Chest;
                case BoneId.UpperArmLeft: return BoneId.Chest;
                case BoneId.LowerArmLeft: return BoneId.UpperArmLeft;
                case BoneId.HandLeft: return BoneId.LowerArmLeft;
                case BoneId.UpperArmRight: return BoneId.Chest;
                case BoneId.LowerArmRight: return BoneId.UpperArmRight;
                case BoneId.HandRight: return BoneId.LowerArmRight;
                case BoneId.UpperLegLeft: return BoneId.Pelvis;
                case BoneId.LowerLegLeft: return BoneId.UpperLegLeft;
                case BoneId.FootLeft: return BoneId.LowerLegLeft;
                case BoneId.UpperLegRight: return BoneId.Pelvis;
                case BoneId.LowerLegRight: return BoneId.UpperLegRight;
                case BoneId.FootRight: return BoneId.LowerLegRight;
                default: return BoneId.Pelvis;
            }
        }
    }
}
