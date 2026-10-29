using System.IO;
using UnityEditor;
using UnityEngine;

namespace Wobblefolk.Ragdoll.Editor
{
    static class PrimitiveRagdollMenu
    {
        const string ProfileFolder = "Assets/ActiveRagdoll/Profiles";

        [MenuItem("GameObject/Wobblefolk/Primitive Active Ragdoll", false, 10)]
        static void CreatePrimitiveRagdoll(MenuCommand command)
        {
            ActiveRagdoll ragdoll = Create(command);
            Selection.activeGameObject = ragdoll.gameObject;
        }

        [MenuItem("GameObject/Wobblefolk/Primitive Active Ragdoll (Player)", false, 11)]
        static void CreatePlayerRagdoll(MenuCommand command)
        {
            ActiveRagdoll ragdoll = Create(command);
            AddPlayerControls(ragdoll);
            Selection.activeGameObject = ragdoll.gameObject;
        }

        /// <summary>Adds player input, a follow camera on the main camera and scene physics settings.</summary>
        internal static void AddPlayerControls(ActiveRagdoll ragdoll)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
                camera = cameraObject.AddComponent<Camera>();
                Undo.RegisterCreatedObjectUndo(cameraObject, "Create Camera");
            }

            RagdollFollowCamera follow = camera.GetComponent<RagdollFollowCamera>();
            if (follow == null) follow = Undo.AddComponent<RagdollFollowCamera>(camera.gameObject);
            Undo.RecordObject(follow, "Assign Follow Target");
            follow.Target = ragdoll;

            var input = Undo.AddComponent<RagdollPlayerInput>(ragdoll.gameObject);
            input.FollowCamera = follow;

            if (Object.FindAnyObjectByType<RagdollPhysicsSettings>() == null)
            {
                var settings = new GameObject("Ragdoll Physics Settings", typeof(RagdollPhysicsSettings));
                Undo.RegisterCreatedObjectUndo(settings, "Create Ragdoll Physics Settings");
            }
        }

        static ActiveRagdoll Create(MenuCommand command)
        {
            ActiveRagdoll ragdoll = PrimitiveRagdollBuilder.Build(new PrimitiveBodyShape(), FindOrCreateProfiles(),
                SpawnPosition(), Quaternion.identity);
            ragdoll.gameObject.AddComponent<RagdollDebugTools>();

            if (command.context is GameObject parent)
            {
                GameObjectUtility.SetParentAndAlign(ragdoll.gameObject, parent);
            }

            Undo.RegisterCreatedObjectUndo(ragdoll.gameObject, "Create Primitive Active Ragdoll");
            return ragdoll;
        }

        static Vector3 SpawnPosition()
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null)
            {
                return Vector3.zero;
            }

            Vector3 pivot = view.pivot;
            return Physics.Raycast(pivot + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point
                : pivot;
        }

        internal static RagdollProfileSet FindOrCreateProfiles()
        {
            return new RagdollProfileSet
            {
                ragdoll = FindOrCreateAsset<RagdollProfile>("RagdollProfile"),
                balance = FindOrCreateAsset<BalanceProfile>("BalanceProfile"),
                locomotion = FindOrCreateAsset<LocomotionProfile>("LocomotionProfile"),
                arms = FindOrCreateAsset<ArmProfile>("ArmProfile"),
                carry = FindOrCreateAsset<CarryProfile>("CarryProfile"),
                climb = FindOrCreateAsset<ClimbProfile>("ClimbProfile"),
            };
        }

        internal static T FindOrCreateAsset<T>(string fileName) where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            if (guids.Length > 0)
            {
                return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            Directory.CreateDirectory(ProfileFolder);
            var asset = ScriptableObject.CreateInstance<T>();
            string path = AssetDatabase.GenerateUniqueAssetPath($"{ProfileFolder}/{fileName}.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"Created default {typeof(T).Name} at {path}", asset);
            return asset;
        }
    }
}
