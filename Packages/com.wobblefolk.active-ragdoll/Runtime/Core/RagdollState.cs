using System;
using UnityEngine.Events;

namespace Wobblefolk.Ragdoll
{
    public enum RagdollState
    {
        /// <summary>Balance and muscles fully active.</summary>
        Normal = 0,
        /// <summary>Lost balance; muscles weakened, waiting to get up.</summary>
        Fallen = 1,
        /// <summary>Recovering from a fall; balance and muscles ramp back in.</summary>
        GettingUp = 2,
        /// <summary>Knocked out; almost limp until the knockout timer expires.</summary>
        Unconscious = 3,
    }

    [Serializable]
    public sealed class RagdollStateChangedEvent : UnityEvent<RagdollState, RagdollState> { }

    [Serializable]
    public sealed class RagdollEvents
    {
        /// <summary>(previous, current)</summary>
        public RagdollStateChangedEvent stateChanged = new RagdollStateChangedEvent();
        public UnityEvent fell = new UnityEvent();
        public UnityEvent gettingUp = new UnityEvent();
        public UnityEvent recovered = new UnityEvent();
        public UnityEvent knockedOut = new UnityEvent();
        public UnityEvent wokeUp = new UnityEvent();
    }
}
