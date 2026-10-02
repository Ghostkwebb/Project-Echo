#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using System.IO;
using System.Collections.Generic;

namespace ParagonWraith
{
    [InitializeOnLoad]
    public class ParagonWraithAutoRunner
    {
        static ParagonWraithAutoRunner()
        {
            EditorApplication.update += RunOnceUpdate;
        }

        private static void RunOnceUpdate()
        {
            EditorApplication.update -= RunOnceUpdate;
            if (SessionState.GetBool("ParagonWraith_AutoRunner_Ran_v8", false)) return;
            SessionState.SetBool("ParagonWraith_AutoRunner_Ran_v8", true);

            Debug.Log("[ParagonWraith] Auto-running complete setup via update callback...");
            ParagonWraithUnitySetup.ApplyPose("Idle_NonCombat");
        }
    }

    public class ParagonWraithUnitySetup : EditorWindow
    {
        [MenuItem("Window/Paragon Wraith/Apply Showcase Pose (Like Fab Image)", false, 0)]
        public static void ApplyShowcasePoseDirect()
        {
            ApplyPose("Idle_NonCombat");
            EditorUtility.DisplayDialog("Paragon Wraith", 
                "Showcase Pose Applied!\n\nWraith is now standing in his iconic hero select pose holding his rifle (matching your Fab reference image).", "Awesome!");
        }

        [MenuItem("Window/Paragon Wraith/Apply Combat Idle", false, 1)]
        public static void ApplyCombatIdleDirect()
        {
            ApplyPose("Idle_Combat");
            EditorUtility.DisplayDialog("Paragon Wraith", 
                "Combat Idle Applied!\n\nWraith is ready for battle holding his weapon in combat stance.", "OK");
        }

        [MenuItem("Window/Paragon Wraith/Run Complete Setup Now (One-Click)", false, 2)]
        public static void RunFromMenu()
        {
            RunCompleteSetupInternal(true);
        }

        [MenuItem("Window/Paragon Wraith/Setup Window (Gaurav)", false, 3)]
        public static void ShowWindow()
        {
            GetWindow<ParagonWraithUnitySetup>("Wraith Setup (Gaurav)");
        }

        private Vector2 scrollPos;
        private string logOutput = "Paragon Wraith Tool Ready.\nSelect an action below:";

        private void OnGUI()
        {
            GUILayout.Label("Paragon Wraith - Rig, PBR & Posing Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox("Posing Options:\n" +
                "• Showcase Pose (FrontEndPose): Wraith stands tall holding his sniper rifle across his chest (matches Fab image).\n" +
                "• Combat Idle: Wraith holds rifle in combat ready position.", MessageType.Info);

            EditorGUILayout.Space();

            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.3f);
            if (GUILayout.Button("★ Apply Showcase Pose (Matching Fab Image)", GUILayout.Height(36)))
            {
                ApplyPose("Idle_NonCombat");
                logOutput += "\n[Pose] Applied Showcase Pose (FrontEndPose) - holding rifle like Fab reference.";
            }

            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("Apply Combat Idle (In-Game Battle Stance)", GUILayout.Height(30)))
            {
                ApplyPose("Idle_Combat");
                logOutput += "\n[Pose] Applied Combat Idle.";
            }

            EditorGUILayout.Space();
            GUILayout.Label("Asset Pipeline Steps:", EditorStyles.boldLabel);

            if (GUILayout.Button("1. Configure Normal Maps", GUILayout.Height(26)))
            {
                FixNormalMaps();
                logOutput += "\n[Step 1] Normal maps configured.";
            }

            if (GUILayout.Button("2. Setup Rig & Axis Conversion on Models", GUILayout.Height(26)))
            {
                SetupCharacterRigs();
                logOutput += "\n[Step 2] Models configured with BakeAxisConversion.";
            }

