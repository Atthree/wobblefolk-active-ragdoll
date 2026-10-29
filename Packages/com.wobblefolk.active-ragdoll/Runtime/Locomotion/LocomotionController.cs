using UnityEngine;

namespace Wobblefolk.Ragdoll
{
    /// <summary>
    /// Animation-free locomotion. Movement comes from capped horizontal forces on the torso; the legs are driven through
    /// a procedural gait cycle on their joint targets, so they visibly step and push against the ground. Also handles
    /// turning, leaning, stepping up and jumping. Feed it with <see cref="MoveInput"/>, <see cref="Sprint"/> and
    /// <see cref="RequestJump"/> from a player or AI driver.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(ActiveRagdoll), typeof(BalanceController))]
    public sealed class LocomotionController : MonoBehaviour
    {
        [SerializeField] ActiveRagdoll ragdoll;
        [SerializeField] BalanceController balance;
        [SerializeField] LocomotionProfile profile;

        readonly RaycastHit[] hits = new RaycastHit[8];
        readonly bool[] armSwingSuppressed = new bool[2];
        PhysicsMaterial footMaterial;
        Vector3 lean;
        float gaitPhase;
        float gaitWeight;
        float lastGroundedTime = float.NegativeInfinity;
        float jumpRequestTime = float.NegativeInfinity;
        float lastJumpTime = float.NegativeInfinity;
        bool liftSuspended;

        public LocomotionProfile Profile => profile;

        /// <summary>Desired horizontal move direction in world space. Magnitude 0..1 scales the speed.</summary>
        public Vector3 MoveInput { get; set; }

        public bool Sprint { get; set; }

        /// <summary>Multiplier on speed and jump height, e.g. lowered while carrying something heavy.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        public bool IsGrounded { get; private set; }
        public bool IsStepping { get; private set; }
        public float GaitPhase => gaitPhase;
        public Vector3 HorizontalVelocity { get; private set; }

        public void RequestJump() => jumpRequestTime = Time.time;

        /// <summary>Stops the gait from posing an arm, e.g. while it reaches or holds something.</summary>
        public void SetArmSwingSuppressed(BodySide side, bool suppressed) => armSwingSuppressed[(int)side] = suppressed;

        internal void Configure(ActiveRagdoll owner, BalanceController balanceController, LocomotionProfile locomotionProfile)
        {
            ragdoll = owner;
            balance = balanceController;
            profile = locomotionProfile;
        }

        void Reset()
        {
            ragdoll = GetComponent<ActiveRagdoll>();
            balance = GetComponent<BalanceController>();
        }

        void Awake()
        {
            if (ragdoll == null) ragdoll = GetComponent<ActiveRagdoll>();
            if (balance == null) balance = GetComponent<BalanceController>();
        }

        void Start()
        {
            if (profile == null)
            {
                return;
            }

            footMaterial = new PhysicsMaterial("Ragdoll Foot")
            {
                staticFriction = profile.gait.footFriction,
                dynamicFriction = profile.gait.footFriction,
                frictionCombine = PhysicsMaterialCombine.Multiply,
            };
            for (int i = 0; i < 2; i++)
            {
                if (ragdoll.TryGetBone(BoneIds.Foot((BodySide)i), out RagdollBone foot) && foot.MainCollider != null)
                {
                    foot.MainCollider.sharedMaterial = footMaterial;
                }
            }
        }

        void OnDestroy()
        {
            if (footMaterial != null) Destroy(footMaterial);
        }

        void FixedUpdate()
        {
            if (profile == null || ragdoll.Pelvis == null)
            {
                return;
            }

            float dt = Time.fixedDeltaTime;
            Vector3 up = BalanceController.Up;
            GroundInfo ground = balance.Ground;
            Vector3 groundVelocity = GroundVelocity(ground);
            HorizontalVelocity = Vector3.ProjectOnPlane(ragdoll.AverageVelocity - groundVelocity, up);

            IsGrounded = ground.isGrounded && ground.pelvisHeight <= balance.StandHeight + profile.jump.groundedTolerance;
            if (IsGrounded) lastGroundedTime = Time.time;

            if (ragdoll.State != RagdollState.Normal)
            {
                ResetPose(up);
                return;
            }

            UpdateLiftSuspension(up);
            Vector3 acceleration = ApplyMovement(up, ground, dt);
            UpdateFacing(up, dt);
            UpdateLean(up, acceleration, dt);
            UpdateStepUp(up);
            UpdateJump(up);
            UpdateGait(dt);
        }

        Vector3 ApplyMovement(Vector3 up, GroundInfo ground, float dt)
        {
            MovementSettings movement = profile.movement;
            Vector3 input = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(MoveInput, up), 1f);
            float speed = (Sprint ? movement.sprintSpeed : movement.walkSpeed) * SpeedMultiplier;
            Vector3 target = input * speed;

