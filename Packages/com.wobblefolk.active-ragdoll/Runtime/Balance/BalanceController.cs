using UnityEngine;

namespace Wobblefolk.Ragdoll
{
    /// <summary>
    /// Keeps the ragdoll upright with PD-controlled torques on the pelvis and chest, holds the pelvis at stand height
    /// above the detected ground, and runs the Normal / Fallen / GettingUp / Unconscious state transitions.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActiveRagdoll))]
    public sealed class BalanceController : MonoBehaviour
    {
        [SerializeField] ActiveRagdoll ragdoll;
        [SerializeField] BalanceProfile profile;

        GroundProbe probe;
        Vector3 pelvisUpLocal;
        Vector3 pelvisForwardLocal;
        Vector3 chestUpLocal;
        float tiltTimer;
        float knockoutDuration;
        float getUpStartHeight;

        public BalanceProfile Profile => profile;
        public GroundInfo Ground { get; private set; }

        /// <summary>World-space horizontal direction the pelvis turns towards.</summary>
        public Vector3 DesiredFacing { get; set; }

        /// <summary>0..1 weight of the balance forces. Other systems may lower it, e.g. while climbing.</summary>
        public float BalanceWeight { get; set; } = 1f;

        /// <summary>When true the stand-height lift is suspended, e.g. during the rising part of a jump.</summary>
        public bool SuspendLift { get; set; }

        /// <summary>World direction the pelvis and chest are held towards. Tilt it away from <see cref="Up"/> to lean.</summary>
        public Vector3 TargetUp { get; set; } = Vector3.up;

        /// <summary>Added to the profile stand height, e.g. while stepping up stairs.</summary>
        public float HeightOffset { get; set; }

        /// <summary>Stand height currently targeted in the Normal state.</summary>
        public float StandHeight => profile.height.standHeight + HeightOffset;

        /// <summary>Current tilt of the pelvis away from the up axis in degrees.</summary>
        public float Tilt { get; private set; }

        public static Vector3 Up => Physics.gravity.sqrMagnitude > 0f ? -Physics.gravity.normalized : Vector3.up;

        internal void Configure(ActiveRagdoll owner, BalanceProfile balanceProfile)
        {
            ragdoll = owner;
            profile = balanceProfile;
        }

        void Reset()
        {
            ragdoll = GetComponent<ActiveRagdoll>();
        }

        void Awake()
        {
            if (ragdoll == null) ragdoll = GetComponent<ActiveRagdoll>();
            probe = new GroundProbe(ragdoll);

            Vector3 up = Up;
            Transform pelvis = ragdoll.Pelvis.transform;
            Vector3 facing = Vector3.ProjectOnPlane(transform.forward, up).normalized;
            pelvisUpLocal = pelvis.InverseTransformDirection(up);
            pelvisForwardLocal = pelvis.InverseTransformDirection(facing);
            if (ragdoll.Chest != null) chestUpLocal = ragdoll.Chest.transform.InverseTransformDirection(up);
            DesiredFacing = facing;
            TargetUp = up;
        }

        void OnEnable()
        {
            if (ragdoll != null)
            {
                ragdoll.BoneCollided += OnBoneCollided;
                ragdoll.StateChanged += OnStateChanged;
            }
        }

        void OnDisable()
        {
            if (ragdoll != null)
            {
                ragdoll.BoneCollided -= OnBoneCollided;
                ragdoll.StateChanged -= OnStateChanged;
            }
        }

        /// <summary>Knocks the character down; it gets up again on its own.</summary>
        public void Fall()
        {
            if (ragdoll.State == RagdollState.Normal || ragdoll.State == RagdollState.GettingUp)
            {
                ragdoll.SetState(RagdollState.Fallen);
            }
        }

        /// <summary>Knocks the character out for the given duration (seconds), or the profile default when negative.</summary>
        public void Knockout(float duration = -1f)
        {
            knockoutDuration = duration >= 0f ? duration : profile.knockout.defaultDuration;
            ragdoll.SetState(RagdollState.Unconscious);
        }

