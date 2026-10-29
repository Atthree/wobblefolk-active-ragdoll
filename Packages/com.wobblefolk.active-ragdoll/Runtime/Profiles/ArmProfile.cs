using System;
using UnityEngine;

namespace Wobblefolk.Ragdoll
{
    [Serializable]
    public sealed class ReachSettings
    {
        [Tooltip("Muscle multiplier of the upper arm, lower arm and hand while reaching.")]
        [Min(0f)] public float muscleStrength = 4f;
        [Tooltip("Elbow bend while reaching (degrees). 0 = straight arm.")]
        public float elbowBend = 5f;
        public LayerMask aimLayers = ~0;
        [Tooltip("Furthest distance along the aim ray at which a surface becomes the reach target (m).")]
        [Min(0f)] public float maxAimDistance = 8f;
        [Tooltip("Reach target distance in front of the chest when the aim ray hits nothing (m).")]
        [Min(0f)] public float defaultReachDistance = 1.5f;
        [Tooltip("Turn the body towards the aim while reaching and not moving.")]
        public bool faceAim = true;
        [Tooltip("Turn rate towards the aim while reaching (degrees/s).")]
        [Min(0f)] public float faceAimTurnSpeed = 360f;

        [Header("Bending over")]
        [Tooltip("Aim angle below horizontal at which the body starts bending forward (degrees).")]
        [Range(0f, 90f)] public float bendStartAngle = 10f;
        [Tooltip("Body bend per degree of aim below the start angle.")]
        [Min(0f)] public float bendPerDegree = 1.6f;
        [Tooltip("Maximum total forward bend of the chest (degrees).")]
        [Range(0f, 120f)] public float maxBend = 90f;
        [Tooltip("Fraction of the bend done by tilting the pelvis; the spine does the rest.")]
        [Range(0f, 1f)] public float pelvisBendShare = 0.4f;
        [Range(0f, 60f)] public float maxPelvisBend = 35f;
        [Tooltip("How fast the bend follows its target (1/s).")]
        [Min(0f)] public float bendResponse = 6f;
    }

    [Serializable]
    public sealed class GrabSettings
    {
        public LayerMask grabLayers = ~0;
        [Tooltip("Extra radius around the hand collider in which surfaces can be grabbed (m).")]
        [Min(0f)] public float grabRadius = 0.06f;
        [Tooltip("Allow grabbing colliders without a Rigidbody (walls, ledges).")]
        public bool allowStatic = true;

        [Header("Grip")]
        [Tooltip("Force that tears the grip loose (N). 0 = unbreakable.")]
        [Min(0f)] public float breakForce = 4000f;
        [Tooltip("Torque that tears the grip loose (N·m). 0 = unbreakable.")]
        [Min(0f)] public float breakTorque = 0f;
        [Tooltip("Lock the held object's rotation to the hand. Off = it pivots around the grip point, which makes " +
                 "carried objects swing and spin.")]
        public bool lockRotation = true;
        [Tooltip("Rotational stiffness of the grip when rotation is not locked.")]
        [Min(0f)] public float angularSpring = 0f;
        [Min(0f)] public float angularDamper = 2f;
        [Tooltip("Above this held-mass to hand-mass ratio the held object looks lighter to the grip joint, which " +
                 "steadies very heavy objects but lets them be lifted more easily. 0 = off (physically accurate).")]
        [Min(0f)] public float maxSolverMassRatio = 0f;
        [Tooltip("Caps how fast a held object is pushed out of the body when they overlap (m/s). Unity's default is " +
                 "unlimited, which can launch the character when an object is pulled into the torso.")]
        [Min(0.1f)] public float heldMaxDepenetrationVelocity = 2f;
        [Tooltip("Solver iterations used by a held object. Matches the ragdoll's for a stable chain.")]
        [Range(1, 64)] public int heldSolverIterations = 12;
        [Tooltip("Delay after release before a held object's original physics settings are restored (s).")]
        [Min(0f)] public float restoreDelay = 1f;

        [Header("Behaviour")]
        [Tooltip("Delay before the same hand can grab again after releasing or breaking (s).")]
        [Min(0f)] public float regrabDelay = 0.25f;
        [Tooltip("Let go of everything when falling or knocked out.")]
        public bool releaseOnFall = true;
        [Tooltip("Disable collisions between the held object and the holding arm.")]
        public bool ignoreArmCollisions = true;
    }

    /// <summary>Reaching and grabbing tuning of an active ragdoll's arms.</summary>
    [CreateAssetMenu(menuName = "Wobblefolk/Arm Profile", fileName = "ArmProfile")]
    public sealed class ArmProfile : ScriptableObject
    {
        public ReachSettings reach = new ReachSettings();
        public GrabSettings grab = new GrabSettings();
    }
}
