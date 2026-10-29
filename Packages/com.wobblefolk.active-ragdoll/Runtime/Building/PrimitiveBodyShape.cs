using System;
using UnityEngine;

namespace Wobblefolk.Ragdoll
{
    /// <summary>Proportions of the primitive (box / capsule / sphere) sample character, in metres.</summary>
    [Serializable]
    public sealed class PrimitiveBodyShape
    {
        [Header("Legs")]
        public Vector3 footSize = new Vector3(0.11f, 0.07f, 0.25f);
        [Tooltip("How far the foot box extends in front of the ankle.")]
        public float footForwardOffset = 0.06f;
        public float lowerLegLength = 0.42f;
        public float lowerLegRadius = 0.06f;
        public float upperLegLength = 0.44f;
        public float upperLegRadius = 0.075f;
        [Tooltip("Distance between the two hip joints.")]
        public float hipSpacing = 0.2f;

        [Header("Torso")]
        public Vector3 pelvisSize = new Vector3(0.32f, 0.2f, 0.2f);
        [Tooltip("Hip joints sit this fraction of the pelvis height below the pelvis centre.")]
        [Range(0f, 0.5f)] public float hipDepth = 0.25f;
        public Vector3 chestSize = new Vector3(0.38f, 0.42f, 0.22f);

        [Header("Head")]
        public float neckLength = 0.06f;
        public float headRadius = 0.12f;

        [Header("Arms")]
        [Tooltip("Distance between the two shoulder joints.")]
        public float shoulderSpacing = 0.5f;
        [Tooltip("Shoulder joints sit this far below the top of the chest.")]
        public float shoulderDrop = 0.06f;
        public float upperArmLength = 0.28f;
        public float upperArmRadius = 0.05f;
        public float lowerArmLength = 0.26f;
        public float lowerArmRadius = 0.045f;
        public float handRadius = 0.05f;

        public float AnkleHeight => footSize.y;
        public float KneeHeight => AnkleHeight + lowerLegLength;
        public float HipHeight => KneeHeight + upperLegLength;
        public float PelvisHeight => HipHeight + pelvisSize.y * hipDepth;
        public float WaistHeight => PelvisHeight + pelvisSize.y * 0.5f;
        public float NeckHeight => WaistHeight + chestSize.y;
        public float ShoulderHeight => NeckHeight - shoulderDrop;
    }
}
