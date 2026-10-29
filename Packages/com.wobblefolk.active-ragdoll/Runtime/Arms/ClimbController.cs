using System;
using UnityEngine;
using UnityEngine.Events;

namespace Wobblefolk.Ragdoll
{
    public enum ClimbPhase
    {
        None = 0,
        /// <summary>Holding a ledge with the feet off the ground.</summary>
        Hanging = 1,
        /// <summary>Pulling the chest up towards the ledge top.</summary>
        PullingUp = 2,
        /// <summary>Hands let go; the pelvis is pushed over and onto the ledge.</summary>
        Mantling = 3,
    }

    [Serializable]
    public sealed class ClimbEvents
    {
        public UnityEvent startedHanging = new UnityEvent();
        public UnityEvent startedClimbing = new UnityEvent();
        public UnityEvent finishedClimbing = new UnityEvent();
        public UnityEvent failedClimbing = new UnityEvent();
    }

    /// <summary>
    /// Simple ledge climbing. While a hand holds a static (or anchor) surface above the chest the character can hang;
    /// <see cref="RequestClimb"/> finds the ledge top above the grip, pulls the chest up, then lets go and pushes the
    /// pelvis over the edge.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(40)]
    [RequireComponent(typeof(ActiveRagdoll), typeof(ArmController))]
    public sealed class ClimbController : MonoBehaviour
    {
        [SerializeField] ActiveRagdoll ragdoll;
        [SerializeField] BalanceController balance;
        [SerializeField] LocomotionController locomotion;
        [SerializeField] ArmController arms;
        [SerializeField] ClimbProfile profile;
        [SerializeField] ClimbEvents events = new ClimbEvents();

        readonly RaycastHit[] hits = new RaycastHit[8];
        float requestTime = float.NegativeInfinity;
        float phaseStartTime;
        Vector3 grip;
        Vector3 ledgeTop;
        Vector3 climbForward;

        public ClimbProfile Profile => profile;
        public ClimbEvents Events => events;
        public ClimbPhase Phase { get; private set; }

        /// <summary>World position of the ledge top being climbed onto.</summary>
        public Vector3 LedgeTop => ledgeTop;

        public void RequestClimb() => requestTime = Time.time;

        internal void Configure(ActiveRagdoll owner, BalanceController balanceController, LocomotionController locomotionController,
            ArmController armController, ClimbProfile climbProfile)
        {
            ragdoll = owner;
            balance = balanceController;
            locomotion = locomotionController;
            arms = armController;
            profile = climbProfile;
        }

        /// <summary>True for grips that act as fixed climbing anchors rather than objects to carry.</summary>
        public bool IsAnchor(Rigidbody body)
        {
            if (body == null) return true;
            if (profile == null) return body.isKinematic;
            return (profile.anchors.kinematicIsAnchor && body.isKinematic) || body.mass >= profile.anchors.minAnchorMass;
        }

        void Reset()
        {
            ragdoll = GetComponent<ActiveRagdoll>();
            balance = GetComponent<BalanceController>();
            locomotion = GetComponent<LocomotionController>();
            arms = GetComponent<ArmController>();
        }

        void Awake()
        {
            if (ragdoll == null) ragdoll = GetComponent<ActiveRagdoll>();
            if (balance == null) balance = GetComponent<BalanceController>();
            if (locomotion == null) locomotion = GetComponent<LocomotionController>();
            if (arms == null) arms = GetComponent<ArmController>();
        }

        void OnDisable()
        {
            SetPhase(ClimbPhase.None);
        }

        void FixedUpdate()
        {
            if (profile == null || arms == null)
            {
                return;
            }

            if (ragdoll.State != RagdollState.Normal)
            {
                Fail(ClimbPhase.None);
                return;
            }

            switch (Phase)
            {
                case ClimbPhase.None:
                case ClimbPhase.Hanging:
                    UpdateHolding();
                    break;

                case ClimbPhase.PullingUp:
                    if (TryFindGrip(false, out grip)) UpdatePullUp();
                    else Fail(ClimbPhase.None);
                    break;

                case ClimbPhase.Mantling:
                    UpdateMantle();
                    break;
            }
        }

