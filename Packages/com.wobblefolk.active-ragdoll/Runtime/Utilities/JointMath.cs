using UnityEngine;

namespace Wobblefolk.Ragdoll
{
    public static class JointMath
    {
        /// <summary>
        /// Rotation that maps joint space (x = axis, y = secondaryAxis) into the body's local space.
        /// </summary>
        public static Quaternion JointToLocal(Vector3 axis, Vector3 secondaryAxis)
        {
            Vector3 right = axis.normalized;
            Vector3 forward = Vector3.Cross(right, secondaryAxis).normalized;
            Vector3 up = Vector3.Cross(forward, right).normalized;
            return Quaternion.LookRotation(forward, up);
        }

        /// <summary>Shortest-arc angle of a rotation in radians, with its axis, in the range [0, PI].</summary>
        public static void ToAngleAxisRadians(Quaternion rotation, out float angle, out Vector3 axis)
        {
            if (rotation.w < 0f)
            {
                rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
            }

            rotation.ToAngleAxis(out float degrees, out axis);
            if (float.IsNaN(axis.x) || float.IsInfinity(axis.x))
            {
                axis = Vector3.zero;
                angle = 0f;
                return;
            }

            angle = degrees * Mathf.Deg2Rad;
        }

        public static Vector3 ClampMagnitude(Vector3 value, float max)
        {
            return max > 0f ? Vector3.ClampMagnitude(value, max) : value;
        }
    }
}
