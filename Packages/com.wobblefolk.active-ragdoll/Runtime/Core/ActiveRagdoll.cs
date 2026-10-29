using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wobblefolk.Ragdoll
{
    /// <summary>
    /// Root of an active ragdoll. Owns the bones, applies the <see cref="RagdollProfile"/>, holds the character state
    /// and raises state events. Behaviour (balance, locomotion, arms) lives in separate components that use this API.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class ActiveRagdoll : MonoBehaviour
    {
        [SerializeField] RagdollProfile profile;
        [SerializeField] RagdollBone[] bones = Array.Empty<RagdollBone>();
        [SerializeField] RagdollEvents events = new RagdollEvents();

        readonly RagdollBone[] lookup = new RagdollBone[BoneIds.Count];
        readonly HashSet<Rigidbody> ownBodies = new HashSet<Rigidbody>();
        readonly Dictionary<Rigidbody, int> heldBodies = new Dictionary<Rigidbody, int>();
        float muscleStrength = 1f;
        bool initialized;

        public RagdollProfile Profile => profile;
        public RagdollEvents Events => events;
        public IReadOnlyList<RagdollBone> Bones => bones;
        public RagdollState State { get; private set; } = RagdollState.Normal;
        public float StateTime => Time.time - StateEnterTime;
        public float StateEnterTime { get; private set; }
        public float TotalMass { get; private set; }
        public float MuscleStrength => muscleStrength;

        public RagdollBone Pelvis => lookup[(int)BoneId.Pelvis];
        public RagdollBone Chest => lookup[(int)BoneId.Chest];
        public RagdollBone Head => lookup[(int)BoneId.Head];

        /// <summary>Raised for every collision a bone receives from a collider outside this ragdoll.</summary>
        public event Action<RagdollBone, Collision> BoneCollided;

        /// <summary>C# counterpart of <see cref="RagdollEvents.stateChanged"/>: (previous, current).</summary>
        public event Action<RagdollState, RagdollState> StateChanged;

        public RagdollBone GetBone(BoneId id) => lookup[(int)id];

        public bool TryGetBone(BoneId id, out RagdollBone bone)
        {
            bone = lookup[(int)id];
            return bone != null;
        }

        public bool IsOwnBody(Rigidbody body) => body != null && ownBodies.Contains(body);

        public bool IsOwnCollider(Collider other) => other != null && IsOwnBody(other.attachedRigidbody);

        /// <summary>True while any hand of this ragdoll holds the body.</summary>
        public bool IsHeld(Rigidbody body) => body != null && heldBodies.ContainsKey(body);

        /// <summary>
        /// True for this ragdoll's own colliders and for objects it holds. Sensors (ground, steps, aim, camera) must
        /// ignore held objects, otherwise the character can stand on what it carries and lift itself into the sky.
        /// </summary>
        public bool IsOwnOrHeld(Collider other)
        {
            Rigidbody body = other != null ? other.attachedRigidbody : null;
            return body != null && (ownBodies.Contains(body) || heldBodies.ContainsKey(body));
        }

        /// <summary>Registers a held object. Calls are counted, so two hands on one object need two releases.</summary>
        public void RegisterHeld(Rigidbody body)
        {
            if (body == null) return;
            heldBodies.TryGetValue(body, out int count);
            heldBodies[body] = count + 1;
        }

        public void UnregisterHeld(Rigidbody body)
        {
            if (body == null || !heldBodies.TryGetValue(body, out int count)) return;
            if (count <= 1) heldBodies.Remove(body);
            else heldBodies[body] = count - 1;
        }

        public Vector3 CenterOfMass
        {
            get
            {
                Vector3 sum = Vector3.zero;
                float mass = 0f;
                foreach (RagdollBone bone in bones)
                {
                    sum += bone.Body.worldCenterOfMass * bone.Body.mass;
                    mass += bone.Body.mass;
                }

                return mass > 0f ? sum / mass : transform.position;
            }
        }

        public Vector3 AverageVelocity
        {
            get
            {
                Vector3 sum = Vector3.zero;
                float mass = 0f;
                foreach (RagdollBone bone in bones)
                {
                    sum += bone.Body.linearVelocity * bone.Body.mass;
                    mass += bone.Body.mass;
                }

                return mass > 0f ? sum / mass : Vector3.zero;
            }
        }

        internal void Configure(RagdollProfile ragdollProfile, RagdollBone[] ragdollBones)
        {
            profile = ragdollProfile;
            bones = ragdollBones;
            initialized = false;
            Initialize();
            if (Application.isPlaying)
            {
                IgnoreSelfCollisions();
            }
        }

        void Awake()
        {
            StateEnterTime = Time.time;
            // A ragdoll built at runtime is added empty and configured right after; Configure initializes it then.
            if (bones.Length == 0)
            {
                return;
            }

            Initialize();
            IgnoreSelfCollisions();
        }

        void Initialize()
        {
            if (initialized)
            {
                return;
            }

            Array.Clear(lookup, 0, lookup.Length);
            ownBodies.Clear();
            foreach (RagdollBone bone in bones)
            {
                if (bone == null)
                {
                    continue;
                }

                lookup[(int)bone.Id] = bone;
                bone.Initialize(this);
                ownBodies.Add(bone.Body);
            }

            if (Pelvis == null)
            {
                Debug.LogError($"{nameof(ActiveRagdoll)} on '{name}' has no pelvis bone.", this);
            }

            initialized = true;
            ApplyProfile();
        }

        /// <summary>Re-applies the profile to every bone. Call after editing the profile at runtime.</summary>
        public void ApplyProfile()
        {
            if (profile == null)
            {
                Debug.LogError($"{nameof(ActiveRagdoll)} on '{name}' has no {nameof(RagdollProfile)}.", this);
                return;
            }

            TotalMass = 0f;
            foreach (RagdollBone bone in bones)
            {
                if (bone == null) continue;
                bone.ApplySettings(profile.GetBone(bone.Id), profile.physics, profile.muscleStrength);
                TotalMass += bone.Body.mass;
            }

            float strength = muscleStrength;
            muscleStrength = -1f;
            SetMuscleStrength(strength);
        }

        void IgnoreSelfCollisions()
        {
            if (profile == null || !profile.physics.ignoreSelfCollisions)
            {
                return;
            }

            var colliders = new List<Collider>();
            foreach (RagdollBone bone in bones)
            {
                if (bone != null) colliders.AddRange(bone.GetComponentsInChildren<Collider>());
            }

            for (int i = 0; i < colliders.Count; i++)
            {
                for (int j = i + 1; j < colliders.Count; j++)
                {
                    Physics.IgnoreCollision(colliders[i], colliders[j], true);
                }
            }
        }

        /// <summary>Scales all muscles relative to their profile strength.</summary>
        public void SetMuscleStrength(float strength)
        {
            if (Mathf.Approximately(strength, muscleStrength))
            {
                return;
            }

            muscleStrength = strength;
            foreach (RagdollBone bone in bones)
            {
                if (bone != null) bone.SetMuscleStrength(strength);
            }
        }

        public void SetState(RagdollState next)
        {
            if (next == State)
            {
                return;
            }

            RagdollState previous = State;
            State = next;
            StateEnterTime = Time.time;

            events.stateChanged.Invoke(previous, next);
            StateChanged?.Invoke(previous, next);

            switch (next)
            {
                case RagdollState.Fallen:
                    if (previous == RagdollState.Unconscious) events.wokeUp.Invoke();
                    else events.fell.Invoke();
                    break;
                case RagdollState.GettingUp:
                    events.gettingUp.Invoke();
                    break;
                case RagdollState.Normal:
                    events.recovered.Invoke();
                    break;
                case RagdollState.Unconscious:
                    events.knockedOut.Invoke();
                    break;
            }
        }

        internal void ReportCollision(RagdollBone bone, Collision collision)
        {
            if (IsOwnOrHeld(collision.collider))
            {
                return;
            }

            BoneCollided?.Invoke(bone, collision);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying && initialized)
            {
                ApplyProfile();
            }
        }
#endif
    }
}
