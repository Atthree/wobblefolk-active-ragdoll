using UnityEngine;

namespace Wobblefolk.Ragdoll
{
    /// <summary>
    /// One physical bone of an active ragdoll. Wraps its Rigidbody and the ConfigurableJoint that connects it to its
    /// parent bone, and exposes muscle targets in joint, local or world space.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RagdollBone : MonoBehaviour
    {
        [SerializeField] BoneId id;
        [SerializeField] Rigidbody body;
        [SerializeField] ConfigurableJoint joint;
        [SerializeField] Collider mainCollider;

        Quaternion jointToLocal = Quaternion.identity;
        Quaternion localToJoint = Quaternion.identity;
        Quaternion startRelativeRotation = Quaternion.identity;
        BoneSettings settings;
        float appliedStrength = -1f;
        float globalStrength = 1f;
        float localStrength = 1f;

        public BoneId Id => id;
        public Rigidbody Body => body;
        public ConfigurableJoint Joint => joint;
        public Collider MainCollider => mainCollider;
        public bool HasJoint => joint != null;
        public ActiveRagdoll Owner { get; private set; }

        /// <summary>Rigidbody this bone's joint is connected to, or null for the root bone.</summary>
        public Rigidbody ParentBody => joint != null ? joint.connectedBody : null;

        internal void Configure(BoneId boneId, Rigidbody rigidbody, ConfigurableJoint configurableJoint, Collider collider)
        {
            id = boneId;
            body = rigidbody;
            joint = configurableJoint;
            mainCollider = collider;
        }

        internal void Initialize(ActiveRagdoll owner)
        {
            Owner = owner;
            if (body == null) body = GetComponent<Rigidbody>();
            if (joint == null) joint = GetComponent<ConfigurableJoint>();
            if (mainCollider == null) mainCollider = GetComponent<Collider>();

            if (joint != null)
            {
                jointToLocal = JointMath.JointToLocal(joint.axis, joint.secondaryAxis);
                localToJoint = Quaternion.Inverse(jointToLocal);
                startRelativeRotation = RelativeRotation();
            }
        }

        /// <summary>Applies mass, rigidbody, limit and muscle settings. Safe to call repeatedly at runtime.</summary>
        public void ApplySettings(BoneSettings boneSettings, BodyPhysicsSettings physics, float profileStrength)
        {
            settings = boneSettings;
            globalStrength = profileStrength;

            body.mass = boneSettings.mass;
            body.solverIterations = physics.solverIterations;
            body.solverVelocityIterations = physics.solverVelocityIterations;
            body.maxAngularVelocity = physics.maxAngularVelocity;
            body.maxDepenetrationVelocity = physics.maxDepenetrationVelocity;
            body.linearDamping = physics.linearDamping;
            body.angularDamping = physics.angularDamping;
            body.interpolation = physics.interpolation;
            body.collisionDetectionMode = physics.collisionDetection;

            if (joint == null)
            {
                return;
            }

            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;

            float low = Mathf.Min(boneSettings.lowAngularX, boneSettings.highAngularX);
            float high = Mathf.Max(boneSettings.lowAngularX, boneSettings.highAngularX);
            joint.angularXMotion = Mathf.Approximately(low, high) ? ConfigurableJointMotion.Locked : ConfigurableJointMotion.Limited;
            joint.angularYMotion = boneSettings.angularY <= 0f ? ConfigurableJointMotion.Locked : ConfigurableJointMotion.Limited;
            joint.angularZMotion = boneSettings.angularZ <= 0f ? ConfigurableJointMotion.Locked : ConfigurableJointMotion.Limited;
            joint.lowAngularXLimit = new SoftJointLimit { limit = low };
            joint.highAngularXLimit = new SoftJointLimit { limit = high };
            joint.angularYLimit = new SoftJointLimit { limit = boneSettings.angularY };
            joint.angularZLimit = new SoftJointLimit { limit = boneSettings.angularZ };

            joint.rotationDriveMode = RotationDriveMode.Slerp;
            joint.projectionMode = physics.useProjection ? JointProjectionMode.PositionAndRotation : JointProjectionMode.None;
            joint.projectionDistance = physics.projectionDistance;
            joint.projectionAngle = physics.projectionAngle;
            joint.enablePreprocessing = physics.enablePreprocessing;
            joint.connectedMassScale = physics.connectedMassScale;

            appliedStrength = -1f;
            SetMuscleStrength(1f);
        }

        /// <summary>Scales this bone's muscle spring and damper relative to its profile values.</summary>
        public void SetMuscleStrength(float strength)
        {
            if (Mathf.Approximately(strength, appliedStrength))
            {
                return;
            }

            appliedStrength = strength;
            ApplyDrive();
        }

        /// <summary>
        /// Extra multiplier for this bone only, on top of <see cref="SetMuscleStrength"/>, e.g. stiffer arms while reaching.
        /// </summary>
        public void SetLocalMuscleStrength(float multiplier)
        {
            if (Mathf.Approximately(multiplier, localStrength))
            {
                return;
            }

            localStrength = multiplier;
            ApplyDrive();
        }

        void ApplyDrive()
        {
            if (joint == null || settings == null)
            {
                return;
            }

            float scale = appliedStrength * globalStrength * localStrength;
            joint.slerpDrive = new JointDrive
            {
                positionSpring = settings.spring * scale,
                positionDamper = settings.damper * scale,
                maximumForce = settings.maxForce,
            };
        }

        /// <summary>Target rotation relative to the rest pose, expressed in joint space (x = joint axis).</summary>
        public void SetTargetJointRotation(Quaternion jointSpaceRotation)
        {
            if (joint != null)
            {
                joint.targetRotation = Quaternion.Inverse(jointSpaceRotation);
            }
        }

        /// <summary>Target rotation relative to the rest pose, expressed in this bone's local space.</summary>
        public void SetTargetLocalRotation(Quaternion localRotation)
        {
            SetTargetJointRotation(localToJoint * localRotation * jointToLocal);
        }

        /// <summary>Target world rotation of this bone, assuming its parent keeps its current orientation.</summary>
        public void SetTargetWorldRotation(Quaternion worldRotation)
        {
            Rigidbody parent = ParentBody;
            if (parent == null)
            {
                return;
            }

            Quaternion targetRelative = Quaternion.Inverse(parent.rotation) * worldRotation;
            SetTargetLocalRotation(Quaternion.Inverse(startRelativeRotation) * targetRelative);
        }

        public void ResetTarget()
        {
            if (joint != null)
            {
                joint.targetRotation = Quaternion.identity;
            }
        }

        Quaternion RelativeRotation()
        {
            Rigidbody parent = ParentBody;
            Quaternion parentRotation = parent != null ? parent.transform.rotation : Quaternion.identity;
            return Quaternion.Inverse(parentRotation) * transform.rotation;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (Owner != null)
            {
                Owner.ReportCollision(this, collision);
            }
        }
    }
}
