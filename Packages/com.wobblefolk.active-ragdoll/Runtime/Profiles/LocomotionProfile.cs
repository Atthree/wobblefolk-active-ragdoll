using System;
using UnityEngine;

namespace Wobblefolk.Ragdoll
{
    [Serializable]
    public sealed class MovementSettings
    {
        [Min(0f)] public float walkSpeed = 2.4f;
        [Min(0f)] public float sprintSpeed = 4.2f;
        [Tooltip("Horizontal acceleration towards the target speed while grounded (m/s²).")]
        [Min(0f)] public float acceleration = 14f;
        [Tooltip("Horizontal deceleration when there is no input while grounded (m/s²).")]
        [Min(0f)] public float deceleration = 18f;
        [Tooltip("Horizontal acceleration while airborne (m/s²).")]
        [Min(0f)] public float airAcceleration = 4f;
        [Tooltip("Turn rate of the body facing towards the move direction (degrees/s).")]
        [Min(0f)] public float turnSpeed = 540f;
        [Tooltip("Fraction of the movement force applied to the chest instead of the pelvis.")]
        [Range(0f, 1f)] public float chestForceShare = 0.4f;
    }

    [Serializable]
    public sealed class LeanSettings
    {
        [Tooltip("Lean towards the acceleration direction (degrees per m/s²).")]
        [Min(0f)] public float perAcceleration = 0.8f;
        [Tooltip("Forward lean at sprint speed (degrees).")]
        [Min(0f)] public float atSprintSpeed = 8f;
        [Range(0f, 45f)] public float max = 15f;
        [Tooltip("How fast the lean follows its target (1/s).")]
        [Min(0f)] public float response = 8f;
    }

    [Serializable]
    public sealed class GaitSettings
    {
        [Tooltip("Distance covered by one full gait cycle (left + right step) at walking speed (m).")]
        [Min(0.05f)] public float strideLength = 1.1f;
        [Tooltip("Gait cycles per second are clamped to this range.")]
        [Min(0f)] public float minCadence = 1.1f;
        [Min(0f)] public float maxCadence = 2.4f;
        [Tooltip("Horizontal speed below which the legs return to the standing pose (m/s).")]
        [Min(0f)] public float idleSpeed = 0.25f;
        [Tooltip("How fast the gait fades in and out (1/s).")]
        [Min(0f)] public float blendSpeed = 6f;

        [Header("Leg angles at walk / sprint speed (degrees)")]
        public float hipSwingWalk = 30f;
        public float hipSwingSprint = 45f;
        [Tooltip("Knee bend while the leg swings forward.")]
        public float kneeSwingWalk = 55f;
        public float kneeSwingSprint = 85f;
        [Tooltip("Knee bend while the leg supports the body.")]
        public float kneeStance = 6f;
        [Tooltip("0 = foot follows the shin, 1 = foot stays level with the pelvis.")]
        [Range(0f, 1f)] public float ankleCompensation = 0.8f;

        [Header("Arms")]
        [Tooltip("Arm swing opposite to the legs at walk / sprint speed (degrees).")]
        public float armSwingWalk = 20f;
        public float armSwingSprint = 40f;
        [Tooltip("Elbow bend while walking (degrees).")]
        public float elbowBend = 15f;

        [Header("Feet")]
        [Tooltip("Foot friction. Low values let the pelvis drive motion without the feet snagging.")]
        [Range(0f, 1f)] public float footFriction = 0.25f;
    }

    [Serializable]
    public sealed class StepUpSettings
    {
        [Tooltip("Obstacles lower than this in front of the feet count as steps (m).")]
        [Min(0f)] public float maxStepHeight = 0.35f;
        [Tooltip("Look-ahead distance in the move direction (m).")]
        [Min(0f)] public float probeDistance = 0.45f;
        [Tooltip("Extra stand height while stepping up (m).")]
        [Min(0f)] public float heightBoost = 0.18f;
        [Tooltip("Extra knee bend while stepping up (degrees).")]
        public float extraKneeBend = 35f;
    }

    [Serializable]
    public sealed class JumpSettings
    {
        [Min(0f)] public float jumpHeight = 0.8f;
        [Tooltip("Jump is still allowed this long after leaving the ground (s).")]
        [Min(0f)] public float coyoteTime = 0.12f;
        [Tooltip("A jump pressed this long before landing is still performed (s).")]
        [Min(0f)] public float bufferTime = 0.15f;
        [Min(0f)] public float cooldown = 0.35f;
        [Tooltip("Maximum time the stand-height lift stays off after a jump (s).")]
        [Min(0f)] public float liftSuspendTime = 0.5f;
        [Tooltip("Pelvis height above stand height that still counts as grounded (m).")]
        [Min(0f)] public float groundedTolerance = 0.15f;

        [Header("Airborne pose (degrees)")]
        public float airHip = -25f;
        public float airKnee = 50f;
    }

    /// <summary>Walking, running, turning and jumping tuning of an active ragdoll.</summary>
    [CreateAssetMenu(menuName = "Wobblefolk/Locomotion Profile", fileName = "LocomotionProfile")]
    public sealed class LocomotionProfile : ScriptableObject
    {
        public MovementSettings movement = new MovementSettings();
        public LeanSettings lean = new LeanSettings();
        public GaitSettings gait = new GaitSettings();
        public StepUpSettings stepUp = new StepUpSettings();
        public JumpSettings jump = new JumpSettings();
    }
}