            if (IsGrounded && ground.isGrounded)
            {
                // Follow slopes: move along the ground plane, keeping the requested horizontal speed.
                Vector3 alongGround = Vector3.ProjectOnPlane(target, ground.normal);
                target = alongGround.sqrMagnitude > 1e-6f ? alongGround.normalized * target.magnitude : target;
                target = Vector3.ProjectOnPlane(target, up);
            }

            float rate = !IsGrounded ? movement.airAcceleration
                : input.sqrMagnitude > 1e-4f ? movement.acceleration
                : movement.deceleration;

            Vector3 next = Vector3.MoveTowards(HorizontalVelocity, target, rate * dt);
            Vector3 acceleration = (next - HorizontalVelocity) / dt;

            float share = ragdoll.Chest != null ? movement.chestForceShare : 0f;
            Vector3 force = acceleration * ragdoll.TotalMass;
            ragdoll.Pelvis.Body.AddForce(force * (1f - share), ForceMode.Force);
            if (share > 0f) ragdoll.Chest.Body.AddForce(force * share, ForceMode.Force);
            return acceleration;
        }

        void UpdateFacing(Vector3 up, float dt)
        {
            Vector3 input = Vector3.ProjectOnPlane(MoveInput, up);
            if (input.sqrMagnitude < 1e-4f)
            {
                return;
            }

            Vector3 current = balance.DesiredFacing.sqrMagnitude > 1e-6f ? balance.DesiredFacing : input;
            float maxRadians = profile.movement.turnSpeed * Mathf.Deg2Rad * dt;
            balance.DesiredFacing = Vector3.RotateTowards(current, input.normalized, maxRadians, 0f);
        }

        void UpdateLean(Vector3 up, Vector3 acceleration, float dt)
        {
            LeanSettings settings = profile.lean;
            float sprintSpeed = Mathf.Max(profile.movement.sprintSpeed, 0.01f);
            Vector3 target = IsGrounded
                ? acceleration * settings.perAcceleration + HorizontalVelocity * (settings.atSprintSpeed / sprintSpeed)
                : Vector3.zero;
            target = Vector3.ClampMagnitude(target, settings.max);
            lean = Vector3.Lerp(lean, target, 1f - Mathf.Exp(-settings.response * dt));

            // lean holds degrees along its direction; tilt the up target that way.
            float degrees = lean.magnitude;
            balance.TargetUp = degrees > 1e-3f
                ? Quaternion.AngleAxis(degrees, Vector3.Cross(up, lean / degrees)) * up
                : up;
        }

        void UpdateStepUp(Vector3 up)
        {
            StepUpSettings settings = profile.stepUp;
            IsStepping = false;
            Vector3 direction = HorizontalVelocity.sqrMagnitude > 0.01f ? HorizontalVelocity.normalized : Vector3.zero;

            if (IsGrounded && direction != Vector3.zero && balance.Ground.isGrounded)
            {
                Vector3 floor = balance.Ground.point;
                Vector3 low = floor + up * (settings.maxStepHeight * 0.25f);
                Vector3 high = floor + up * settings.maxStepHeight;
                IsStepping = Cast(low, direction, settings.probeDistance) && !Cast(high, direction, settings.probeDistance);
            }

            balance.HeightOffset = IsStepping ? settings.heightBoost : 0f;
        }

        void UpdateJump(Vector3 up)
        {
            JumpSettings jump = profile.jump;
            bool buffered = Time.time - jumpRequestTime <= jump.bufferTime;
            bool canJump = Time.time - lastGroundedTime <= jump.coyoteTime && Time.time - lastJumpTime >= jump.cooldown;
            if (!buffered || !canJump)
            {
                return;
            }

            float height = jump.jumpHeight * SpeedMultiplier;
            float speed = Mathf.Sqrt(2f * Physics.gravity.magnitude * height);
            foreach (RagdollBone bone in ragdoll.Bones)
            {
                float vertical = Vector3.Dot(bone.Body.linearVelocity, up);
                bone.Body.AddForce(up * (speed - Mathf.Min(vertical, 0f)), ForceMode.VelocityChange);
            }

            lastJumpTime = Time.time;
            jumpRequestTime = float.NegativeInfinity;
            lastGroundedTime = float.NegativeInfinity;
            liftSuspended = true;
            balance.SuspendLift = true;
        }

        void UpdateLiftSuspension(Vector3 up)
        {
            if (!liftSuspended)
            {
                return;
            }

            float vertical = Vector3.Dot(ragdoll.Pelvis.Body.linearVelocity, up);
            if (vertical <= 0f || Time.time - lastJumpTime >= profile.jump.liftSuspendTime)
            {
                liftSuspended = false;
                balance.SuspendLift = false;
            }
        }