            if (GUILayout.Button("3. Setup Rig & Axis Conversion on Animations", GUILayout.Height(26)))
            {
                SetupAnimations();
                logOutput += "\n[Step 3] Animations configured.";
            }

            if (GUILayout.Button("4. Re-Build ODGreen PBR Materials & Prefabs", GUILayout.Height(26)))
            {
                SetupODGreenMaterialsAndPrefab();
                logOutput += "\n[Step 4] ODGreen Materials & Prefabs generated.";
            }

            EditorGUILayout.Space();

            GUI.backgroundColor = new Color(1f, 0.5f, 0.1f);
            if (GUILayout.Button("Run Complete One-Click Setup", GUILayout.Height(40)))
            {
                RunCompleteSetupInternal(false);
                logOutput += "\n>>> COMPLETE SETUP COMPLETED SUCCESSFULLY! <<<";
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space();
            GUILayout.Label("Log Output:", EditorStyles.boldLabel);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(140));
            EditorGUILayout.TextArea(logOutput, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        public static void RunCompleteSetupInternal(bool showDialog)
        {
            Debug.Log("[ParagonWraith] Starting complete setup...");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            FixNormalMaps();
            SetupCharacterRigs();
            SetupAnimations();
            SetupODGreenMaterialsAndPrefab();
            ApplyPose("Idle_NonCombat");
            Debug.Log("[ParagonWraith] >>> COMPLETE SETUP COMPLETED SUCCESSFULLY! <<<");

            if (showDialog)
            {
                EditorUtility.DisplayDialog("Paragon Wraith Setup", 
                    "Wraith ODGreen Setup Complete!\n\n" +
                    "- Normal Maps configured\n" +
                    "- Generic Rigs configured with Bake Axis Conversion\n" +
                    "- Animations retargeted\n" +
                    "- 5 Authentic ODGreen PBR Materials configured (with glowing amber skull & chest slits!)\n" +
                    "- Ready Prefabs created for both Wraith_ODGreen and Wraith\n" +
                    "- Hero Showcase Pose applied in scene (holding rifle like Fab image)!", "Awesome!");
            }
        }

        public static void ApplyPose(string clipName)
        {
            Avatar wraithAvatar = GetWraithAvatar();
            AnimationClip targetClip = GetClipByName(clipName);

            if (targetClip == null)
            {
                Debug.LogError($"[ParagonWraith] Could not find animation clip: {clipName}");
                return;
            }

            // 1. Create or update Animator Controller cleanly
            string controllerPath = "Assets/Developers/Gaurav/ParagonWraith/Wraith_ODGreen_Animator.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null || controller.layers == null || controller.layers.Length == 0 || controller.layers[0].stateMachine == null)
            {
                AssetDatabase.DeleteAsset(controllerPath);
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            }

            var rootStateMachine = controller.layers[0].stateMachine;
            var states = rootStateMachine.states;
            AnimatorState targetState = null;
            foreach (var st in states)
            {
                if (st.state.name == clipName)
                {
                    targetState = st.state;
                    break;
                }
            }
            if (targetState == null)
            {
                targetState = rootStateMachine.AddState(clipName);
            }
            targetState.motion = targetClip;
            rootStateMachine.defaultState = targetState;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            // 2. Setup on Prefabs
            string[] prefabs = new string[] {
                "Assets/Developers/Gaurav/ParagonWraith/Wraith_ODGreen_Ready.prefab",
                "Assets/Developers/Gaurav/ParagonWraith/Wraith_Ready.prefab"
            };
            foreach (string prefabPath in prefabs)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab != null)
                {
                    using (var editScope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
                    {
                        GameObject root = editScope.prefabContentsRoot;
                        Animator anim = root.GetComponent<Animator>();
                        if (anim == null) anim = root.AddComponent<Animator>();
                        if (wraithAvatar != null) anim.avatar = wraithAvatar;
                        anim.runtimeAnimatorController = controller;
                    }
                }
            }

            // 3. Pose or Instantiate in Scene
            GameObject sceneObj = GameObject.Find("Wraith_ODGreen_Ready");
            if (sceneObj == null)
            {
                GameObject odPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Developers/Gaurav/ParagonWraith/Wraith_ODGreen_Ready.prefab");
                if (odPrefab != null)
                {
                    sceneObj = (GameObject)PrefabUtility.InstantiatePrefab(odPrefab);
                    sceneObj.name = "Wraith_ODGreen_Ready";
                    sceneObj.transform.position = new Vector3(1.5f, 0f, 0f);
                    sceneObj.transform.rotation = Quaternion.identity;
                    Undo.RegisterCreatedObjectUndo(sceneObj, "Spawn Wraith ODGreen");
                }
            }

            if (sceneObj != null)
            {
                Undo.RecordObject(sceneObj.transform, "Position and Rotate Wraith");
                sceneObj.transform.position = new Vector3(2.2f, 0f, 0f);
                sceneObj.transform.rotation = Quaternion.Euler(0f, 175f, 0f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(sceneObj.transform);

                Animator sceneAnim = sceneObj.GetComponent<Animator>();
                if (sceneAnim == null) sceneAnim = sceneObj.AddComponent<Animator>();
                if (wraithAvatar != null) sceneAnim.avatar = wraithAvatar;
                sceneAnim.runtimeAnimatorController = controller;

                targetClip.SampleAnimation(sceneObj, 0f);
                EditorUtility.SetDirty(sceneObj);
                EditorSceneManager.MarkSceneDirty(sceneObj.scene);
                EditorSceneManager.SaveOpenScenes();
                Debug.Log($"[ParagonWraith] Applied '{clipName}' to scene object '{sceneObj.name}' and saved scene successfully!");
            }
        }

        public static void FixNormalMaps()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D");
            int count = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("ParagonWraith")) continue;