        void FixedUpdate()
        {
            if (profile == null || ragdoll.Pelvis == null)
            {
                return;
            }

            Vector3 up = Up;
            Ground = probe.Probe(profile.ground, profile.height.standHeight, up);
            Rigidbody pelvis = ragdoll.Pelvis.Body;
            Tilt = Vector3.Angle(pelvis.rotation * pelvisUpLocal, up);
            float stateTime = ragdoll.StateTime;

            switch (ragdoll.State)
            {
                case RagdollState.Normal:
                    ApplyBalance(up, BalanceWeight, StandHeight);
                    UpdateFallDetection();
                    break;

                case RagdollState.Fallen:
                    bool settled = (pelvis.linearVelocity - GroundVelocity()).magnitude <= profile.getUp.maxStartSpeed;
                    if (stateTime >= profile.getUp.delay && Ground.isGrounded && settled)
                    {
                        ragdoll.SetState(RagdollState.GettingUp);
                    }
                    break;

                case RagdollState.GettingUp:
                    UpdateGettingUp(up, stateTime);
                    break;

                case RagdollState.Unconscious:
                    if (stateTime >= knockoutDuration)
                    {
                        ragdoll.SetState(RagdollState.Fallen);
                    }
                    break;
            }
        }

        void UpdateFallDetection()
        {
            if (Tilt > profile.fall.fallTiltAngle)
            {
                tiltTimer += Time.fixedDeltaTime;
                if (tiltTimer >= profile.fall.fallTiltTime)
                {
                    ragdoll.SetState(RagdollState.Fallen);
                }
            }
            else
            {
                tiltTimer = 0f;
            }
        }

        void UpdateGettingUp(Vector3 up, float stateTime)
        {
            GetUpSettings getUp = profile.getUp;
            float t = Mathf.Clamp01(stateTime / getUp.duration);
            float weight = Mathf.Clamp01(getUp.strengthCurve.Evaluate(t));

            ragdoll.SetMuscleStrength(Mathf.Lerp(profile.fall.fallenMuscleStrength, 1f, weight));
            float height = Mathf.Lerp(getUpStartHeight, profile.height.standHeight, weight);
            ApplyBalance(up, weight * BalanceWeight, height);

            if (t >= 1f && Tilt <= getUp.completeTiltAngle)
            {
                ragdoll.SetState(RagdollState.Normal);
            }
            else if (stateTime >= getUp.timeout)
            {
                ragdoll.SetState(RagdollState.Fallen);
            }
        }

        void ApplyBalance(Vector3 up, float weight, float targetHeight)
        {
            if (weight <= 0f)
            {
                return;
            }

            UprightSettings upright = profile.upright;
            Rigidbody pelvis = ragdoll.Pelvis.Body;

            Vector3 targetUp = ragdoll.State == RagdollState.Normal ? TargetUp.normalized : up;
            Vector3 torque = UprightTorque(pelvis, pelvisUpLocal, targetUp, up, upright) + YawTorque(pelvis, up, upright);
            pelvis.AddTorque(JointMath.ClampMagnitude(torque, upright.maxTorque) * weight, ForceMode.Force);

            RagdollBone chest = ragdoll.Chest;
            if (chest != null && upright.chestWeight > 0f)
            {
                Vector3 chestTorque = UprightTorque(chest.Body, chestUpLocal, targetUp, up, upright) * upright.chestWeight;
                chest.Body.AddTorque(JointMath.ClampMagnitude(chestTorque, upright.maxTorque) * weight, ForceMode.Force);
            }

            ApplyLift(pelvis, up, weight, targetHeight);
        }

        static Vector3 UprightTorque(Rigidbody body, Vector3 upLocal, Vector3 targetUp, Vector3 up, UprightSettings upright)
        {
            Vector3 currentUp = body.rotation * upLocal;
            Quaternion error = Quaternion.FromToRotation(currentUp, targetUp);
            JointMath.ToAngleAxisRadians(error, out float angle, out Vector3 axis);

            Vector3 tiltVelocity = Vector3.ProjectOnPlane(body.angularVelocity, up);
            return axis * (angle * upright.tiltSpring) - tiltVelocity * upright.tiltDamper;
        }

