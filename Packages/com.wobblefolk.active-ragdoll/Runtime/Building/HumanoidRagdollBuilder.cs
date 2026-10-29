using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wobblefolk.Ragdoll
{
    /// <summary>Collider sizing of the humanoid builder, as ratios of measured bone lengths.</summary>
    [Serializable]
    public sealed class HumanoidRagdollSettings
    {
        [Tooltip("Arm capsule radius as a fraction of the bone length.")]
        [Range(0.05f, 0.5f)] public float armRadiusRatio = 0.18f;
        [Tooltip("Leg capsule radius as a fraction of the bone length.")]
        [Range(0.05f, 0.5f)] public float legRadiusRatio = 0.2f;
        [Tooltip("Hand length as a fraction of the lower arm length.")]
        [Range(0.1f, 1f)] public float handLengthRatio = 0.45f;
        [Tooltip("Foot length as a fraction of the lower leg length, used when the rig has no toe bones.")]
        [Range(0.1f, 1f)] public float footLengthRatio = 0.55f;
        [Tooltip("Head radius as a fraction of the character height.")]
        [Range(0.02f, 0.2f)] public float headRadiusRatio = 0.065f;
        [Tooltip("Chest width as a fraction of the shoulder spacing.")]
        [Range(0.3f, 1.5f)] public float chestWidthRatio = 0.8f;
        [Tooltip("Chest depth as a fraction of its width.")]
        [Range(0.2f, 1f)] public float chestDepthRatio = 0.55f;
        [Tooltip("Pelvis depth as a fraction of its width.")]
        [Range(0.2f, 1f)] public float pelvisDepthRatio = 0.6f;
        [Tooltip("Rotate the upper arms down by this angle below horizontal before building, so a T-pose model " +
                 "gets the relaxed rest pose the profiles are tuned for. 0 = keep the model's pose.")]
        [Range(0f, 90f)] public float armsDownAngle = 75f;
    }

    /// <summary>
    /// Turns a model with a Humanoid avatar into an active ragdoll: maps the avatar bones, adds rigidbodies, sized
    /// colliders and configurable joints, then the behaviour controllers. Joint axes are derived from the character's
    /// own right / up directions, so the same profiles work regardless of how the rig's bone axes are oriented.
    /// </summary>
    public static class HumanoidRagdollBuilder
    {
        /// <summary>Name of the child objects that hold the generated colliders.</summary>
        public const string ColliderObjectName = "RagdollCollider";

        static readonly (BoneId id, HumanBodyBones bone)[] Map =
        {
            (BoneId.Pelvis, HumanBodyBones.Hips),
            (BoneId.Head, HumanBodyBones.Head),
            (BoneId.UpperArmLeft, HumanBodyBones.LeftUpperArm),
            (BoneId.LowerArmLeft, HumanBodyBones.LeftLowerArm),
            (BoneId.HandLeft, HumanBodyBones.LeftHand),
            (BoneId.UpperArmRight, HumanBodyBones.RightUpperArm),
            (BoneId.LowerArmRight, HumanBodyBones.RightLowerArm),
            (BoneId.HandRight, HumanBodyBones.RightHand),
            (BoneId.UpperLegLeft, HumanBodyBones.LeftUpperLeg),
            (BoneId.LowerLegLeft, HumanBodyBones.LeftLowerLeg),
            (BoneId.FootLeft, HumanBodyBones.LeftFoot),
            (BoneId.UpperLegRight, HumanBodyBones.RightUpperLeg),
            (BoneId.LowerLegRight, HumanBodyBones.RightLowerLeg),
            (BoneId.FootRight, HumanBodyBones.RightFoot),
        };

        /// <summary>Returns an error message, or null when the animator can be turned into an active ragdoll.</summary>
        public static string Validate(Animator animator)
        {
            if (animator == null) return "Assign a model with an Animator.";
            if (animator.avatar == null || !animator.avatar.isHuman) return "The Animator needs a Humanoid avatar (Rig > Animation Type: Humanoid).";
            if (animator.GetComponent<ActiveRagdoll>() != null) return "This model already has an Active Ragdoll. Remove it first.";
            if (FindChest(animator) == null) return "The avatar has no Chest or Spine bone.";

            foreach ((BoneId id, HumanBodyBones bone) in Map)
            {
                if (animator.GetBoneTransform(bone) == null) return $"The avatar has no {bone} bone (needed for {id}).";
            }

            return null;
        }

        /// <summary>Pelvis height above the model root, i.e. the stand height the balance profile should use.</summary>
        public static float MeasureStandHeight(Animator animator)
        {
            Transform root = animator.transform;
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            return Vector3.Dot(hips.position - root.position, root.up);
        }

        public static ActiveRagdoll Build(Animator animator, RagdollProfileSet profiles, HumanoidRagdollSettings settings)
        {
            string error = Validate(animator);
            if (error != null) throw new InvalidOperationException(error);

            Transform root = animator.transform;
            Vector3 right = root.right;
            Vector3 up = root.up;
            Vector3 forward = root.forward;

            Dictionary<BoneId, Transform> bones = CollectBones(animator);
            if (settings.armsDownAngle > 0f)
            {
                LowerArms(bones, settings.armsDownAngle, right, up);
            }

            // Measurements in the (possibly re-posed) rest pose.
            float height = Vector3.Dot(bones[BoneId.Head].position - root.position, up);
            float hipSpacing = Vector3.Distance(bones[BoneId.UpperLegLeft].position, bones[BoneId.UpperLegRight].position);
            float shoulderSpacing = Vector3.Distance(bones[BoneId.UpperArmLeft].position, bones[BoneId.UpperArmRight].position);
            float lowerArmLength = Vector3.Distance(bones[BoneId.LowerArmLeft].position, bones[BoneId.HandLeft].position);
            float lowerLegLength = Vector3.Distance(bones[BoneId.LowerLegLeft].position, bones[BoneId.FootLeft].position);
            float headRadius = height * settings.headRadiusRatio;

            var created = new Dictionary<BoneId, RagdollBone>();
            foreach (KeyValuePair<BoneId, Transform> pair in bones)
            {
                Transform bone = pair.Value;
                Collider collider = CreateCollider(pair.Key, bone, bones, animator, settings, root, up, forward,
                    hipSpacing, shoulderSpacing, lowerArmLength, lowerLegLength, headRadius);

                var body = bone.gameObject.AddComponent<Rigidbody>();
                var ragdollBone = bone.gameObject.AddComponent<RagdollBone>();
                ragdollBone.Configure(pair.Key, body, null, collider);
                created.Add(pair.Key, ragdollBone);
            }

            // Joints are added only once every body exists and the rest pose is final.
            var result = new RagdollBone[created.Count];
            int index = 0;
            foreach (KeyValuePair<BoneId, RagdollBone> pair in created)
            {
                RagdollBone bone = pair.Value;
                ConfigurableJoint joint = null;
                if (pair.Key != BoneId.Pelvis)
                {
                    Transform t = bone.transform;
                    joint = bone.gameObject.AddComponent<ConfigurableJoint>();
                    joint.autoConfigureConnectedAnchor = true;
                    joint.anchor = Vector3.zero;
                    // Same convention as the primitive builder: X = character right, Y = character up.
                    joint.axis = t.InverseTransformDirection(right);
                    joint.secondaryAxis = t.InverseTransformDirection(up);
                    joint.connectedBody = created[BoneIds.Parent(pair.Key)].Body;
                }

                bone.Configure(pair.Key, bone.Body, joint, bone.MainCollider);
                result[index++] = bone;
            }

            // Animation would overwrite the simulated pose; the Animator stays only for its avatar.
            animator.enabled = false;

            var ragdoll = root.gameObject.AddComponent<ActiveRagdoll>();
            ragdoll.Configure(profiles.ragdoll, result);
            RagdollSetup.AddControllers(ragdoll, profiles);
            return ragdoll;
        }

        static Dictionary<BoneId, Transform> CollectBones(Animator animator)
        {
            var bones = new Dictionary<BoneId, Transform>();
            foreach ((BoneId id, HumanBodyBones bone) in Map)
            {
                bones[id] = animator.GetBoneTransform(bone);
            }

            bones[BoneId.Chest] = FindChest(animator);
            return bones;
        }

        /// <summary>The chest body covers the rib cage: Chest when present, else Spine. Upper chest follows it.</summary>
        static Transform FindChest(Animator animator)
        {
            Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            return chest != null ? chest : animator.GetBoneTransform(HumanBodyBones.Spine);
        }

        static void LowerArms(Dictionary<BoneId, Transform> bones, float angle, Vector3 right, Vector3 up)
        {
            for (int i = 0; i < 2; i++)
            {
                var side = (BodySide)i;
                Transform upper = bones[BoneIds.UpperArm(side)];
                Transform lower = bones[BoneIds.LowerArm(side)];
                Vector3 outward = side == BodySide.Left ? -right : right;
                Vector3 current = (lower.position - upper.position).normalized;
                // Tilt from horizontal-outward towards straight down; below 90° the arms keep clear of the torso.
                float radians = angle * Mathf.Deg2Rad;
                Vector3 target = outward * Mathf.Cos(radians) - up * Mathf.Sin(radians);
                upper.rotation = Quaternion.FromToRotation(current, target) * upper.rotation;
            }
        }

        static Collider CreateCollider(BoneId id, Transform bone, Dictionary<BoneId, Transform> bones, Animator animator,
            HumanoidRagdollSettings settings, Transform root, Vector3 up, Vector3 forward, float hipSpacing,
            float shoulderSpacing, float lowerArmLength, float lowerLegLength, float headRadius)
        {
            switch (id)
            {
                case BoneId.Pelvis:
                {
                    Vector3 legs = (bones[BoneId.UpperLegLeft].position + bones[BoneId.UpperLegRight].position) * 0.5f;
                    float top = Vector3.Dot(bones[BoneId.Chest].position - legs, up);
                    float width = hipSpacing * 1.5f;
                    Vector3 size = new Vector3(width, Mathf.Max(top, hipSpacing * 0.5f), width * settings.pelvisDepthRatio);
                    return Box(bone, legs + up * (size.y * 0.5f), root.rotation, size);
                }

                case BoneId.Chest:
                {
                    Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
                    Vector3 topPoint = neck != null ? neck.position : bones[BoneId.Head].position;
                    float h = Vector3.Dot(topPoint - bone.position, up);
                    float width = shoulderSpacing * settings.chestWidthRatio;
                    Vector3 size = new Vector3(width, Mathf.Max(h, 0.05f), width * settings.chestDepthRatio);
                    return Box(bone, bone.position + up * (size.y * 0.5f), root.rotation, size);
                }

                case BoneId.Head:
                {
                    var go = ColliderObject(bone, bone.position + up * headRadius, root.rotation);
                    var sphere = go.AddComponent<SphereCollider>();
                    sphere.radius = headRadius / Mathf.Max(go.transform.lossyScale.x, 1e-4f);
                    return sphere;
                }

                case BoneId.HandLeft:
                case BoneId.HandRight:
                {
                    BodySide side = id == BoneId.HandLeft ? BodySide.Left : BodySide.Right;
                    Vector3 direction = (bone.position - bones[BoneIds.LowerArm(side)].position).normalized;
                    float length = lowerArmLength * settings.handLengthRatio;
                    return Capsule(bone, bone.position, bone.position + direction * length, length * 0.35f);
                }

                case BoneId.FootLeft:
                case BoneId.FootRight:
                {
                    Transform toes = animator.GetBoneTransform(id == BoneId.FootLeft ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes);
                    float ankleHeight = Mathf.Max(Vector3.Dot(bone.position - root.position, up), 0.03f);
                    float length = toes != null
                        ? Vector3.Dot(toes.position - bone.position, forward) * 1.6f
                        : lowerLegLength * settings.footLengthRatio;
                    length = Mathf.Max(length, lowerLegLength * 0.3f);
                    Vector3 size = new Vector3(length * 0.4f, ankleHeight, length);
                    Vector3 center = bone.position - up * (ankleHeight * 0.5f) + forward * (length * 0.3f);
                    return Box(bone, center, root.rotation, size);
                }

                default:
                {
                    // Limbs: a capsule from this bone to the next one in the chain.
                    Transform end = bones[ChildOf(id)];
                    bool arm = id == BoneId.UpperArmLeft || id == BoneId.UpperArmRight
                               || id == BoneId.LowerArmLeft || id == BoneId.LowerArmRight;
                    float length = Vector3.Distance(bone.position, end.position);
                    float radius = length * (arm ? settings.armRadiusRatio : settings.legRadiusRatio);
                    return Capsule(bone, bone.position, end.position, radius);
                }
            }
        }

        static BoneId ChildOf(BoneId id)
        {
            switch (id)
            {
                case BoneId.UpperArmLeft: return BoneId.LowerArmLeft;
                case BoneId.LowerArmLeft: return BoneId.HandLeft;
                case BoneId.UpperArmRight: return BoneId.LowerArmRight;
                case BoneId.LowerArmRight: return BoneId.HandRight;
                case BoneId.UpperLegLeft: return BoneId.LowerLegLeft;
                case BoneId.LowerLegLeft: return BoneId.FootLeft;
                case BoneId.UpperLegRight: return BoneId.LowerLegRight;
                case BoneId.LowerLegRight: return BoneId.FootRight;
                default: throw new ArgumentOutOfRangeException(nameof(id), id, "Not a limb bone.");
            }
        }

        /// <summary>
        /// Colliders live on child objects aligned with the body part, so they fit regardless of the bone's axes.
        /// The child's world scale is cancelled so sizes are in metres.
        /// </summary>
        static GameObject ColliderObject(Transform bone, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject(ColliderObjectName);
            go.transform.SetParent(bone, false);
            go.transform.SetPositionAndRotation(position, rotation);
            Vector3 parentScale = bone.lossyScale;
            go.transform.localScale = new Vector3(1f / SafeScale(parentScale.x), 1f / SafeScale(parentScale.y), 1f / SafeScale(parentScale.z));
            return go;
        }

        static float SafeScale(float value) => Mathf.Abs(value) < 1e-4f ? 1f : value;

        static Collider Box(Transform bone, Vector3 center, Quaternion rotation, Vector3 size)
        {
            var box = ColliderObject(bone, center, rotation).AddComponent<BoxCollider>();
            box.size = size;
            return box;
        }

        static Collider Capsule(Transform bone, Vector3 start, Vector3 end, float radius)
        {
            Vector3 axis = end - start;
            float length = axis.magnitude;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, length > 1e-5f ? axis / length : Vector3.up);
            var capsule = ColliderObject(bone, (start + end) * 0.5f, rotation).AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.radius = radius;
            capsule.height = length + radius * 2f;
            return capsule;
        }
    }
}
