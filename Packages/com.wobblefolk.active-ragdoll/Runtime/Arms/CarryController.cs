using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Wobblefolk.Ragdoll
{
    [Serializable]
    public sealed class StrainEvent : UnityEvent<float> { }

    [Serializable]
    public sealed class CarryEvents
    {
        /// <summary>Strain (0..1) whenever it changes noticeably.</summary>
        public StrainEvent strainChanged = new StrainEvent();
        /// <summary>Hands slipped off a load that was too heavy for too long.</summary>
        public UnityEvent overloadSlipped = new UnityEvent();
    }

    /// <summary>
    /// Turns the weight of held objects into gameplay: slower movement and jumps, weaker balance, leaning back against
    /// the load, extra lift for lifted weight, and hands slipping off loads that are too heavy.
    /// Grips on static or climbable surfaces are ignored here (see <see cref="ClimbController"/>).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(30)]
    [RequireComponent(typeof(ActiveRagdoll), typeof(ArmController))]
    public sealed class CarryController : MonoBehaviour
    {
        const float StrainEventThreshold = 0.05f;

        [SerializeField] ActiveRagdoll ragdoll;
        [SerializeField] BalanceController balance;
        [SerializeField] LocomotionController locomotion;
        [SerializeField] ArmController arms;
        [Tooltip("Optional. Decides which grips are climbing anchors rather than loads.")]
        [SerializeField] ClimbController climb;
        [SerializeField] CarryProfile profile;
        [SerializeField] CarryEvents events = new CarryEvents();

        readonly Dictionary<Rigidbody, int> handsPerBody = new Dictionary<Rigidbody, int>();
        readonly Dictionary<Rigidbody, Collider> colliderPerBody = new Dictionary<Rigidbody, Collider>();
        readonly RaycastHit[] hits = new RaycastHit[8];
        float overloadTimer;
        float reportedStrain;

        public CarryProfile Profile => profile;
        public CarryEvents Events => events;

        /// <summary>0 = comfortable, 1 = at the limit of what the holding hands can carry.</summary>
        public float Strain { get; private set; }

        /// <summary>Effective carried mass: lifted objects fully, dragged ones partially (kg).</summary>
        public float CarriedMass { get; private set; }

        /// <summary>Mass of held objects that are off the ground (kg).</summary>
        public float LiftedMass { get; private set; }

        public bool IsOverloaded { get; private set; }

        internal void Configure(ActiveRagdoll owner, BalanceController balanceController, LocomotionController locomotionController,
            ArmController armController, ClimbController climbController, CarryProfile carryProfile)
        {
            ragdoll = owner;
            balance = balanceController;
            locomotion = locomotionController;
            arms = armController;
            climb = climbController;
            profile = carryProfile;
        }

        void Reset()
        {
            ragdoll = GetComponent<ActiveRagdoll>();
            balance = GetComponent<BalanceController>();
            locomotion = GetComponent<LocomotionController>();
            arms = GetComponent<ArmController>();
            climb = GetComponent<ClimbController>();
        }

        void Awake()
        {
            if (ragdoll == null) ragdoll = GetComponent<ActiveRagdoll>();
            if (balance == null) balance = GetComponent<BalanceController>();
            if (locomotion == null) locomotion = GetComponent<LocomotionController>();
            if (arms == null) arms = GetComponent<ArmController>();
            if (climb == null) climb = GetComponent<ClimbController>();
        }

        void OnDisable()
        {
            if (balance != null) balance.ClearModifier(this);
            if (locomotion != null)
            {
                locomotion.SpeedMultiplier = 1f;
                locomotion.SprintAllowed = true;
                locomotion.AdditionalLean = Vector3.zero;
            }
        }

        void FixedUpdate()
        {
            if (profile == null || arms == null)
            {
                return;
            }

            CollectHeldBodies();

            Vector3 up = BalanceController.Up;
            Vector3 pelvis = ragdoll.Pelvis.Body.position;
            float carried = 0f;
            float lifted = 0f;
            int hands = 0;
            Vector3 loadMoment = Vector3.zero;

            foreach (KeyValuePair<Rigidbody, int> pair in handsPerBody)
            {
                Rigidbody body = pair.Key;
                hands += pair.Value;
                if (IsLifted(body, colliderPerBody[body], up))
                {
                    lifted += body.mass;
                    carried += body.mass;
                    loadMoment += Vector3.ProjectOnPlane(body.worldCenterOfMass - pelvis, up) * body.mass;
                }
                else
                {
                    carried += body.mass * profile.dragMassFactor;
                }
            }

            CarriedMass = carried;
            LiftedMass = lifted;
            float comfortable = hands * profile.comfortableMassPerHand;
            float maximum = hands * profile.maxMassPerHand;
            Strain = hands == 0 || maximum <= comfortable ? 0f : Mathf.Clamp01((carried - comfortable) / (maximum - comfortable));
            IsOverloaded = hands > 0 && lifted > maximum;

            float effect = Mathf.Clamp01(profile.strainCurve.Evaluate(Strain));
            if (locomotion != null)
            {
                locomotion.SpeedMultiplier = Mathf.Lerp(1f, profile.minSpeedMultiplier, effect);
                locomotion.SprintAllowed = Strain <= profile.noSprintStrain;
                locomotion.AdditionalLean = CounterLean(loadMoment);
            }

            if (balance != null)
            {
                balance.SetModifier(this, Mathf.Lerp(1f, profile.minBalanceWeight, effect), 1f, lifted * profile.liftSupport);
            }

            UpdateOverload();
            ReportStrain();
        }

        void CollectHeldBodies()
        {
            handsPerBody.Clear();
            colliderPerBody.Clear();
            for (int i = 0; i < 2; i++)
            {
                if (!arms.TryGetGrab((BodySide)i, out GrabInfo grab) || grab.body == null || IsAnchor(grab.body))
                {
                    continue;
                }

                handsPerBody.TryGetValue(grab.body, out int count);
                handsPerBody[grab.body] = count + 1;
                colliderPerBody[grab.body] = grab.collider;
            }
        }

        bool IsAnchor(Rigidbody body) => climb != null ? climb.IsAnchor(body) : body.isKinematic;

        bool IsLifted(Rigidbody body, Collider collider, Vector3 up)
        {
            if (collider == null)
            {
                return false;
            }

            Bounds bounds = collider.bounds;
            float halfHeight = Mathf.Abs(Vector3.Dot(bounds.extents, up));
            float distance = halfHeight + profile.liftedClearance;
            LayerMask layers = balance != null ? balance.Profile.ground.groundLayers : (LayerMask)~0;
            int count = Physics.RaycastNonAlloc(bounds.center, -up, hits, distance, layers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!ragdoll.IsOwnOrHeld(hits[i].collider))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Lean away from a lifted load in front of the body, proportional to its moment about the pelvis.</summary>
        Vector3 CounterLean(Vector3 loadMoment)
        {
            float degrees = Mathf.Min(loadMoment.magnitude * profile.leanPerKilogramMetre, profile.maxLean);
            return degrees > 1e-3f ? -loadMoment.normalized * degrees : Vector3.zero;
        }

        void UpdateOverload()
        {
            if (!IsOverloaded || profile.overloadSlipTime <= 0f)
            {
                overloadTimer = 0f;
                return;
            }

            overloadTimer += Time.fixedDeltaTime;
            if (overloadTimer < profile.overloadSlipTime)
            {
                return;
            }

            overloadTimer = 0f;
            for (int i = 0; i < 2; i++)
            {
                var side = (BodySide)i;
                if (arms.TryGetGrab(side, out GrabInfo grab) && grab.body != null && handsPerBody.ContainsKey(grab.body))
                {
                    arms.Release(side);
                }
            }

            arms.BlockGrabs(profile.slipRegrabBlockTime);
            events.overloadSlipped.Invoke();
        }

        void ReportStrain()
        {
            if (Mathf.Abs(Strain - reportedStrain) < StrainEventThreshold && !(Strain == 0f && reportedStrain != 0f))
            {
                return;
            }

            reportedStrain = Strain;
            events.strainChanged.Invoke(Strain);
        }
    }
}
