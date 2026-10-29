using UnityEngine;

namespace Wobblefolk.Ragdoll
{
    /// <summary>
    /// Scene-level physics settings recommended for active ragdolls. Applied once on Awake so a scene does not depend on
    /// the project's Time / Physics settings. Remove it if those are configured in Project Settings instead.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class RagdollPhysicsSettings : MonoBehaviour
    {
        [Tooltip("Physics step (s). 0.01 (100 Hz) greatly reduces joint jitter compared to the default 0.02.")]
        [Min(0.001f)] public float fixedTimestep = 0.01f;
        [Tooltip("Caps catch-up steps after a frame hitch to avoid a spiral of death (s).")]
        [Min(0.01f)] public float maximumAllowedTimestep = 0.05f;
        [Range(1, 64)] public int defaultSolverIterations = 12;
        [Range(1, 64)] public int defaultSolverVelocityIterations = 4;
        [Min(1f)] public float defaultMaxAngularSpeed = 30f;

        void Awake()
        {
            Time.fixedDeltaTime = fixedTimestep;
            Time.maximumDeltaTime = maximumAllowedTimestep;
            Physics.defaultSolverIterations = defaultSolverIterations;
            Physics.defaultSolverVelocityIterations = defaultSolverVelocityIterations;
            Physics.defaultMaxAngularSpeed = defaultMaxAngularSpeed;
        }
    }
}