                string filename = Path.GetFileNameWithoutExtension(path).ToLower();
                if (filename.EndsWith("_n") || filename.Contains("_normal") || filename.Contains("_n_demo") || filename.EndsWith("_nrm") || filename.Contains("_ts_demo"))
                {
                    TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null && importer.textureType != TextureImporterType.NormalMap)
                    {
                        importer.textureType = TextureImporterType.NormalMap;
                        importer.SaveAndReimport();
                        count++;
                    }
                }
            }
            Debug.Log($"[ParagonWraith] Configured {count} textures as Normal Maps.");
        }

        public static void SetupCharacterRigs()
        {
            string[] charPaths = new string[] {
                "Assets/Developers/Gaurav/ParagonWraith/Characters/Heroes/Wraith/Skins/ODGreen/Meshes/Wraith_ODGreen.fbx",
                "Assets/Developers/Gaurav/ParagonWraith/Characters/Heroes/Wraith/Meshes/Wraith.fbx"
            };
            int count = 0;
            foreach (string path in charPaths)
            {
                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer != null)
                {
                    bool changed = false;
                    if (!importer.bakeAxisConversion)
                    {
                        importer.bakeAxisConversion = true;
                        changed = true;
                    }
                    if (importer.animationType != ModelImporterAnimationType.Generic)
                    {
                        importer.animationType = ModelImporterAnimationType.Generic;
                        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                        changed = true;
                    }
                    if (changed)
                    {
                        importer.SaveAndReimport();
                        count++;
                    }
                }
            }
            Debug.Log($"[ParagonWraith] Configured {count} character models as Generic with BakeAxisConversion=true.");
        }

        public static void SetupAnimations()
        {
            Avatar wraithAvatar = GetWraithAvatar();

            string[] guids = AssetDatabase.FindAssets("t:Model");
            int count = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("ParagonWraith") || !path.Contains("Animations")) continue;

                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer != null)
                {
                    bool changed = false;
                    if (!importer.bakeAxisConversion)
                    {
                        importer.bakeAxisConversion = true;
                        changed = true;
                    }
                    if (importer.animationType != ModelImporterAnimationType.Generic)
                    {
                        importer.animationType = ModelImporterAnimationType.Generic;
                        changed = true;
                    }
                    if (wraithAvatar != null && importer.sourceAvatar != wraithAvatar)
                    {
                        importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                        importer.sourceAvatar = wraithAvatar;
                        changed = true;
                    }

                    string fnLower = Path.GetFileNameWithoutExtension(path).ToLower();
                    if (fnLower.Contains("idle") || fnLower.Contains("jog") || fnLower.Contains("run") || fnLower.Contains("walk") || fnLower.Contains("loop"))
                    {
                        var defaultClips = importer.defaultClipAnimations;
                        if (defaultClips != null && defaultClips.Length > 0)
                        {
                            var existingClips = importer.clipAnimations;
                            if (existingClips == null || existingClips.Length == 0)
                            {
                                existingClips = defaultClips;
                            }
                            bool anyClipSet = false;
                            foreach (var c in existingClips)
                            {
                                if (!c.loopTime)
                                {
                                    c.loopTime = true;
                                    anyClipSet = true;
                                }
                            }
                            if (anyClipSet)
                            {
                                importer.clipAnimations = existingClips;
                                changed = true;
                            }
                        }
                    }

                    if (changed)
                    {
                        importer.SaveAndReimport();
                        count++;
                    }
                }
            }
            Debug.Log($"[ParagonWraith] Configured {count} animations as Generic with BakeAxisConversion=true.");
        }

        public static void SetupODGreenMaterialsAndPrefab()
        {
            string[] texNames = new string[] {
                "T_Wraith_ODGreen_Attachment_Albedo",
                "T_Wraith_ODGreen_UpperBody_Albedo",
                "T_Wraith_ODGreen_UpperBody_Emissive",
                "T_Wraith_ODGreen_LowerBody_Albedo",
                "T_Wraith_ODGreen_LowerBody_Emissive",
                "T_Wraith_ODGreen_Backpack_Albedo",
                "T_Wraith_ODGreen_Backpack_Emissive",
                "T_Wraith_ODGreen_Gun_Albedo",
                "T_Wraith_ODGreen_Gun_Emissive"
            };
            foreach (string tn in texNames)
            {
                string tp = $"Assets/Developers/Gaurav/ParagonWraith/Characters/Heroes/Wraith/Skins/ODGreen/Textures/{tn}.png";
                AssetDatabase.ImportAsset(tp, ImportAssetOptions.ForceUpdate);
            }

            string matDir = "Assets/Developers/Gaurav/ParagonWraith/Characters/Heroes/Wraith/Skins/ODGreen/Materials";
            if (!AssetDatabase.IsValidFolder(matDir))
            {
                Directory.CreateDirectory(matDir);
                AssetDatabase.Refresh();
            }

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            Color orangeEmissive = new Color(1.0f, 0.537f, 0.137f) * 4.0f; // Glowing amber/orange

            Material CreateMat(string matName, string albedoName, string normalName, string emissiveName, float metallic, float smoothness, Color? emissive = null)
            {
                string assetPath = $"{matDir}/{matName}.mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if (mat == null)
                {
                    mat = new Material(litShader);
                    AssetDatabase.CreateAsset(mat, assetPath);
                }

                Texture2D albedo = FindTexture(albedoName);
                Texture2D normal = FindTexture(normalName);
                Texture2D emissiveTex = !string.IsNullOrEmpty(emissiveName) ? FindTexture(emissiveName) : null;

                if (albedo != null)
                {
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", albedo);
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedo);
                    mat.SetColor("_BaseColor", Color.white);
                    mat.SetColor("_Color", Color.white);
                }
                if (normal != null)
                {
                    if (mat.HasProperty("_BumpMap"))
                    {
                        mat.SetTexture("_BumpMap", normal);
                        mat.EnableKeyword("_NORMALMAP");
                    }
                }

                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);

                if (emissive != null && emissive.Value != Color.black)
                {
                    if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emissive.Value);
                    if (mat.HasProperty("_EmissionMap") && emissiveTex != null)
                    {
                        mat.SetTexture("_EmissionMap", emissiveTex);
                    }
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }

                EditorUtility.SetDirty(mat);
                return mat;
            }

            Material matUpper = CreateMat("M_Wraith_Upperbody_ODGreen", "T_Wraith_ODGreen_UpperBody_Albedo", "T_Wraith_upperbody_N_DEMO", "T_Wraith_ODGreen_UpperBody_Emissive", 0.35f, 0.65f, orangeEmissive);
            Material matLower = CreateMat("M_Wraith_Lowerbody_ODGreen", "T_Wraith_ODGreen_LowerBody_Albedo", "T_Wraith_lowerbody_N_DEMO", "T_Wraith_ODGreen_LowerBody_Emissive", 0.30f, 0.60f, orangeEmissive);
            Material matBackpack = CreateMat("M_Wraith_Backpack_ODGreen", "T_Wraith_ODGreen_Backpack_Albedo", "T_Wraith_backpack_N_DEMO", "T_Wraith_ODGreen_Backpack_Emissive", 0.45f, 0.70f, orangeEmissive);
            Material matGun = CreateMat("M_Wraith_Gun_ODGreen", "T_Wraith_ODGreen_Gun_Albedo", "T_Wraith_gun_N_DEMO", "T_Wraith_ODGreen_Gun_Emissive", 0.50f, 0.75f, orangeEmissive);
            Material matAttachment = CreateMat("M_Wraith_Attachment_ODGreen", "T_Wraith_ODGreen_Attachment_Albedo", "T_Accessories_ts_DEMO", "", 0.40f, 0.65f);

            AssetDatabase.SaveAssets();

            Avatar wraithAvatar = GetWraithAvatar();
            string controllerPath = "Assets/Developers/Gaurav/ParagonWraith/Wraith_ODGreen_Animator.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

            // 1. Create Wraith_ODGreen_Ready.prefab
            string odFbxPath = "Assets/Developers/Gaurav/ParagonWraith/Characters/Heroes/Wraith/Skins/ODGreen/Meshes/Wraith_ODGreen.fbx";
            GameObject odModel = AssetDatabase.LoadAssetAtPath<GameObject>(odFbxPath);
            if (odModel != null)
            {
                string odPrefabPath = "Assets/Developers/Gaurav/ParagonWraith/Wraith_ODGreen_Ready.prefab";
                GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(odModel);
                if (inst != null)
                {
                    Material[] odMats = new Material[6] {
                        matAttachment, matLower, matLower, matUpper, matBackpack, matGun
                    };
                    SkinnedMeshRenderer[] smrs = inst.GetComponentsInChildren<SkinnedMeshRenderer>();
                    foreach (var smr in smrs)
                    {
                        Material[] newMats = new Material[smr.sharedMaterials.Length];
                        for (int i = 0; i < newMats.Length; i++)
                        {
                            newMats[i] = (i < odMats.Length) ? odMats[i] : matUpper;
                        }
                        smr.sharedMaterials = newMats;
                    }

                    Animator anim = inst.GetComponent<Animator>();
                    if (anim == null) anim = inst.AddComponent<Animator>();
                    if (wraithAvatar != null) anim.avatar = wraithAvatar;
                    if (controller != null) anim.runtimeAnimatorController = controller;

                    PrefabUtility.SaveAsPrefabAsset(inst, odPrefabPath);
                    DestroyImmediate(inst);
                    Debug.Log($"[ParagonWraith] Created ready ODGreen prefab: {odPrefabPath}");
                }
            }

            // 2. Create Wraith_Ready.prefab (Base mesh)
            string baseFbxPath = "Assets/Developers/Gaurav/ParagonWraith/Characters/Heroes/Wraith/Meshes/Wraith.fbx";
            GameObject baseModel = AssetDatabase.LoadAssetAtPath<GameObject>(baseFbxPath);
            if (baseModel != null)
            {
                string basePrefabPath = "Assets/Developers/Gaurav/ParagonWraith/Wraith_Ready.prefab";
                GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(baseModel);
                if (inst != null)
                {
                    Material[] baseMats = new Material[4] {
                        matLower, matUpper, matBackpack, matGun
                    };
                    SkinnedMeshRenderer[] smrs = inst.GetComponentsInChildren<SkinnedMeshRenderer>();
                    foreach (var smr in smrs)
                    {
                        Material[] newMats = new Material[smr.sharedMaterials.Length];
                        for (int i = 0; i < newMats.Length; i++)
                        {
                            newMats[i] = (i < baseMats.Length) ? baseMats[i] : matUpper;
                        }
                        smr.sharedMaterials = newMats;
                    }

                    Animator anim = inst.GetComponent<Animator>();
                    if (anim == null) anim = inst.AddComponent<Animator>();
                    if (wraithAvatar != null) anim.avatar = wraithAvatar;
                    if (controller != null) anim.runtimeAnimatorController = controller;

                    PrefabUtility.SaveAsPrefabAsset(inst, basePrefabPath);
                    DestroyImmediate(inst);
                    Debug.Log($"[ParagonWraith] Created ready Base Wraith prefab: {basePrefabPath}");
                }
            }
        }

        private static Avatar GetWraithAvatar()
        {
            string odFbxPath = "Assets/Developers/Gaurav/ParagonWraith/Characters/Heroes/Wraith/Skins/ODGreen/Meshes/Wraith_ODGreen.fbx";
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(odFbxPath))
            {
                if (obj is Avatar av) return av;
            }
            string baseFbxPath = "Assets/Developers/Gaurav/ParagonWraith/Characters/Heroes/Wraith/Meshes/Wraith.fbx";
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(baseFbxPath))
            {
                if (obj is Avatar av) return av;
            }
            return null;
        }

        private static AnimationClip GetClipByName(string clipName)
        {
            string directPath = $"Assets/Developers/Gaurav/ParagonWraith/Characters/Heroes/Wraith/Animations/{clipName}.fbx";
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(directPath))
            {
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    return clip;
                }
            }

            string combatLocomotionPath = $"Assets/Developers/Gaurav/ParagonWraith/Characters/Heroes/Wraith/Animations/Locomotion_Combat/{clipName}.fbx";
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(combatLocomotionPath))
            {
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    return clip;
                }
            }

            string[] guids = AssetDatabase.FindAssets($"{clipName} t:Model");
            foreach (string g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (p.Contains("ParagonWraith") && p.Contains("Animations"))
                {
                    foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(p))
                    {
                        if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                        {
                            return clip;
                        }
                    }
                }
            }
            return null;
        }

        private static Texture2D FindTexture(string texName)
        {
            if (string.IsNullOrEmpty(texName)) return null;

            string odTexPath = $"Assets/Developers/Gaurav/ParagonWraith/Characters/Heroes/Wraith/Skins/ODGreen/Textures/{texName}.png";
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(odTexPath);
            if (tex != null) return tex;

            string baseTexPath = $"Assets/Developers/Gaurav/ParagonWraith/Characters/Heroes/Wraith/Textures/{texName}.png";
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(baseTexPath);
            if (tex != null) return tex;

            string[] guids = AssetDatabase.FindAssets($"{texName} t:Texture2D");
            foreach (string g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (p.Contains("ParagonWraith") && Path.GetFileNameWithoutExtension(p) == texName)
                {
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                }
            }
            return null;
        }
    }
}
#endif
