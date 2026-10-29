using UnityEngine;

namespace Wobblefolk.Ragdoll
{
    /// <summary>How carried weight slows, strains and unbalances an active ragdoll.</summary>
    [CreateAssetMenu(menuName = "Wobblefolk/Carry Profile", fileName = "CarryProfile")]
    public sealed class CarryProfile : ScriptableObject
    {
        [Header("Capacity (per holding hand)")]
        [Tooltip("Mass one hand carries without any penalty (kg). Two hands on an object double it.")]
        [Min(0f)] public float comfortableMassPerHand = 6f;
        [Tooltip("Mass at which one hand is at full strain (kg).")]
        [Min(0f)] public float maxMassPerHand = 18f;
        [Tooltip("Fraction of an object's mass that counts while it is dragged along the ground instead of lifted.")]
        [Range(0f, 1f)] public float dragMassFactor = 0.35f;
        [Tooltip("An object whose bottom is less than this above the ground counts as dragged (m).")]
        [Min(0f)] public float liftedClearance = 0.05f;

        [Header("Strain effects")]
        [Tooltip("Maps strain (0..1) to effect strength (0..1).")]
        public AnimationCurve strainCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [Tooltip("Speed and jump multiplier at full strain.")]
        [Range(0f, 1f)] public float minSpeedMultiplier = 0.3f;
        [Tooltip("Balance torque multiplier at full strain. Lower values make heavy loads pull the body around.")]
        [Range(0f, 1f)] public float minBalanceWeight = 0.7f;
        [Tooltip("Strain above which sprinting is disabled.")]
        [Range(0f, 1f)] public float noSprintStrain = 0.5f;
        [Tooltip("Fraction of a lifted object's weight added to the stand-height lift.")]
        [Range(0f, 1f)] public float liftSupport = 1f;

        [Header("Counter-lean")]
        [Tooltip("Lean away from a lifted load per kg·m of its horizontal offset from the pelvis (degrees).")]
        [Min(0f)] public float leanPerKilogramMetre = 1.2f;
        [Range(0f, 30f)] public float maxLean = 12f;

        [Header("Overload")]
        [Tooltip("Hands slip off a lifted load that stays above full capacity this long (s). 0 = never.")]
        [Min(0f)] public float overloadSlipTime = 1.5f;
        [Tooltip("Hands cannot grab again for this long after slipping (s).")]
        [Min(0f)] public float slipRegrabBlockTime = 0.75f;
    }
}
