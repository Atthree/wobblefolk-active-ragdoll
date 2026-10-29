using System;
using UnityEngine;

namespace Wobblefolk.Ragdoll
{
    [Serializable]
    public sealed class ClimbAnchorSettings
    {
        [Tooltip("Grips on kinematic rigidbodies count as climbable anchors.")]
        public bool kinematicIsAnchor = true;
        [Tooltip("Grips on rigidbodies at least this heavy count as climbable anchors (kg).")]
        [Min(0f)] public float minAnchorMass = 200f;
        [Tooltip("A grip counts as a ledge hold when it is at least this far above the chest (m).")]
        public float minHeightAboveChest = 0.05f;
    }

    [Serializable]
    public sealed class LedgeProbeSettings
    {
        [Tooltip("The ledge top is searched this far beyond the grip, away from the character (m).")]
        [Min(0f)] public float inset = 0.2f;
        [Tooltip("Highest ledge top above the grip that can still be climbed (m).")]
        [Min(0f)] public float maxHeightAboveGrip = 0.9f;
        [Tooltip("Steepest surface that counts as a ledge top (degrees).")]
        [Range(0f, 90f)] public float maxTopAngle = 40f;
    }

    [Serializable]
    public sealed class HangSettings
    {
        [Tooltip("Balance torque weight while hanging or climbing.")]
        [Range(0f, 1f)] public float balanceWeight = 0.15f;
    }

    [Serializable]
    public sealed class PullUpSettings
    {
        [Tooltip("Chest target height above the ledge top (m).")]
        public float chestHeightAboveLedge = 0.3f;
        [Tooltip("The chest is never pulled higher than this above the hands, which stay on their grip (m).")]
        public float maxChestAboveGrip = 0.45f;
        [Tooltip("Chest target distance back from the ledge, towards the character (m).")]
        public float wallOffset = 0.3f;
        [Tooltip("Pull acceleration per metre of error (1/s²).")]
        [Min(0f)] public float spring = 40f;
        [Tooltip("Pull acceleration per m/s of velocity (1/s).")]
        [Min(0f)] public float damper = 9f;
        [Tooltip("Upper bound of the pull acceleration, excluding gravity compensation (m/s²).")]
        [Min(0f)] public float maxAcceleration = 18f;
        [Tooltip("Fraction of gravity cancelled while climbing.")]
        [Range(0f, 1.5f)] public float gravityCompensation = 1f;
        [Tooltip("Fraction of the climb force applied to the driven body; the rest goes to the other torso body.")]
        [Range(0f, 1f)] public float drivenShare = 0.6f;
        [Tooltip("Pull-up is complete when the chest is within this distance below its target height (m).")]
        [Min(0f)] public float completeTolerance = 0.1f;
        [Min(0.1f)] public float timeout = 2.5f;
    }

    [Serializable]
    public sealed class MantleSettings
    {
        [Tooltip("How far past the ledge edge the pelvis is pushed (m).")]
        [Min(0f)] public float forwardDistance = 0.6f;
        [Tooltip("Pelvis target height above the ledge top (m).")]
        public float pelvisHeightAboveLedge = 0.75f;
        [Tooltip("Mantle completes once the pelvis is this far above the ledge top (m).")]
        public float completeClearance = 0.3f;
        [Tooltip("Ground this far below the ledge top still counts as standing on it (m).")]
        [Min(0f)] public float topTolerance = 0.1f;
        [Min(0.1f)] public float timeout = 2f;
    }

    /// <summary>Ledge hanging and pull-up tuning of an active ragdoll.</summary>
    [CreateAssetMenu(menuName = "Wobblefolk/Climb Profile", fileName = "ClimbProfile")]
    public sealed class ClimbProfile : ScriptableObject
    {
        public ClimbAnchorSettings anchors = new ClimbAnchorSettings();
        public LedgeProbeSettings ledge = new LedgeProbeSettings();
        public HangSettings hang = new HangSettings();
        public PullUpSettings pullUp = new PullUpSettings();
        public MantleSettings mantle = new MantleSettings();

        [Header("Input")]
        [Tooltip("A climb request is remembered this long, e.g. jump pressed just before grabbing the ledge (s).")]
        [Min(0f)] public float requestBufferTime = 0.25f;
        [Tooltip("Hands cannot grab again for this long after climbing over (s).")]
        [Min(0f)] public float regrabBlockTime = 0.6f;
    }
}
