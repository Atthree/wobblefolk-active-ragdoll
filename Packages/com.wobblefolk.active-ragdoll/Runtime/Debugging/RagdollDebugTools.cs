using UnityEngine;

namespace Wobblefolk.Ragdoll
{
    /// <summary>
    /// Play-mode test helpers, callable from the component's context menu (⋮) in the Inspector.
    /// </summary>
    [RequireComponent(typeof(ActiveRagdoll))]
    public sealed class RagdollDebugTools : MonoBehaviour
    {
        [Tooltip("Velocity change applied to the chest by 'Push' (m/s).")]
        [Min(0f)] public float pushSpeed = 3f;
        [Tooltip("Knockout length used by 'Knock Out' (s).")]
        [Min(0f)] public float knockoutDuration = 3f;

        ActiveRagdoll ragdoll;
        BalanceController balance;

        void Awake()
        {
            ragdoll = GetComponent<ActiveRagdoll>();
            balance = GetComponent<BalanceController>();
        }

        [ContextMenu("Push Forward")]
        public void PushForward() => Push(transform.forward);

        [ContextMenu("Push Sideways")]
        public void PushSideways() => Push(transform.right);

        public void Push(Vector3 direction)
        {
            if (ragdoll != null && ragdoll.Chest != null)
            {
                ragdoll.Chest.Body.AddForce(direction.normalized * pushSpeed, ForceMode.VelocityChange);
            }
        }

        [ContextMenu("Fall")]
        public void Fall()
        {
            if (balance != null) balance.Fall();
        }

        [ContextMenu("Knock Out")]
        public void KnockOut()
        {
            if (balance != null) balance.Knockout(knockoutDuration);
        }
    }
}
