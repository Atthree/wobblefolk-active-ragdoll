using UnityEngine;

namespace Wobblefolk.Ragdoll
{
    public struct GroundInfo
    {
        /// <summary>Walkable ground is within reach below the pelvis.</summary>
        public bool isGrounded;
        /// <summary>Pelvis height above the ground point, along the up axis.</summary>
        public float pelvisHeight;
        public Vector3 point;
        public Vector3 normal;
        public Collider collider;
        public bool leftFootContact;
        public bool rightFootContact;

        public bool AnyFootContact => leftFootContact || rightFootContact;
        public Rigidbody Body => collider != null ? collider.attachedRigidbody : null;
    }

    /// <summary>Allocation-free ground detection below the pelvis and under each foot.</summary>
    public sealed class GroundProbe
    {
        const int MaxHits = 16;

        readonly RaycastHit[] hits = new RaycastHit[MaxHits];
        readonly ActiveRagdoll ragdoll;

        public GroundProbe(ActiveRagdoll ragdoll)
        {
            this.ragdoll = ragdoll;
        }

        public GroundInfo Probe(GroundSettings settings, float standHeight, Vector3 up)
        {
            var info = new GroundInfo { normal = up };
            Vector3 pelvis = ragdoll.Pelvis.Body.position;
            Vector3 origin = pelvis + up * settings.probeStartOffset;
            float distance = settings.probeStartOffset + standHeight + settings.probeExtraDistance;
            float minNormalDot = Mathf.Cos(settings.maxGroundAngle * Mathf.Deg2Rad);

            int count = Physics.SphereCastNonAlloc(origin, settings.probeRadius, -up, hits, distance,
                settings.groundLayers, QueryTriggerInteraction.Ignore);

            if (TryNearest(count, up, minNormalDot, out RaycastHit hit))
            {
                info.isGrounded = true;
                info.point = hit.point;
                info.normal = hit.normal;
                info.collider = hit.collider;
                info.pelvisHeight = Vector3.Dot(pelvis - hit.point, up);
            }

            info.leftFootContact = FootContact(BoneId.FootLeft, settings, up, minNormalDot);
            info.rightFootContact = FootContact(BoneId.FootRight, settings, up, minNormalDot);
            return info;
        }

        bool FootContact(BoneId footId, GroundSettings settings, Vector3 up, float minNormalDot)
        {
            if (!ragdoll.TryGetBone(footId, out RagdollBone foot) || foot.MainCollider == null)
            {
                return false;
            }

            Bounds bounds = foot.MainCollider.bounds;
            float halfHeight = Mathf.Abs(Vector3.Dot(bounds.extents, up));
            int count = Physics.RaycastNonAlloc(bounds.center, -up, hits, halfHeight + settings.footProbeDistance,
                settings.groundLayers, QueryTriggerInteraction.Ignore);
            return TryNearest(count, up, minNormalDot, out _);
        }

        bool TryNearest(int count, Vector3 up, float minNormalDot, out RaycastHit nearest)
        {
            nearest = default;
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                // distance 0 means the cast started inside this collider; the point is undefined.
                if (hit.distance <= 0f || ragdoll.IsOwnCollider(hit.collider) || Vector3.Dot(hit.normal, up) < minNormalDot)
                {
                    continue;
                }

                if (hit.distance < best)
                {
                    best = hit.distance;
                    nearest = hit;
                    found = true;
                }
            }

            return found;
        }
    }
}
