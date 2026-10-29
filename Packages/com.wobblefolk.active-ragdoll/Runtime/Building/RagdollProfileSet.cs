using System;
using UnityEngine;

namespace Wobblefolk.Ragdoll
{
    /// <summary>
    /// All profiles of one character. Only the ragdoll and balance profiles are required; a missing optional profile
    /// leaves its controller out.
    /// </summary>
    [Serializable]
    public sealed class RagdollProfileSet
    {
        public RagdollProfile ragdoll;
        public BalanceProfile balance;
        public LocomotionProfile locomotion;
        public ArmProfile arms;
        public CarryProfile carry;
        public ClimbProfile climb;
    }

    public static class RagdollSetup
    {
        /// <summary>
        /// Adds and wires the behaviour controllers to a configured ragdoll root. Returns the balance controller.
        /// </summary>
        public static BalanceController AddControllers(ActiveRagdoll ragdoll, RagdollProfileSet profiles)
        {
            GameObject root = ragdoll.gameObject;
            var balance = root.AddComponent<BalanceController>();
            balance.Configure(ragdoll, profiles.balance);

            LocomotionController locomotion = null;
            if (profiles.locomotion != null)
            {
                locomotion = root.AddComponent<LocomotionController>();
                locomotion.Configure(ragdoll, balance, profiles.locomotion);
            }

            if (profiles.arms == null)
            {
                return balance;
            }

            var arms = root.AddComponent<ArmController>();
            arms.Configure(ragdoll, balance, locomotion, profiles.arms);

            ClimbController climb = null;
            if (profiles.climb != null)
            {
                climb = root.AddComponent<ClimbController>();
                climb.Configure(ragdoll, balance, locomotion, arms, profiles.climb);
            }

            if (profiles.carry != null)
            {
                root.AddComponent<CarryController>().Configure(ragdoll, balance, locomotion, arms, climb, profiles.carry);
            }

            return balance;
        }
    }
}
