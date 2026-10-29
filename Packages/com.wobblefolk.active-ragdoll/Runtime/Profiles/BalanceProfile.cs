using System;
using UnityEngine;

namespace Wobblefolk.Ragdoll
{
    [Serializable]
    public sealed class UprightSettings
    {
        [Tooltip("Torque per radian of tilt applied to the pelvis (N·m/rad).")]
        [Min(0f)] public float tiltSpring = 900f;
        [Tooltip("Torque per rad/s of tilting angular velocity (N·m·s/rad).")]
        [Min(0f)] public float tiltDamper = 90f;
        [Tooltip("Torque per radian of yaw error towards the desired facing (N·m/rad).")]
        [Min(0f)] public float yawSpring = 400f;
        [Min(0f)] public float yawDamper = 60f;
        [Tooltip("Upper bound of the pelvis balance torque (N·m). 0 = unlimited.")]
        [Min(0f)] public float maxTorque = 3000f;
        [Tooltip("Fraction of the pelvis balance torque also applied to the chest.")]
        [Range(0f, 2f)] public float chestWeight = 0.6f;
    }

    [Serializable]
    public sealed class HeightSettings
    {
        [Tooltip("Desired pelvis height above the ground (m). Match it to the character's leg length.")]
        [Min(0f)] public float standHeight = 0.96f;
        [Tooltip("Lift acceleration per metre of height error (1/s²).")]
        [Min(0f)] public float spring = 150f;
        [Tooltip("Lift acceleration per m/s of vertical velocity (1/s).")]
        [Min(0f)] public float damper = 20f;
        [Tooltip("Fraction of gravity cancelled while grounded. 1 = the legs carry no weight.")]
        [Range(0f, 1.5f)] public float gravityCompensation = 0.9f;
        [Tooltip("Upper bound of the lift acceleration applied to the total mass (m/s²).")]
        [Min(0f)] public float maxLiftAcceleration = 30f;
    }

    [Serializable]
    public sealed class GroundSettings
    {
        public LayerMask groundLayers = ~0;
        [Tooltip("Radius of the sphere cast below the pelvis.")]
        [Min(0.01f)] public float probeRadius = 0.15f;
        [Tooltip("The probe starts this far above the pelvis so it still works while lying on the floor.")]
        [Min(0f)] public float probeStartOffset = 0.3f;
        [Tooltip("Extra reach below the stand height that still counts as grounded.")]
        [Min(0f)] public float probeExtraDistance = 0.25f;
        [Tooltip("Foot ray length below the foot collider.")]
        [Min(0f)] public float footProbeDistance = 0.08f;
        [Range(0f, 90f)] public float maxGroundAngle = 55f;
    }

    [Serializable]
    public sealed class FallSettings
    {
        [Tooltip("Pelvis tilt that counts as losing balance (degrees).")]
        [Range(5f, 180f)] public float fallTiltAngle = 55f;
        [Tooltip("How long the tilt must persist before falling (s).")]
        [Min(0f)] public float fallTiltTime = 0.2f;
        [Tooltip("Relative impact speed on any bone that knocks the character down (m/s). 0 = disabled.")]
        [Min(0f)] public float knockdownImpactSpeed = 9f;
        [Tooltip("Relative impact speed on the head that knocks the character out (m/s). 0 = disabled.")]
        [Min(0f)] public float knockoutImpactSpeed = 13f;
        [Tooltip("Ignore foot impacts for knockdowns. Swinging feet hit the ground fast while sprinting.")]
        public bool ignoreFootImpacts = true;
        [Tooltip("Muscle strength multiplier while fallen.")]
        [Range(0f, 1f)] public float fallenMuscleStrength = 0.3f;
    }

    [Serializable]
    public sealed class GetUpSettings
    {
        [Tooltip("Time spent on the floor before trying to get up (s).")]
        [Min(0f)] public float delay = 1.2f;
        [Tooltip("Pelvis speed below which the character may start getting up (m/s).")]
        [Min(0f)] public float maxStartSpeed = 1.5f;
        [Tooltip("Time for balance and muscles to ramp back to full strength (s).")]
        [Min(0.01f)] public float duration = 1f;
        [Tooltip("Strength ramp over normalized get-up time.")]
        public AnimationCurve strengthCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("Pelvis tilt below which getting up is complete (degrees).")]
        [Range(0f, 90f)] public float completeTiltAngle = 20f;
        [Tooltip("Give up and fall again if not upright after this long (s).")]
        [Min(0.1f)] public float timeout = 3f;
    }

    [Serializable]
    public sealed class KnockoutSettings
    {
        [Min(0f)] public float defaultDuration = 4f;
        [Range(0f, 1f)] public float muscleStrength = 0.05f;
    }

    /// <summary>Balance, ground detection, falling and recovery tuning of an active ragdoll.</summary>
    [CreateAssetMenu(menuName = "Wobblefolk/Balance Profile", fileName = "BalanceProfile")]
    public sealed class BalanceProfile : ScriptableObject
    {
        public UprightSettings upright = new UprightSettings();
        public HeightSettings height = new HeightSettings();
        public GroundSettings ground = new GroundSettings();
        public FallSettings fall = new FallSettings();
        public GetUpSettings getUp = new GetUpSettings();
        public KnockoutSettings knockout = new KnockoutSettings();
    }
}
