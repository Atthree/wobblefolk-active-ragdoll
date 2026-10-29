using System;
using UnityEngine;

namespace Wobblefolk.Ragdoll
{
    [Serializable]
    public sealed class BoneSettings
    {
        public BoneId bone;

        [Min(0.01f)] public float mass = 1f;

        [Header("Joint frame (bone local space)")]
        [Tooltip("Primary joint axis. Angular X limits rotate around this axis.")]
        public Vector3 axis = Vector3.right;
        [Tooltip("Secondary joint axis. Angular Y limit rotates around this axis.")]
        public Vector3 secondaryAxis = Vector3.up;

        [Header("Angular limits (degrees)")]
        [Range(-177f, 0f)] public float lowAngularX = -30f;
        [Range(0f, 177f)] public float highAngularX = 30f;
        [Range(0f, 177f)] public float angularY = 30f;
        [Range(0f, 177f)] public float angularZ = 30f;

        [Header("Muscle (slerp drive)")]
        [Min(0f)] public float spring = 500f;
        [Min(0f)] public float damper = 30f;
        [Min(0f)] public float maxForce = 10000f;

        public BoneSettings Clone() => (BoneSettings)MemberwiseClone();
    }

    [Serializable]
    public sealed class BodyPhysicsSettings
    {
        [Tooltip("Per-body position solver iterations. Higher = stiffer joint chains, less jitter.")]
        [Range(1, 64)] public int solverIterations = 12;
        [Tooltip("Per-body velocity solver iterations. Helps bounce and contact stability.")]
        [Range(1, 64)] public int solverVelocityIterations = 4;
        [Min(1f)] public float maxAngularVelocity = 30f;
        [Min(0.1f)] public float maxDepenetrationVelocity = 5f;
        [Min(0f)] public float linearDamping = 0.05f;
        [Min(0f)] public float angularDamping = 0.5f;
        public RigidbodyInterpolation interpolation = RigidbodyInterpolation.Interpolate;
        public CollisionDetectionMode collisionDetection = CollisionDetectionMode.ContinuousSpeculative;

        [Header("Joints")]
        [Tooltip("Snaps joints back when they drift beyond the tolerances below. Prevents limbs stretching apart.")]
        public bool useProjection = true;
        [Min(0f)] public float projectionDistance = 0.05f;
        [Range(0f, 180f)] public float projectionAngle = 10f;
        [Tooltip("PhysX preprocessing. Disabling reduces jitter in long joint chains.")]
        public bool enablePreprocessing = false;
        [Tooltip("Scales the connected (parent) body's mass as seen by the joint. Values > 1 stiffen chains.")]
        [Min(0.01f)] public float connectedMassScale = 1f;

        [Header("Collisions")]
        [Tooltip("Ignore collisions between all colliders of the same ragdoll.")]
        public bool ignoreSelfCollisions = true;
    }

    /// <summary>Physical body description of an active ragdoll: bone masses, joint limits and muscle strengths.</summary>
    [CreateAssetMenu(menuName = "Wobblefolk/Ragdoll Profile", fileName = "RagdollProfile")]
    public sealed class RagdollProfile : ScriptableObject
    {
        public BodyPhysicsSettings physics = new BodyPhysicsSettings();

        [Tooltip("Global multiplier applied to every bone's muscle spring and damper.")]
        [Min(0f)] public float muscleStrength = 1f;

        public BoneSettings[] bones = CreateDefaultBones();

        [NonSerialized] BoneSettings[] lookup;

        public BoneSettings GetBone(BoneId id)
        {
            if (lookup == null || lookup.Length != BoneIds.Count)
            {
                RebuildLookup();
            }

            return lookup[(int)id];
        }

        void OnValidate()
        {
            lookup = null;
        }

        void RebuildLookup()
        {
            lookup = new BoneSettings[BoneIds.Count];
            if (bones != null)
            {
                foreach (BoneSettings settings in bones)
                {
                    if (settings != null)
                    {
                        lookup[(int)settings.bone] = settings;
                    }
                }
            }

            // Missing entries fall back to defaults so a partially filled profile still works.
            BoneSettings[] defaults = null;
            for (int i = 0; i < lookup.Length; i++)
            {
                if (lookup[i] == null)
                {
                    defaults ??= CreateDefaultBones();
                    lookup[i] = defaults[i];
                }
            }
        }

        [ContextMenu("Reset Bones To Defaults")]
        void ResetBonesToDefaults()
        {
            bones = CreateDefaultBones();
            lookup = null;
        }

        /// <summary>Default values for a ~65 kg, ~1.75 m humanoid. Indexed by <see cref="BoneId"/>.</summary>
        public static BoneSettings[] CreateDefaultBones()
        {
            var result = new BoneSettings[BoneIds.Count];

            result[(int)BoneId.Pelvis] = Bone(BoneId.Pelvis, 12f, 0f, 0f, 0f, 0f, 0f, 0f);
            result[(int)BoneId.Chest] = Bone(BoneId.Chest, 15f, -30f, 20f, 25f, 20f, 1800f, 90f);
            result[(int)BoneId.Head] = Bone(BoneId.Head, 5f, -40f, 40f, 50f, 30f, 400f, 25f);

            // Arms hang down; positive X rotation swings the arm backwards.
            for (int side = 0; side < 2; side++)
            {
                var s = (BodySide)side;
                result[(int)BoneIds.UpperArm(s)] = Bone(BoneIds.UpperArm(s), 2.5f, -175f, 60f, 90f, 90f, 300f, 20f);
                result[(int)BoneIds.LowerArm(s)] = Bone(BoneIds.LowerArm(s), 1.5f, -145f, 0f, 60f, 10f, 200f, 12f);
                result[(int)BoneIds.Hand(s)] = Bone(BoneIds.Hand(s), 0.5f, -60f, 60f, 30f, 30f, 50f, 3f);
                result[(int)BoneIds.UpperLeg(s)] = Bone(BoneIds.UpperLeg(s), 7f, -110f, 40f, 20f, 35f, 1500f, 70f);
                result[(int)BoneIds.LowerLeg(s)] = Bone(BoneIds.LowerLeg(s), 4f, 0f, 145f, 5f, 5f, 1200f, 60f);
                result[(int)BoneIds.Foot(s)] = Bone(BoneIds.Foot(s), 1f, -35f, 45f, 15f, 20f, 400f, 20f);
            }

            return result;
        }

        static BoneSettings Bone(BoneId id, float mass, float lowX, float highX, float y, float z, float spring, float damper)
        {
            return new BoneSettings
            {
                bone = id,
                mass = mass,
                lowAngularX = lowX,
                highAngularX = highX,
                angularY = y,
                angularZ = z,
                spring = spring,
                damper = damper,
            };
        }
    }
}