        void UpdateHolding()
        {
            if (!TryFindGrip(true, out grip))
            {
                SetPhase(ClimbPhase.None);
                return;
            }

            bool grounded = locomotion != null ? locomotion.IsGrounded : balance.Ground.isGrounded;
            if (Time.time - requestTime <= profile.requestBufferTime)
            {
                requestTime = float.NegativeInfinity;
                BeginClimb();
            }
            else if (Phase == ClimbPhase.None && !grounded)
            {
                SetPhase(ClimbPhase.Hanging);
                events.startedHanging.Invoke();
            }
            else if (Phase == ClimbPhase.Hanging && grounded)
            {
                SetPhase(ClimbPhase.None);
            }
        }

        /// <summary>Average of the hands holding climbing anchors; optionally only grips above the chest count.</summary>
        bool TryFindGrip(bool requireAboveChest, out Vector3 point)
        {
            point = Vector3.zero;
            int count = 0;
            Vector3 up = BalanceController.Up;
            float minHeight = Vector3.Dot(ragdoll.Chest.Body.position, up) + profile.anchors.minHeightAboveChest;

            for (int i = 0; i < 2; i++)
            {
                if (!arms.TryGetGrab((BodySide)i, out GrabInfo grab) || !IsAnchor(grab.body))
                {
                    continue;
                }

                Vector3 p = grab.Point;
                if (!requireAboveChest || Vector3.Dot(p, up) >= minHeight)
                {
                    point += p;
                    count++;
                }
            }

            if (count == 0)
            {
                return false;
            }

            point /= count;
            return true;
        }

        void BeginClimb()
        {
            Vector3 up = BalanceController.Up;
            climbForward = Vector3.ProjectOnPlane(grip - ragdoll.Pelvis.Body.position, up);
            if (climbForward.sqrMagnitude < 1e-4f) climbForward = Vector3.ProjectOnPlane(balance.DesiredFacing, up);
            climbForward.Normalize();

            if (!TryFindLedgeTop(up, out ledgeTop))
            {
                events.failedClimbing.Invoke();
                return;
            }

            balance.DesiredFacing = climbForward;
            SetPhase(ClimbPhase.PullingUp);
            events.startedClimbing.Invoke();
        }

        /// <summary>Looks down onto the surface just beyond the grip to find the top the character climbs onto.</summary>
        bool TryFindLedgeTop(Vector3 up, out Vector3 top)
        {
            LedgeProbeSettings probe = profile.ledge;
            Vector3 origin = grip + climbForward * probe.inset + up * probe.maxHeightAboveGrip;
            float distance = probe.maxHeightAboveGrip + profile.mantle.completeClearance;
            float minDot = Mathf.Cos(probe.maxTopAngle * Mathf.Deg2Rad);
            LayerMask layers = balance.Profile.ground.groundLayers;

            int count = Physics.RaycastNonAlloc(origin, -up, hits, distance, layers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            top = grip;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.distance > 0f && hit.distance < best && !ragdoll.IsOwnOrHeld(hit.collider)
                    && Vector3.Dot(hit.normal, up) >= minDot)
                {
                    best = hit.distance;
                    top = hit.point;
                }
            }

            return best < float.MaxValue;
        }

        void UpdatePullUp()
        {
            PullUpSettings pull = profile.pullUp;
            Vector3 up = BalanceController.Up;
            Rigidbody chest = ragdoll.Chest.Body;

            // The hands stay on their grip, so the chest cannot rise further above it than the arms allow.
            float targetHeight = Mathf.Min(Vector3.Dot(ledgeTop, up) + pull.chestHeightAboveLedge,
                Vector3.Dot(grip, up) + pull.maxChestAboveGrip);
            Vector3 horizontal = Vector3.ProjectOnPlane(ledgeTop, up) - climbForward * pull.wallOffset;
            Vector3 target = horizontal + up * targetHeight;
            Drive(chest, target, pull);

            if (Vector3.Dot(chest.position, up) >= targetHeight - pull.completeTolerance)
            {
                StartMantle();
            }
            else if (Time.time - phaseStartTime >= pull.timeout)
            {
                Fail(ClimbPhase.Hanging);
            }
        }

