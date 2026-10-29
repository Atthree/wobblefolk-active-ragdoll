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
            RagdollProfile ragdollProfile = FindOrCreateAsset<RagdollProfile>("RagdollProfile");
            BalanceProfile balanceProfile = FindOrCreateAsset<BalanceProfile>("BalanceProfile");

            Vector3 position = SpawnPosition();
            ActiveRagdoll ragdoll = PrimitiveRagdollBuilder.Build(new PrimitiveBodyShape(), ragdollProfile, balanceProfile,
                position, Quaternion.identity);
            ragdoll.gameObject.AddComponent<RagdollDebugTools>();

            if (command.context is GameObject parent)
            {
                GameObjectUtility.SetParentAndAlign(ragdoll.gameObject, parent);
            }

            Undo.RegisterCreatedObjectUndo(ragdoll.gameObject, "Create Primitive Active Ragdoll");
            Selection.activeGameObject = ragdoll.gameObject;
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
