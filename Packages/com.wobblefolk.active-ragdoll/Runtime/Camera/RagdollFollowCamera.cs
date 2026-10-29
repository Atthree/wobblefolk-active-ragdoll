using UnityEngine;

namespace Wobblefolk.Ragdoll
{
    /// <summary>
    /// Third-person orbit camera that follows a ragdoll's chest with smoothing and wall avoidance. Look input is fed by
    /// a driver through <see cref="AddLookInput"/>. <see cref="PlanarForward"/> / <see cref="PlanarRight"/> give the
    /// basis for camera-relative movement and <see cref="AimRay"/> the aim direction for reaching arms.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class RagdollFollowCamera : MonoBehaviour
    {
        [SerializeField] ActiveRagdoll target;

        [Header("Orbit")]
        [Min(0f)] public float distance = 4f;
        [Tooltip("Pivot offset above the chest (m).")]
        public float pivotHeight = 0.35f;
        [Tooltip("Sideways pivot offset for an over-the-shoulder view (m).")]
        public float shoulderOffset = 0f;
        [Range(-89f, 0f)] public float minPitch = -40f;
        [Range(0f, 89f)] public float maxPitch = 70f;
        public float initialPitch = 15f;

        [Header("Input")]
        [Tooltip("Degrees per unit of look input.")]
        [Min(0f)] public float sensitivity = 0.12f;
        public bool invertY;

        [Header("Smoothing")]
        [Tooltip("How fast the pivot follows the target (1/s). Hides ragdoll wobble.")]
        [Min(0f)] public float followSharpness = 12f;

        [Header("Collision")]
        public LayerMask obstacleLayers = ~0;
        [Min(0f)] public float collisionRadius = 0.2f;
        [Tooltip("Minimum distance kept from the pivot when pushed in by walls (m).")]
        [Min(0f)] public float minDistance = 0.6f;

        readonly RaycastHit[] hits = new RaycastHit[8];
        Vector3 pivot;
        float yaw;
        float pitch;
        bool hasPivot;

        public ActiveRagdoll Target
        {
            get => target;
            set
            {
                target = value;
                hasPivot = false;
            }
        }

        public float Yaw => yaw;
        public float Pitch => pitch;
        public Quaternion Orientation => Quaternion.Euler(pitch, yaw, 0f);
        public Vector3 PlanarForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        public Vector3 PlanarRight => Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
        public Ray AimRay => new Ray(transform.position, transform.forward);

        void Start()
        {
            yaw = transform.eulerAngles.y;
            pitch = initialPitch;
            if (target != null)
            {
                yaw = target.transform.eulerAngles.y;
            }
        }

        public void AddLookInput(Vector2 delta)
        {
            yaw += delta.x * sensitivity;
            pitch = Mathf.Clamp(pitch + delta.y * sensitivity * (invertY ? 1f : -1f), minPitch, maxPitch);
        }

        /// <summary>Converts a 2D move input into a world direction relative to the camera's yaw.</summary>
        public Vector3 ToWorldDirection(Vector2 input) => PlanarForward * input.y + PlanarRight * input.x;

        void LateUpdate()
        {
            if (target == null || target.Chest == null)
            {
                return;
            }

            Vector3 desiredPivot = target.Chest.transform.position + Vector3.up * pivotHeight;
            pivot = hasPivot
                ? Vector3.Lerp(pivot, desiredPivot, 1f - Mathf.Exp(-followSharpness * Time.deltaTime))
                : desiredPivot;
            hasPivot = true;

            Quaternion rotation = Orientation;
            Vector3 origin = pivot + rotation * Vector3.right * shoulderOffset;
            Vector3 back = rotation * Vector3.back;
            float allowed = distance;

            int count = Physics.SphereCastNonAlloc(origin, collisionRadius, back, hits, distance, obstacleLayers,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (hits[i].distance > 0f && !target.IsOwnCollider(hits[i].collider))
                {
                    allowed = Mathf.Min(allowed, Mathf.Max(minDistance, hits[i].distance));
                }
            }

            transform.SetPositionAndRotation(origin + back * allowed, rotation);
        }
    }
}
