using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Wobblefolk.Ragdoll
{
    /// <summary>A hand's hold on a collider.</summary>
    public readonly struct GrabInfo
    {
        public readonly Collider collider;
        public readonly Rigidbody body;
        public readonly Vector3 localPoint;
        public readonly float startTime;

        public GrabInfo(Collider collider, Rigidbody body, Vector3 localPoint, float startTime)
        {
            this.collider = collider;
            this.body = body;
            this.localPoint = localPoint;
            this.startTime = startTime;
        }

        public bool IsStatic => body == null;

        /// <summary>Current world position of the grip point.</summary>
        public Vector3 Point => body != null ? body.transform.TransformPoint(localPoint) : localPoint;
    }

    [Serializable]
    public sealed class GrabEvent : UnityEvent<BodySide, Collider> { }

    [Serializable]
    public sealed class ReleaseEvent : UnityEvent<BodySide, Collider, bool> { }

    [Serializable]
    public sealed class ArmEvents
    {
        public GrabEvent grabbed = new GrabEvent();
        /// <summary>(side, collider, broken) — broken is true when the grip was torn loose.</summary>
        public ReleaseEvent released = new ReleaseEvent();
    }

    /// <summary>
    /// Independent left / right arms. While an arm's input is held it reaches towards the aim target, and the hand
    /// grabs the first surface it touches with a breakable joint. Releasing the input lets go.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(20)]
    [RequireComponent(typeof(ActiveRagdoll))]
    public sealed class ArmController : MonoBehaviour
    {
        const int MaxOverlaps = 16;

        [SerializeField] ActiveRagdoll ragdoll;
        [SerializeField] BalanceController balance;
        [SerializeField] LocomotionController locomotion;
        [SerializeField] ArmProfile profile;
        [SerializeField] ArmEvents events = new ArmEvents();

        sealed class Arm
        {
            public BodySide side;
            public RagdollBone upper;
            public RagdollBone lower;
            public RagdollBone hand;
            public Vector3 directionLocal;
            public Quaternion restRelativeToChest;
            public bool input;
            public bool reaching;
            public bool holding;
            public GrabInfo grab;
            public ConfigurableJoint joint;
            public float releaseTime = float.NegativeInfinity;
            public readonly List<Collider> heldColliders = new List<Collider>();
            public readonly List<Collider> armColliders = new List<Collider>();
        }

        /// <summary>Original settings of a held body, restored a moment after the last hand lets go.</summary>
        sealed class SavedBody
        {
            public float maxDepenetrationVelocity;
            public int solverIterations;
            public float restoreTime = -1f;
        }

        readonly Arm[] arms = new Arm[2];
        readonly Dictionary<Rigidbody, SavedBody> savedBodies = new Dictionary<Rigidbody, SavedBody>();
        readonly List<Rigidbody> restored = new List<Rigidbody>();
        readonly Collider[] overlaps = new Collider[MaxOverlaps];
        readonly RaycastHit[] hits = new RaycastHit[MaxOverlaps];
        bool initialized;
        float bend;
        float grabBlockedUntil = float.NegativeInfinity;

        public ArmProfile Profile => profile;
        public ArmEvents Events => events;

        /// <summary>Ray the arms aim along, usually the camera's view ray. Set every frame by a driver.</summary>
        public Ray AimRay { get; set; }
        public bool HasAim { get; set; }

        /// <summary>World point the arms reached towards in the last physics step.</summary>
        public Vector3 AimPoint { get; private set; }

        public event Action<BodySide, GrabInfo> Grabbed;
        /// <summary>(side, grab, broken)</summary>
        public event Action<BodySide, GrabInfo, bool> Released;

        public void SetReach(BodySide side, bool held)
        {
            if (arms[(int)side] != null) arms[(int)side].input = held;
        }

        public bool IsReaching(BodySide side) => arms[(int)side] != null && arms[(int)side].reaching;
        public bool IsHolding(BodySide side) => arms[(int)side] != null && arms[(int)side].holding;

        public bool TryGetGrab(BodySide side, out GrabInfo grab)
        {
            Arm arm = arms[(int)side];
            grab = arm != null ? arm.grab : default;
            return arm != null && arm.holding;
        }

        /// <summary>The joint holding the grabbed object, for systems that tune it (e.g. carrying).</summary>
        public ConfigurableJoint GetGripJoint(BodySide side) => arms[(int)side]?.joint;

        internal void Configure(ActiveRagdoll owner, BalanceController balanceController, LocomotionController locomotionController,
            ArmProfile armProfile)
        {
            ragdoll = owner;
            balance = balanceController;
            locomotion = locomotionController;
            profile = armProfile;
        }

        void Reset()
        {
            ragdoll = GetComponent<ActiveRagdoll>();
            balance = GetComponent<BalanceController>();
            locomotion = GetComponent<LocomotionController>();
        }

        void Awake()
        {
            if (ragdoll == null) ragdoll = GetComponent<ActiveRagdoll>();
            if (balance == null) balance = GetComponent<BalanceController>();
            if (locomotion == null) locomotion = GetComponent<LocomotionController>();
        }

        void Start()
        {
            RagdollBone chest = ragdoll.Chest;
            if (chest == null)
            {
                return;
            }

            for (int i = 0; i < 2; i++)
            {
                var side = (BodySide)i;
                var arm = new Arm
                {
                    side = side,
                    upper = ragdoll.GetBone(BoneIds.UpperArm(side)),
                    lower = ragdoll.GetBone(BoneIds.LowerArm(side)),
                    hand = ragdoll.GetBone(BoneIds.Hand(side)),
                };
                if (arm.upper == null || arm.lower == null || arm.hand == null)
                {
                    continue;
                }

                // Rest-pose direction from shoulder to elbow, in upper-arm space, and the rest orientation relative
                // to the chest. Captured on Start while the ragdoll is still in its built pose.
                Vector3 toElbow = arm.lower.transform.position - arm.upper.transform.position;
                arm.directionLocal = arm.upper.transform.InverseTransformDirection(toElbow.normalized);
                arm.restRelativeToChest = Quaternion.Inverse(chest.transform.rotation) * arm.upper.transform.rotation;
                arm.armColliders.AddRange(arm.lower.GetComponentsInChildren<Collider>());
                arms[i] = arm;
            }

            initialized = true;
        }

        void OnDisable()
        {
            foreach (Arm arm in arms)
            {
                if (arm != null && arm.holding) Release(arm, false);
            }
        }

        /// <summary>Prevents new grabs for a while, e.g. right after climbing over a ledge.</summary>
        public void BlockGrabs(float duration) => grabBlockedUntil = Mathf.Max(grabBlockedUntil, Time.time + duration);

        /// <summary>Lets go with one hand, even while its input is still held.</summary>
        public void Release(BodySide side)
        {
            Arm arm = arms[(int)side];
            if (arm != null && arm.holding) Release(arm, false);
        }

        void FixedUpdate()
        {
            if (!initialized || profile == null)
            {
                return;
            }

            bool canAct = ragdoll.State == RagdollState.Normal;
            if (canAct || !profile.grab.releaseOnFall)
            {
                AimPoint = ComputeAimPoint();
            }

            bool anyReaching = false;
            foreach (Arm arm in arms)
            {
                if (arm == null) continue;

                if (arm.holding && arm.joint == null)
                {
                    // Unity destroys a joint whose break force was exceeded.
                    Release(arm, true);
                }

                bool keepHolding = arm.holding && (canAct || !profile.grab.releaseOnFall);
                arm.reaching = arm.input && (canAct || keepHolding);
                anyReaching |= arm.reaching;

                if (arm.holding && (!arm.input || !keepHolding))
                {
                    Release(arm, false);
                }

                float strength = arm.reaching ? profile.reach.muscleStrength : 1f;
                arm.upper.SetLocalMuscleStrength(strength);
                arm.lower.SetLocalMuscleStrength(strength);
                arm.hand.SetLocalMuscleStrength(strength);
                if (locomotion != null) locomotion.SetArmSwingSuppressed(arm.side, arm.reaching);

                if (arm.reaching)
                {
                    PoseArm(arm);
                    if (!arm.holding && canAct && Time.time >= grabBlockedUntil
                        && Time.time - arm.releaseTime >= profile.grab.regrabDelay)
                    {
                        TryGrab(arm);
                    }
                }
                else if (locomotion == null || ragdoll.State != RagdollState.Normal)
                {
                    arm.upper.ResetTarget();
                    arm.lower.ResetTarget();
                    arm.hand.ResetTarget();
                }
            }

            if (anyReaching && canAct) FaceAim();
            UpdateBend(anyReaching && canAct);
            RestoreReleasedBodies();
        }

        void RestoreReleasedBodies()
        {
            foreach (KeyValuePair<Rigidbody, SavedBody> pair in savedBodies)
            {
                if (pair.Value.restoreTime >= 0f && Time.time >= pair.Value.restoreTime)
                {
                    restored.Add(pair.Key);
                }
            }

            foreach (Rigidbody body in restored)
            {
                SavedBody saved = savedBodies[body];
                savedBodies.Remove(body);
                if (body != null)
                {
                    body.maxDepenetrationVelocity = saved.maxDepenetrationVelocity;
                    body.solverIterations = saved.solverIterations;
                }
            }

            restored.Clear();
        }

        void UpdateBend(bool active)
        {
            ReachSettings reach = profile.reach;
            float target = 0f;
            if (active)
            {
                Vector3 direction = (AimPoint - ragdoll.Chest.Body.position).normalized;
                float depression = -Mathf.Asin(Mathf.Clamp(Vector3.Dot(direction, BalanceController.Up), -1f, 1f)) * Mathf.Rad2Deg;
                target = Mathf.Clamp((depression - reach.bendStartAngle) * reach.bendPerDegree, 0f, reach.maxBend);
            }

            bend = Mathf.Lerp(bend, target, 1f - Mathf.Exp(-reach.bendResponse * Time.fixedDeltaTime));
            if (bend < 0.05f && target == 0f) bend = 0f;

            float pelvisBend = Mathf.Min(bend * reach.pelvisBendShare, reach.maxPelvisBend);
            if (balance != null)
            {
                balance.ChestBend = bend;
                balance.PelvisBend = pelvisBend;
            }

            // The spine bends by whatever the pelvis does not.
            ragdoll.Chest.SetTargetJointRotation(Quaternion.Euler(bend - pelvisBend, 0f, 0f));
        }

        Vector3 ComputeAimPoint()
        {
            Vector3 chest = ragdoll.Chest.Body.position;
            Ray ray = HasAim ? AimRay : new Ray(chest, balance != null ? balance.DesiredFacing : transform.forward);
            ReachSettings reach = profile.reach;

            int count = Physics.RaycastNonAlloc(ray, hits, reach.maxAimDistance, reach.aimLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 point = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].distance < best && !ragdoll.IsOwnOrHeld(hits[i].collider))
                {
                    best = hits[i].distance;
                    point = hits[i].point;
                }
            }

            if (best < float.MaxValue)
            {
                return point;
            }

            // Nothing hit: reach a fixed distance past the point on the ray closest to the chest.
            float along = Vector3.Dot(chest - ray.origin, ray.direction);
            return ray.GetPoint(Mathf.Max(0f, along) + reach.defaultReachDistance);
        }

        void PoseArm(Arm arm)
        {
            Vector3 shoulder = arm.upper.Body.position;
            Vector3 direction = AimPoint - shoulder;
            if (direction.sqrMagnitude < 1e-4f)
            {
                direction = HasAim ? AimRay.direction : ragdoll.Chest.transform.forward;
            }

            Quaternion rest = ragdoll.Chest.Body.rotation * arm.restRelativeToChest;
            Quaternion target = Quaternion.FromToRotation(rest * arm.directionLocal, direction.normalized) * rest;
            arm.upper.SetTargetWorldRotation(target);
            arm.lower.SetTargetJointRotation(Quaternion.Euler(-profile.reach.elbowBend, 0f, 0f));
            arm.hand.ResetTarget();
        }

        void FaceAim()
        {
            ReachSettings reach = profile.reach;
            if (!reach.faceAim || balance == null || (locomotion != null && locomotion.MoveInput.sqrMagnitude > 1e-4f))
            {
                return;
            }

            Vector3 up = BalanceController.Up;
            Vector3 aim = Vector3.ProjectOnPlane(AimPoint - ragdoll.Chest.Body.position, up);
            if (aim.sqrMagnitude < 1e-4f)
            {
                return;
            }

            float maxRadians = reach.faceAimTurnSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime;
            balance.DesiredFacing = Vector3.RotateTowards(balance.DesiredFacing, aim.normalized, maxRadians, 0f);
        }

        void TryGrab(Arm arm)
        {
            GrabSettings settings = profile.grab;
            Collider handCollider = arm.hand.MainCollider;
            if (handCollider == null)
            {
                return;
            }

            Bounds bounds = handCollider.bounds;
            Vector3 center = bounds.center;
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z) + settings.grabRadius;
            int count = Physics.OverlapSphereNonAlloc(center, radius, overlaps, settings.grabLayers, QueryTriggerInteraction.Ignore);

            Collider best = null;
            Vector3 bestPoint = Vector3.zero;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider other = overlaps[i];
                if (ragdoll.IsOwnCollider(other) || (!settings.allowStatic && other.attachedRigidbody == null))
                {
                    continue;
                }

                Vector3 point = ClosestPoint(other, center);
                float distance = (point - center).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = other;
                    bestPoint = point;
                }
            }

            if (best != null)
            {
                Attach(arm, best, bestPoint);
            }
        }

        static Vector3 ClosestPoint(Collider other, Vector3 position)
        {
            bool supported = !(other is MeshCollider mesh) || mesh.convex;
            return supported ? other.ClosestPoint(position) : other.bounds.ClosestPoint(position);
        }

        void Attach(Arm arm, Collider other, Vector3 point)
        {
            GrabSettings settings = profile.grab;
            Rigidbody body = other.attachedRigidbody;
            Rigidbody hand = arm.hand.Body;

            // Anchors are computed from the transforms on purpose: adding a joint syncs each body to its transform, so
            // transform-space anchors match what the solver sees. (The auto-configured connected anchor is only
            // computed later, inside the physics step, and reads back as zero here.)
            Vector3 bodyLocalPoint = body != null ? body.transform.InverseTransformPoint(point) : point;
            ConfigurableJoint joint = arm.hand.gameObject.AddComponent<ConfigurableJoint>();
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = arm.hand.transform.InverseTransformPoint(point);
            joint.connectedBody = body;
            joint.connectedAnchor = bodyLocalPoint;
            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;

            ConfigurableJointMotion angular = settings.lockRotation ? ConfigurableJointMotion.Locked : ConfigurableJointMotion.Free;
            joint.angularXMotion = angular;
            joint.angularYMotion = angular;
            joint.angularZMotion = angular;
            joint.rotationDriveMode = RotationDriveMode.Slerp;
            joint.slerpDrive = new JointDrive
            {
                positionSpring = settings.angularSpring,
                positionDamper = settings.angularDamper,
                maximumForce = float.MaxValue,
            };

            joint.breakForce = settings.breakForce > 0f ? settings.breakForce : float.PositiveInfinity;
            joint.breakTorque = settings.breakTorque > 0f ? settings.breakTorque : float.PositiveInfinity;
            joint.enablePreprocessing = false;
            if (body != null)
            {
                float ratio = body.mass / hand.mass;
                if (settings.maxSolverMassRatio > 0f && ratio > settings.maxSolverMassRatio)
                {
                    joint.connectedMassScale = ratio / settings.maxSolverMassRatio;
                }

                if (!savedBodies.TryGetValue(body, out SavedBody saved))
                {
                    saved = new SavedBody
                    {
                        maxDepenetrationVelocity = body.maxDepenetrationVelocity,
                        solverIterations = body.solverIterations,
                    };
                    savedBodies.Add(body, saved);
                }

                saved.restoreTime = -1f;
                body.maxDepenetrationVelocity = Mathf.Min(body.maxDepenetrationVelocity, settings.heldMaxDepenetrationVelocity);
                body.solverIterations = Mathf.Max(body.solverIterations, settings.heldSolverIterations);
            }

            ragdoll.RegisterHeld(body);
            arm.joint = joint;
            arm.holding = true;
            arm.grab = new GrabInfo(other, body, bodyLocalPoint, Time.time);

            arm.heldColliders.Clear();
            if (settings.ignoreArmCollisions)
            {
                if (body != null) body.GetComponentsInChildren(arm.heldColliders);
                else arm.heldColliders.Add(other);
                SetArmCollisions(arm, true);
            }

            events.grabbed.Invoke(arm.side, other);
            Grabbed?.Invoke(arm.side, arm.grab);
        }

        void Release(Arm arm, bool broken)
        {
            if (arm.joint != null)
            {
                Destroy(arm.joint);
            }

            SetArmCollisions(arm, false);
            arm.heldColliders.Clear();

            // Keep the depenetration cap a little longer: a released object may still overlap the body.
            Rigidbody body = arm.grab.body;
            ragdoll.UnregisterHeld(body);
            if (body != null && !IsHeldByOtherArm(arm, body) && savedBodies.TryGetValue(body, out SavedBody saved))
            {
                saved.restoreTime = Time.time + profile.grab.restoreDelay;
            }

            arm.joint = null;
            arm.holding = false;
            arm.releaseTime = Time.time;

            GrabInfo grab = arm.grab;
            arm.grab = default;
            events.released.Invoke(arm.side, grab.collider, broken);
            Released?.Invoke(arm.side, grab, broken);
        }

        bool IsHeldByOtherArm(Arm arm, Rigidbody body)
        {
            foreach (Arm other in arms)
            {
                if (other != null && other != arm && other.holding && other.grab.body == body)
                {
                    return true;
                }
            }

            return false;
        }

        static void SetArmCollisions(Arm arm, bool ignore)
        {
            foreach (Collider held in arm.heldColliders)
            {
                if (held == null) continue;
                foreach (Collider own in arm.armColliders)
                {
                    if (own != null) Physics.IgnoreCollision(held, own, ignore);
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || !initialized)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(AimPoint, 0.08f);
            foreach (Arm arm in arms)
            {
                if (arm == null) continue;
                Gizmos.color = arm.holding ? Color.green : arm.reaching ? Color.yellow : Color.gray;
                Gizmos.DrawLine(arm.upper.Body.position, arm.hand.Body.position);
            }
        }
    }
}