        Vector3 YawTorque(Rigidbody body, Vector3 up, UprightSettings upright)
        {
            Vector3 facing = Vector3.ProjectOnPlane(DesiredFacing, up);
            Vector3 current = Vector3.ProjectOnPlane(body.rotation * pelvisForwardLocal, up);
            if (facing.sqrMagnitude < 1e-6f || current.sqrMagnitude < 1e-6f)
            {
                return Vector3.zero;
            }

            float angle = Vector3.SignedAngle(current, facing, up) * Mathf.Deg2Rad;
            float yawVelocity = Vector3.Dot(body.angularVelocity, up);
            return up * (angle * upright.yawSpring - yawVelocity * upright.yawDamper);
        }

        void ApplyLift(Rigidbody pelvis, Vector3 up, float weight, float targetHeight)
        {
            GroundInfo ground = Ground;
            if (SuspendLift || !ground.isGrounded)
            {
                return;
            }

            HeightSettings height = profile.height;
            float error = targetHeight - ground.pelvisHeight;
            float verticalVelocity = Vector3.Dot(pelvis.linearVelocity - GroundVelocity(), up);
            float acceleration = Physics.gravity.magnitude * height.gravityCompensation
                                 + error * height.spring
                                 - verticalVelocity * height.damper;
            acceleration = Mathf.Clamp(acceleration, 0f, height.maxLiftAcceleration);
            pelvis.AddForce(up * (acceleration * ragdoll.TotalMass * weight), ForceMode.Force);
        }

        Vector3 GroundVelocity()
        {
            Rigidbody body = Ground.Body;
            return body != null ? body.GetPointVelocity(Ground.point) : Vector3.zero;
        }

        void OnStateChanged(RagdollState previous, RagdollState current)
        {
            tiltTimer = 0f;
            switch (current)
            {
                case RagdollState.Normal:
                    ragdoll.SetMuscleStrength(1f);
                    break;
                case RagdollState.Fallen:
                    ragdoll.SetMuscleStrength(profile.fall.fallenMuscleStrength);
                    break;
                case RagdollState.GettingUp:
                    getUpStartHeight = Ground.isGrounded ? Ground.pelvisHeight : 0f;
                    ragdoll.SetMuscleStrength(profile.fall.fallenMuscleStrength);
                    break;
                case RagdollState.Unconscious:
                    ragdoll.SetMuscleStrength(profile.knockout.muscleStrength);
                    break;
            }
        }

        void OnBoneCollided(RagdollBone bone, Collision collision)
        {
            if (profile == null)
            {
                return;
            }

            FallSettings fall = profile.fall;
            if (fall.ignoreFootImpacts && (bone.Id == BoneId.FootLeft || bone.Id == BoneId.FootRight))
            {
                return;
            }

            // Only the approach speed along the contact normal counts; sliding and grazing contacts are not impacts.
            float speed = collision.contactCount > 0
                ? Mathf.Abs(Vector3.Dot(collision.relativeVelocity, collision.GetContact(0).normal))
                : collision.relativeVelocity.magnitude;

            if (bone.Id == BoneId.Head && fall.knockoutImpactSpeed > 0f && speed >= fall.knockoutImpactSpeed
                && ragdoll.State != RagdollState.Unconscious)
            {
                Knockout();
            }
            else if (fall.knockdownImpactSpeed > 0f && speed >= fall.knockdownImpactSpeed)
            {
                Fall();
            }
        }

        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || ragdoll == null || ragdoll.Pelvis == null)
            {
                return;
            }

            GroundInfo ground = Ground;
            Vector3 pelvis = ragdoll.Pelvis.Body.position;
            Gizmos.color = ground.isGrounded ? Color.green : Color.red;
            Gizmos.DrawLine(pelvis, ground.isGrounded ? ground.point : pelvis - Up * profile.height.standHeight);
            if (ground.isGrounded)
            {
                Gizmos.DrawWireSphere(ground.point, 0.05f);
            }

            Gizmos.color = Color.blue;
            Gizmos.DrawRay(pelvis, DesiredFacing * 0.5f);
        }
    }
}
