using System.Collections.Generic;
using UnityEngine;

namespace Wobblefolk.Ragdoll
{
    /// <summary>
    /// Builds a complete active ragdoll out of primitive shapes. Each bone's pivot sits on its joint; colliders and
    /// visuals are offset from it. The root is placed at foot level, facing its forward axis.
    /// </summary>
    public static class PrimitiveRagdollBuilder
    {
        enum Shape { Box, Capsule, Sphere }

        public static ActiveRagdoll Build(PrimitiveBodyShape shape, RagdollProfile ragdollProfile, BalanceProfile balanceProfile,
            Vector3 position, Quaternion rotation, Material material = null, string name = "Active Ragdoll")
        {
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(position, rotation);
            Transform r = root.transform;
            var created = new Dictionary<BoneId, RagdollBone>();

            float s = shape.shoulderSpacing * 0.5f;
            float h = shape.hipSpacing * 0.5f;

            RagdollBone pelvis = CreateBone(created, BoneId.Pelvis, r, r, new Vector3(0f, shape.PelvisHeight, 0f),
                Shape.Box, shape.pelvisSize, Vector3.zero, material);
            RagdollBone chest = CreateBone(created, BoneId.Chest, pelvis.transform, r, new Vector3(0f, shape.WaistHeight, 0f),
                Shape.Box, shape.chestSize, Vector3.up * (shape.chestSize.y * 0.5f), material);
            CreateBone(created, BoneId.Head, chest.transform, r, new Vector3(0f, shape.NeckHeight, 0f),
                Shape.Sphere, Vector3.one * (shape.headRadius * 2f), Vector3.up * (shape.neckLength + shape.headRadius), material);

            for (int i = 0; i < 2; i++)
            {
                var side = (BodySide)i;
                float x = side == BodySide.Left ? -1f : 1f;

                RagdollBone upperArm = CreateLimb(created, BoneIds.UpperArm(side), chest.transform, r,
                    new Vector3(x * s, shape.ShoulderHeight, 0f), shape.upperArmLength, shape.upperArmRadius, material);
                RagdollBone lowerArm = CreateLimb(created, BoneIds.LowerArm(side), upperArm.transform, r,
                    new Vector3(x * s, shape.ShoulderHeight - shape.upperArmLength, 0f), shape.lowerArmLength, shape.lowerArmRadius, material);
                CreateBone(created, BoneIds.Hand(side), lowerArm.transform, r,
                    new Vector3(x * s, shape.ShoulderHeight - shape.upperArmLength - shape.lowerArmLength, 0f),
                    Shape.Sphere, Vector3.one * (shape.handRadius * 2f), Vector3.down * shape.handRadius, material);

                RagdollBone upperLeg = CreateLimb(created, BoneIds.UpperLeg(side), pelvis.transform, r,
                    new Vector3(x * h, shape.HipHeight, 0f), shape.upperLegLength, shape.upperLegRadius, material);
                RagdollBone lowerLeg = CreateLimb(created, BoneIds.LowerLeg(side), upperLeg.transform, r,
                    new Vector3(x * h, shape.KneeHeight, 0f), shape.lowerLegLength, shape.lowerLegRadius, material);
                CreateBone(created, BoneIds.Foot(side), lowerLeg.transform, r,
                    new Vector3(x * h, shape.AnkleHeight, 0f), Shape.Box, shape.footSize,
                    new Vector3(0f, -shape.footSize.y * 0.5f, shape.footForwardOffset), material);
            }

            // Joints are created only after every transform is in its rest pose, because a joint captures the
            // relative rotation between its bodies at creation time.
            var bones = new RagdollBone[created.Count];
            int index = 0;
            foreach (KeyValuePair<BoneId, RagdollBone> pair in created)
            {
                RagdollBone bone = pair.Value;
                ConfigurableJoint joint = null;
                if (pair.Key != BoneId.Pelvis)
                {
                    BoneSettings settings = ragdollProfile.GetBone(pair.Key);
                    joint = bone.gameObject.AddComponent<ConfigurableJoint>();
                    joint.autoConfigureConnectedAnchor = true;
                    joint.anchor = Vector3.zero;
                    joint.axis = settings.axis;
                    joint.secondaryAxis = settings.secondaryAxis;
                    joint.connectedBody = created[BoneIds.Parent(pair.Key)].Body;
                }

                bone.Configure(pair.Key, bone.Body, joint, bone.GetComponent<Collider>());
                bones[index++] = bone;
            }

            var ragdoll = root.AddComponent<ActiveRagdoll>();
            ragdoll.Configure(ragdollProfile, bones);
            var balance = root.AddComponent<BalanceController>();
            balance.Configure(ragdoll, balanceProfile);
            return ragdoll;
        }

        static RagdollBone CreateLimb(Dictionary<BoneId, RagdollBone> created, BoneId id, Transform parent, Transform root,
            Vector3 localJointPosition, float length, float radius, Material material)
        {
            return CreateBone(created, id, parent, root, localJointPosition, Shape.Capsule,
                new Vector3(radius * 2f, length, radius * 2f), Vector3.down * (length * 0.5f), material);
        }

        static RagdollBone CreateBone(Dictionary<BoneId, RagdollBone> created, BoneId id, Transform parent, Transform root,
            Vector3 rootLocalJointPosition, Shape shape, Vector3 size, Vector3 center, Material material)
        {
            var go = new GameObject(id.ToString());
            go.transform.SetParent(parent, false);
            go.transform.position = root.TransformPoint(rootLocalJointPosition);
            go.transform.rotation = root.rotation;

            switch (shape)
            {
                case Shape.Box:
                    var box = go.AddComponent<BoxCollider>();
                    box.size = size;
                    box.center = center;
                    break;
                case Shape.Capsule:
                    var capsule = go.AddComponent<CapsuleCollider>();
                    capsule.direction = 1;
                    capsule.radius = size.x * 0.5f;
                    capsule.height = size.y;
                    capsule.center = center;
                    break;
                case Shape.Sphere:
                    var sphere = go.AddComponent<SphereCollider>();
                    sphere.radius = size.x * 0.5f;
                    sphere.center = center;
                    break;
            }

            CreateVisual(go.transform, shape, size, center, material);

            go.AddComponent<Rigidbody>();
            var bone = go.AddComponent<RagdollBone>();
            bone.Configure(id, go.GetComponent<Rigidbody>(), null, go.GetComponent<Collider>());
            created.Add(id, bone);
            return bone;
        }

        static void CreateVisual(Transform bone, Shape shape, Vector3 size, Vector3 center, Material material)
        {
            PrimitiveType type = shape == Shape.Box ? PrimitiveType.Cube
                : shape == Shape.Capsule ? PrimitiveType.Capsule
                : PrimitiveType.Sphere;

            GameObject visual = GameObject.CreatePrimitive(type);
            visual.name = "Visual";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(bone, false);
            visual.transform.localPosition = center;
            // Unity's capsule primitive is 2 units tall and 1 unit wide.
            visual.transform.localScale = shape == Shape.Capsule ? new Vector3(size.x, size.y * 0.5f, size.z) : size;

            if (material != null)
            {
                visual.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
        }
    }
}
