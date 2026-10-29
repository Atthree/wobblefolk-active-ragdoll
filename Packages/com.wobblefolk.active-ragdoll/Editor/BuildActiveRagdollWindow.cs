using UnityEditor;
using UnityEngine;

namespace Wobblefolk.Ragdoll.Editor
{
    /// <summary>
    /// Tools > Wobblefolk > Build Active Ragdoll. Drop a Humanoid model from the scene, pick profiles and build.
    /// </summary>
    sealed class BuildActiveRagdollWindow : EditorWindow
    {
        const string StandHeightToleranceTip = "When the model's pelvis height differs from the balance profile's stand " +
                                               "height by more than this, a copy of the profile is made for this model (m).";

        [SerializeField] Animator model;
        [SerializeField] RagdollProfileSet profiles = new RagdollProfileSet();
        [SerializeField] HumanoidRagdollSettings settings = new HumanoidRagdollSettings();
        [SerializeField] bool addPlayerControls = true;
        [SerializeField] bool addDebugTools = true;
        [SerializeField] float standHeightTolerance = 0.05f;

        SerializedObject serialized;
        Vector2 scroll;

        [MenuItem("Tools/Wobblefolk/Build Active Ragdoll", false, 0)]
        static void Open()
        {
            var window = GetWindow<BuildActiveRagdollWindow>("Build Active Ragdoll");
            window.minSize = new Vector2(360f, 420f);
            if (Selection.activeGameObject != null)
            {
                window.model = Selection.activeGameObject.GetComponentInChildren<Animator>();
            }
        }

        void OnEnable()
        {
            if (profiles.ragdoll == null) profiles = PrimitiveRagdollMenu.FindOrCreateProfiles();
            serialized = new SerializedObject(this);
        }

        void OnGUI()
        {
            serialized.Update();
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.HelpBox("Drag a model with a Humanoid avatar from the scene. Its bones get rigidbodies, " +
                                    "colliders and joints, and the root gets the active ragdoll controllers. The " +
                                    "Animator is disabled.", MessageType.None);

            EditorGUILayout.PropertyField(serialized.FindProperty(nameof(model)));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serialized.FindProperty(nameof(profiles)), true);
            EditorGUILayout.PropertyField(serialized.FindProperty(nameof(settings)), true);
            EditorGUILayout.PropertyField(serialized.FindProperty(nameof(addPlayerControls)),
                new GUIContent("Player Controls", "Add input, a follow camera on the main camera and physics settings."));
            EditorGUILayout.PropertyField(serialized.FindProperty(nameof(addDebugTools)));
            EditorGUILayout.PropertyField(serialized.FindProperty(nameof(standHeightTolerance)),
                new GUIContent("Stand Height Tolerance", StandHeightToleranceTip));
            serialized.ApplyModifiedProperties();

            EditorGUILayout.Space();
            string error = model != null && model.GetComponent<ActiveRagdoll>() != null ? null : HumanoidRagdollBuilder.Validate(model);
            if (profiles.ragdoll == null || profiles.balance == null)
            {
                error ??= "Assign at least the Ragdoll and Balance profiles.";
            }

            bool hasRagdoll = model != null && model.GetComponent<ActiveRagdoll>() != null;
            if (hasRagdoll)
            {
                EditorGUILayout.HelpBox("This model already has an Active Ragdoll.", MessageType.Info);
                if (GUILayout.Button("Remove Active Ragdoll"))
                {
                    RemoveActiveRagdoll(model.gameObject);
                }
            }
            else
            {
                if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
                using (new EditorGUI.DisabledScope(error != null))
                {
                    if (GUILayout.Button("Build Active Ragdoll", GUILayout.Height(32f)))
                    {
                        Build();
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        void Build()
        {
            if (PrefabUtility.IsPartOfPrefabAsset(model))
            {
                EditorUtility.DisplayDialog("Build Active Ragdoll", "Drag an instance from the scene, not the prefab asset.", "OK");
                return;
            }

            GameObject root = model.gameObject;
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build Active Ragdoll");
            Undo.RegisterFullObjectHierarchyUndo(root, "Build Active Ragdoll");

            RagdollProfileSet set = WithFittedBalanceProfile(profiles, HumanoidRagdollBuilder.MeasureStandHeight(model), root.name);
            ActiveRagdoll ragdoll = HumanoidRagdollBuilder.Build(model, set, settings);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == HumanoidRagdollBuilder.ColliderObjectName) Undo.RegisterCreatedObjectUndo(t.gameObject, "Build Active Ragdoll");
            }

            if (addDebugTools) Undo.AddComponent<RagdollDebugTools>(root);
            if (addPlayerControls) PrimitiveRagdollMenu.AddPlayerControls(ragdoll);

            Undo.CollapseUndoOperations(undoGroup);
            EditorUtility.SetDirty(root);
            Selection.activeGameObject = root;
            Debug.Log($"Built active ragdoll on '{root.name}'.", root);
        }

        /// <summary>Copies the balance profile when its stand height does not fit this model.</summary>
        RagdollProfileSet WithFittedBalanceProfile(RagdollProfileSet source, float standHeight, string modelName)
        {
            float target = standHeight - source.balance.height.standHeight;
            if (Mathf.Abs(target) <= standHeightTolerance)
            {
                return source;
            }

            BalanceProfile copy = Instantiate(source.balance);
            copy.height.standHeight = standHeight;
            string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(source.balance))?.Replace('\\', '/');
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{modelName}_BalanceProfile.asset");
            AssetDatabase.CreateAsset(copy, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"Stand height of '{modelName}' is {standHeight:F2} m; created {path}.", copy);

            return new RagdollProfileSet
            {
                ragdoll = source.ragdoll,
                balance = copy,
                locomotion = source.locomotion,
                arms = source.arms,
                carry = source.carry,
                climb = source.climb,
            };
        }

        static void RemoveActiveRagdoll(GameObject root)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Remove Active Ragdoll");
            Undo.RegisterFullObjectHierarchyUndo(root, "Remove Active Ragdoll");
            DestroyAll<RagdollPlayerInput>(root);
            DestroyAll<RagdollDebugTools>(root);
            DestroyAll<CarryController>(root);
            DestroyAll<ClimbController>(root);
            DestroyAll<ArmController>(root);
            DestroyAll<LocomotionController>(root);
            DestroyAll<BalanceController>(root);
            DestroyAll<ActiveRagdoll>(root);
            DestroyAll<RagdollBone>(root);
            DestroyAll<ConfigurableJoint>(root);
            DestroyAll<Rigidbody>(root);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.name == HumanoidRagdollBuilder.ColliderObjectName) Undo.DestroyObjectImmediate(t.gameObject);
            }

            if (root.TryGetComponent(out Animator animator))
            {
                Undo.RecordObject(animator, "Remove Active Ragdoll");
                animator.enabled = true;
            }

            Undo.CollapseUndoOperations(undoGroup);
        }

        static void DestroyAll<T>(GameObject root) where T : Component
        {
            foreach (T component in root.GetComponentsInChildren<T>(true))
            {
                Undo.DestroyObjectImmediate(component);
            }
        }
    }
}