        void StartMantle()
        {
            // Let go of the ledge: the arms would otherwise hold the body below the top.
            for (int i = 0; i < 2; i++)
            {
                var side = (BodySide)i;
                if (arms.TryGetGrab(side, out GrabInfo grab) && IsAnchor(grab.body))
                {
                    arms.Release(side);
                }
            }

            arms.BlockGrabs(profile.mantle.timeout + profile.regrabBlockTime);
            SetPhase(ClimbPhase.Mantling);
        }

        void UpdateMantle()
        {
            MantleSettings mantle = profile.mantle;
            Vector3 up = BalanceController.Up;
            Rigidbody pelvis = ragdoll.Pelvis.Body;
            Vector3 target = ledgeTop + up * mantle.pelvisHeightAboveLedge + climbForward * mantle.forwardDistance;
            Drive(pelvis, target, profile.pullUp);

            // Done once the pelvis is high enough and either past the edge or already standing on the top.
            Vector3 offset = pelvis.position - ledgeTop;
            GroundInfo ground = balance.Ground;
            bool onTop = ground.isGrounded && Vector3.Dot(ground.point - ledgeTop, up) >= -mantle.topTolerance;
            bool over = Vector3.Dot(offset, up) >= mantle.completeClearance
                        && (onTop || Vector3.Dot(offset, climbForward) >= mantle.forwardDistance * 0.5f);
            if (over)
            {
                arms.BlockGrabs(profile.regrabBlockTime);
                SetPhase(ClimbPhase.None);
                events.finishedClimbing.Invoke();
            }
            else if (Time.time - phaseStartTime >= mantle.timeout)
            {
                Fail(ClimbPhase.None);
            }
        }

        /// <summary>
        /// Moves the whole ragdoll towards a target for one torso body with a capped PD acceleration plus gravity
        /// compensation. The force is split between the driven body and the other torso body.
        /// </summary>
        void Drive(Rigidbody driven, Vector3 target, PullUpSettings pull)
        {
            Vector3 acceleration = (target - driven.position) * pull.spring - driven.linearVelocity * pull.damper;
            acceleration = JointMath.ClampMagnitude(acceleration, pull.maxAcceleration) - Physics.gravity * pull.gravityCompensation;
            Vector3 force = acceleration * ragdoll.TotalMass;

            Rigidbody other = driven == ragdoll.Chest.Body ? ragdoll.Pelvis.Body : ragdoll.Chest.Body;
            driven.AddForce(force * pull.drivenShare, ForceMode.Force);
            other.AddForce(force * (1f - pull.drivenShare), ForceMode.Force);
        }

        void Fail(ClimbPhase next)
        {
            if (Phase == ClimbPhase.PullingUp || Phase == ClimbPhase.Mantling)
            {
                events.failedClimbing.Invoke();
            }

            SetPhase(next);
        }

        void SetPhase(ClimbPhase next)
        {
            if (next == Phase && next == ClimbPhase.None)
            {
                return;
            }

            Phase = next;
            phaseStartTime = Time.time;
            if (balance == null)
            {
                return;
            }

            if (next == ClimbPhase.None)
            {
                balance.ClearModifier(this);
            }
            else
            {
                // Hanging and climbing: the body dangles, and the stand-height lift would fight the climb drive.
                balance.SetModifier(this, profile.hang.balanceWeight, 0f);
            }
        }

        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || Phase == ClimbPhase.None)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(grip, 0.08f);
            Gizmos.DrawWireSphere(ledgeTop, 0.12f);
            Gizmos.DrawRay(ledgeTop, climbForward * profile.mantle.forwardDistance);
        }
    }
}