        void UpdateGait(float dt)
        {
            GaitSettings gait = profile.gait;
            float speed = HorizontalVelocity.magnitude;
            bool moving = IsGrounded && speed > gait.idleSpeed;
            gaitWeight = Mathf.MoveTowards(gaitWeight, moving ? 1f : 0f, gait.blendSpeed * dt);

            float cadence = Mathf.Clamp(speed / gait.strideLength, gait.minCadence, gait.maxCadence);
            if (moving || gaitWeight > 0f)
            {
                gaitPhase = Mathf.Repeat(gaitPhase + cadence * dt, 1f);
            }

            float walk = Mathf.Max(profile.movement.walkSpeed, 0.01f);
            float sprint = Mathf.Max(profile.movement.sprintSpeed, walk + 0.01f);
            float intensity = Mathf.Clamp01((speed - walk) / (sprint - walk));
            float speedScale = Mathf.Clamp01(speed / walk);

            for (int i = 0; i < 2; i++)
            {
                var side = (BodySide)i;
                float phase = gaitPhase + i * 0.5f;
                PoseLeg(side, phase, intensity, speedScale);
                PoseArm(side, phase, intensity, speedScale);
            }
        }

        void PoseLeg(BodySide side, float phase, float intensity, float speedScale)
        {
            GaitSettings gait = profile.gait;
            JumpSettings jump = profile.jump;
            float angle = phase * 2f * Mathf.PI;

            // Positive joint X rotates a limb backwards. cos = 1 puts the leg fully forward; the leg swings forward
            // (and lifts its knee) while sin < 0.
            float hipAmplitude = Mathf.Lerp(gait.hipSwingWalk, gait.hipSwingSprint, intensity) * speedScale;
            float kneeSwing = Mathf.Lerp(gait.kneeSwingWalk, gait.kneeSwingSprint, intensity) * speedScale;
            if (IsStepping) kneeSwing += profile.stepUp.extraKneeBend;

            float swing = Mathf.Max(0f, -Mathf.Sin(angle));
            float hip = -hipAmplitude * Mathf.Cos(angle) * gaitWeight;
            float knee = (gait.kneeStance + kneeSwing * swing) * gaitWeight;

            if (!IsGrounded)
            {
                hip = jump.airHip;
                knee = jump.airKnee;
            }

            float ankle = -(hip + knee) * gait.ankleCompensation;

            SetJointX(BoneIds.UpperLeg(side), hip);
            SetJointX(BoneIds.LowerLeg(side), knee);
            SetJointX(BoneIds.Foot(side), ankle);
        }

        void PoseArm(BodySide side, float phase, float intensity, float speedScale)
        {
            if (armSwingSuppressed[(int)side])
            {
                return;
            }

            GaitSettings gait = profile.gait;
            float amplitude = Mathf.Lerp(gait.armSwingWalk, gait.armSwingSprint, intensity) * speedScale * gaitWeight;
            // Arms swing opposite to the leg on the same side.
            float shoulder = amplitude * Mathf.Cos(phase * 2f * Mathf.PI);
            SetJointX(BoneIds.UpperArm(side), shoulder);
            SetJointX(BoneIds.LowerArm(side), -gait.elbowBend * gaitWeight);
        }

        void ResetPose(Vector3 up)
        {
            gaitWeight = 0f;
            lean = Vector3.zero;
            balance.TargetUp = up;
            balance.HeightOffset = 0f;
            if (liftSuspended)
            {
                liftSuspended = false;
                balance.SuspendLift = false;
            }

            for (int i = 0; i < 2; i++)
            {
                var side = (BodySide)i;
                ResetTarget(BoneIds.UpperLeg(side));
                ResetTarget(BoneIds.LowerLeg(side));
                ResetTarget(BoneIds.Foot(side));
                if (!armSwingSuppressed[i])
                {
                    ResetTarget(BoneIds.UpperArm(side));
                    ResetTarget(BoneIds.LowerArm(side));
                }
            }
        }

        void SetJointX(BoneId id, float degrees)
        {
            if (ragdoll.TryGetBone(id, out RagdollBone bone))
            {
                bone.SetTargetJointRotation(Quaternion.Euler(degrees, 0f, 0f));
            }
        }

        void ResetTarget(BoneId id)
        {
            if (ragdoll.TryGetBone(id, out RagdollBone bone))
            {
                bone.ResetTarget();
            }
        }

        bool Cast(Vector3 origin, Vector3 direction, float distance)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, balance.Profile.ground.groundLayers,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!ragdoll.IsOwnCollider(hits[i].collider))
                {
                    return true;
                }
            }

            return false;
        }

        static Vector3 GroundVelocity(GroundInfo ground)
        {
            Rigidbody body = ground.Body;
            return body != null ? body.GetPointVelocity(ground.point) : Vector3.zero;
        }
    }
}
