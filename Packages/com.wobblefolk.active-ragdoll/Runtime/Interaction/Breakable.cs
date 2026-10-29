using System;
using UnityEngine;
using UnityEngine.Events;

namespace Wobblefolk.Ragdoll
{
    [Serializable]
    public sealed class BreakEvent : UnityEvent<Vector3, float> { }

    /// <summary>
    /// Raises an event when hit hard enough and optionally swaps itself for a fractured version. Impacts are measured
    /// as the approach speed along the contact normal; weak hits below the threshold can wear it down instead.
    /// Fracturing itself is left to the <see cref="brokenPrefab"/> or event listeners.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Breakable : MonoBehaviour
    {
        [Tooltip("Impact speed along the contact normal that breaks the object outright (m/s).")]
        [Min(0f)] public float breakSpeed = 6f;
        [Tooltip("Impacts slower than this are ignored entirely (m/s).")]
        [Min(0f)] public float minDamageSpeed = 2f;
        [Tooltip("Hits between the two speeds deal damage proportional to their speed. Breaks when used up. 0 = only " +
                 "breaks on a single hit above the break speed.")]
        [Min(0f)] public float durability = 0f;
        [Tooltip("Optional replacement spawned at this pose, inheriting velocity (e.g. pre-fractured pieces).")]
        public GameObject brokenPrefab;
        public bool destroyOnBreak = true;

        [Tooltip("(contact point, impact speed)")]
        public BreakEvent broken = new BreakEvent();

        float damage;
        bool isBroken;

        public bool IsBroken => isBroken;

        void OnCollisionEnter(Collision collision)
        {
            if (isBroken)
            {
                return;
            }

            ContactPoint contact = collision.contactCount > 0 ? collision.GetContact(0) : default;
            float speed = collision.contactCount > 0
                ? Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal))
                : collision.relativeVelocity.magnitude;

            if (speed < minDamageSpeed)
            {
                return;
            }

            if (speed >= breakSpeed)
            {
                Break(contact.point, speed);
                return;
            }

            if (durability > 0f)
            {
                damage += speed - minDamageSpeed;
                if (damage >= durability)
                {
                    Break(contact.point, speed);
                }
            }
        }

        /// <summary>Breaks the object now, e.g. from an explosion or script.</summary>
        public void Break(Vector3 point, float speed)
        {
            if (isBroken)
            {
                return;
            }

            isBroken = true;
            if (brokenPrefab != null)
            {
                GameObject pieces = Instantiate(brokenPrefab, transform.position, transform.rotation);
                pieces.transform.localScale = transform.lossyScale;
                if (TryGetComponent(out Rigidbody source))
                {
                    foreach (Rigidbody piece in pieces.GetComponentsInChildren<Rigidbody>())
                    {
                        piece.linearVelocity = source.GetPointVelocity(piece.worldCenterOfMass);
                        piece.angularVelocity = source.angularVelocity;
                    }
                }
            }

            broken.Invoke(point, speed);
            if (destroyOnBreak)
            {
                Destroy(gameObject);
            }
        }
    }
}
