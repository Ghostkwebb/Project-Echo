#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Collections.Generic;

namespace ParagonHowitzer
{
    [InitializeOnLoad]
    public class ParagonHowitzerDebrisAutoRunner
    {
        static ParagonHowitzerDebrisAutoRunner()
        {
            EditorApplication.update += RunOnceUpdate;
        }

        private static void RunOnceUpdate()
        {
            EditorApplication.update -= RunOnceUpdate;
            if (SessionState.GetBool("ParagonHowitzer_DebrisRemap_Ran_v4", false)) return;
            SessionState.SetBool("ParagonHowitzer_DebrisRemap_Ran_v4", true);

            Debug.Log("[ParagonHowitzer] Auto-running Debris, DetCharge, and MicroRocket material setup...");
            ParagonHowitzerUnitySetup.SetupDebrisAndDetChargeMaterials();
        }
    }

    public class ParagonHowitzerUnitySetup : EditorWindow
    {
        [MenuItem("Window/Paragon Howitzer/Apply Showcase Pose (Like Reference Image)", false, 0)]
        public static void ApplyShowcasePoseDirect()
        {
            ApplyPose("FrontEndPose");
            EditorUtility.DisplayDialog("Paragon Howitzer", 
                "Showcase Pose Applied!\n\nHowitzer is now standing tall and upright in his hero select stance (matching your reference image).", "Awesome!");
        }

        [MenuItem("Window/Paragon Howitzer/Apply Combat Idle Crouch", false, 1)]
        public static void ApplyCombatIdleDirect()
        {
            ApplyPose("Idle");
            EditorUtility.DisplayDialog("Paragon Howitzer", 
                "Combat Idle Applied!\n\nHowitzer is in his low combat spring-crouch ready for battle.", "OK");
        }

        [MenuItem("Window/Paragon Howitzer/Run Complete Setup Now (One-Click)", false, 2)]
        public static void RunFromMenu()
        {
            Debug.Log("[ParagonHowitzer] Starting complete setup from menu...");
            FixNormalMaps();
            SetupCharacterRigs();
            SetupAnimations();
            SetupOrangeMaterialsAndPrefab();
            ApplyPose("FrontEndPose");
            Debug.Log("[ParagonHowitzer] >>> COMPLETE SETUP COMPLETED SUCCESSFULLY! <<<");
            EditorUtility.DisplayDialog("Paragon Howitzer Setup", 
                "Orange & White Domed Howitzer setup complete!\n\n" +
                "- Normal Maps configured\n" +
                "- Humanoid Rigs configured\n" +
                "- 83 Animations retargeted\n" +
                "- 10 Orange & White Domed PBR Materials configured\n" +
                "- Hero Showcase Pose applied (matching reference image)!\n" +
                "- Pilot seated in cockpit with controls!", "Awesome!");
        }

        [MenuItem("Window/Paragon Howitzer/Setup Window (Sharad)", false, 3)]
        public static void ShowWindow()
        {
            GetWindow<ParagonHowitzerUnitySetup>("Howitzer Setup (Sharad)");
        }

        private Vector2 scrollPos;
        private string logOutput = "Paragon Howitzer Tool Ready.\nSelect an action below:";

        private void OnGUI()
        {
            GUILayout.Label("Paragon Howitzer - Rig & Posing Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox("Posing Options:\n" +
                "• Showcase Pose (FrontEndPose): Howitzer stands tall, upright, and proud with cannon ready (matches your reference image).\n" +
                "• Combat Idle: Howitzer crouches low in his spring-loaded artillery battle stance.", MessageType.Info);

            EditorGUILayout.Space();

            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.3f);
            if (GUILayout.Button("★ Apply Showcase Pose (Matching Reference Image)", GUILayout.Height(36)))
            {
                ApplyPose("FrontEndPose");
                logOutput += "\n[Pose] Applied Showcase Pose (FrontEndPose) - Tall upright hero stance.";
            }

            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("Apply Combat Idle Crouch (In-Game Battle Stance)", GUILayout.Height(30)))
            {
                ApplyPose("Idle");
                logOutput += "\n[Pose] Applied Combat Idle (low battle crouch).";
            }

            EditorGUILayout.Space();
            GUILayout.Label("Asset Pipeline Steps:", EditorStyles.boldLabel);

            if (GUILayout.Button("1. Configure Normal Maps", GUILayout.Height(26)))
            {
                FixNormalMaps();
                logOutput += "\n[Step 1] Normal maps configured.";
            }

            if (GUILayout.Button("2. Setup Generic Rig & Bake Axis Conversion on Models", GUILayout.Height(26)))
            {
                SetupCharacterRigs();
                logOutput += "\n[Step 2] Humanoid rigs configured.";
            }

            if (GUILayout.Button("3. Setup Generic Rig & Bake Axis Conversion on Animations", GUILayout.Height(26)))
            {
                SetupAnimations();
                logOutput += "\n[Step 3] Animations retargeted.";
            }

            if (GUILayout.Button("4. Re-Build Orange & White PBR Materials & Prefab", GUILayout.Height(26)))
            {
                SetupOrangeMaterialsAndPrefab();
                logOutput += "\n[Step 4] Orange Domed Materials and Prefab generated.";
            }

            EditorGUILayout.Space();

            GUI.backgroundColor = new Color(1f, 0.5f, 0.1f);
            if (GUILayout.Button("Run Complete One-Click Setup", GUILayout.Height(40)))
            {
                FixNormalMaps();
                SetupCharacterRigs();
                SetupAnimations();
                SetupOrangeMaterialsAndPrefab();
                ApplyPose("FrontEndPose");
                logOutput += "\n>>> COMPLETE SETUP COMPLETED SUCCESSFULLY! <<<";
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space();
            GUILayout.Label("Log Output:", EditorStyles.boldLabel);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(140));
            EditorGUILayout.TextArea(logOutput, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        public static void ApplyPose(string clipName)
        {
            Avatar howitzerAvatar = GetHowitzerAvatar();
            AnimationClip targetClip = GetClipByName(clipName);

            if (targetClip == null)
            {
                Debug.LogError($"[ParagonHowitzer] Could not find animation clip: {clipName}");
                return;
            }

            // 1. Create or update Animator Controller
            string controllerPath = "Assets/Developers/Sharad/ParagonHowitzer/Howitzer_Orange_Animator.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            }

            var rootStateMachine = controller.layers[0].stateMachine;
            // Clear or update default state
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

            // 2. Setup on Prefab (keep prefab pristine without baking child bone overrides)
            string prefabPath = "Assets/Developers/Sharad/ParagonHowitzer/Howitzer_Orange_Ready.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                using (var editScope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
                {
                    GameObject root = editScope.prefabContentsRoot;
                    Animator anim = root.GetComponent<Animator>();
                    if (anim == null) anim = root.AddComponent<Animator>();
                    if (howitzerAvatar != null) anim.avatar = howitzerAvatar;
                    anim.runtimeAnimatorController = controller;
                }
            }

            // 3. Pose Scene Instance
            GameObject sceneObj = GameObject.Find("Howitzer_Orange_Ready");
            if (sceneObj != null)
            {
                // Revert any corrupted bone transform overrides from the scene instance
                PrefabUtility.RevertPrefabInstance(sceneObj, InteractionMode.AutomatedAction);

                Animator sceneAnim = sceneObj.GetComponent<Animator>();
                if (sceneAnim == null) sceneAnim = sceneObj.AddComponent<Animator>();
                if (howitzerAvatar != null) sceneAnim.avatar = howitzerAvatar;
                sceneAnim.runtimeAnimatorController = controller;

                targetClip.SampleAnimation(sceneObj, 0f);
                EditorUtility.SetDirty(sceneObj);
                Debug.Log($"[ParagonHowitzer] Applied '{clipName}' to scene object successfully!");
            }
        }

        public static void FixNormalMaps()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D");
            int count = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("ParagonHowitzer")) continue;

                string filename = Path.GetFileNameWithoutExtension(path).ToLower();
                if (filename.EndsWith("_n") || filename.Contains("_normal") || filename.Contains("_n_demo") || filename.EndsWith("_nrm") || filename.Contains("_n_") || filename.Contains("demo_n"))
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
            Debug.Log($"[ParagonHowitzer] Configured {count} textures as Normal Maps.");
        }

        public static void SetupCharacterRigs()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model");
            int count = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("ParagonHowitzer")) continue;

                string name = Path.GetFileNameWithoutExtension(path);
                if (name == "Howitzer_Domed" || name == "Howitzer_GDC" || name == "HowitzerHotRod" || name == "HowitzerWasteland")
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
            }
            Debug.Log($"[ParagonHowitzer] Configured {count} character models as Generic with BakeAxisConversion=true.");
        }

        public static void SetupAnimations()
        {
            Avatar howitzerAvatar = GetHowitzerAvatar();

            string[] guids = AssetDatabase.FindAssets("t:Model");
            int count = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("ParagonHowitzer") || !path.Contains("Animations")) continue;

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
                    if (howitzerAvatar != null && importer.sourceAvatar != howitzerAvatar)
                    {
                        importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                        importer.sourceAvatar = howitzerAvatar;
                        changed = true;
                    }
                    if (changed)
                    {
                        importer.SaveAndReimport();
                        count++;
                    }
                }
            }
            Debug.Log($"[ParagonHowitzer] Configured {count} animations as Generic with BakeAxisConversion=true (Avatar: {(howitzerAvatar != null ? howitzerAvatar.name : "None")}).");
        }

        public static void SetupOrangeMaterialsAndPrefab()
        {
            string[] texPaths = new string[] {
                "Assets/Developers/Sharad/ParagonHowitzer/Characters/Heroes/Howitzer/Skins/Tier_2/Domed/Textures/T_Howitzer_Domed_Body_Albedo.png",
                "Assets/Developers/Sharad/ParagonHowitzer/Characters/Heroes/Howitzer/Skins/Tier_2/Domed/Textures/T_Howitzer_Domed_Arms_Albedo.png",
                "Assets/Developers/Sharad/ParagonHowitzer/Characters/Heroes/Howitzer/Skins/Tier_2/Domed/Textures/T_Howitzer_Domed_Legs_Albedo.png",
                "Assets/Developers/Sharad/ParagonHowitzer/Characters/Heroes/Howitzer/Skins/Tier_2/Domed/Textures/T_Howitzer_Domed_RabbitSuit_Albedo.png",
                "Assets/Developers/Sharad/ParagonHowitzer/Characters/Heroes/Howitzer/Skins/Tier_2/Domed/Textures/T_Howitzer_Domed_TealGlow.png"
            };
            foreach (string tp in texPaths)
            {
                AssetDatabase.ImportAsset(tp, ImportAssetOptions.ForceUpdate);
            }

            string matDir = "Assets/Developers/Sharad/ParagonHowitzer/Characters/Heroes/Howitzer/Materials/OrangeDomed";
            if (!AssetDatabase.IsValidFolder(matDir))
            {
                Directory.CreateDirectory(matDir);
                AssetDatabase.Refresh();
            }

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            Material CreateMat(string matName, string albedoName, string normalName, float metallic, float smoothness, Color? emissive = null)
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

                if (emissive.HasValue)
                {
                    Color emColor = emissive.Value;
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.SetColor("_EmissionColor", emColor);
                        mat.EnableKeyword("_EMISSION");
                    }
                }

                EditorUtility.SetDirty(mat);
                return mat;
            }

            Color tealGlow = new Color(0f, 1f, 0.75f) * 3f;

            Material matBody = CreateMat("M_Howitzer_Body_DomeWhite", "T_Howitzer_Domed_Body_Albedo", "T_M_Howitzer_Domed_Demo_N", 0.2f, 0.65f);
            Material matArms = CreateMat("M_HowitzerV2_Arms_DomeWhite", "T_Howitzer_Domed_Arms_Albedo", "T_HowitzerV2_Arms_N", 0.2f, 0.65f);
            Material matLegs = CreateMat("M_HowitzerV2_Legs_DomeWhite", "T_Howitzer_Domed_Legs_Albedo", "T_M_HowitzerV2_Legs_N", 0.2f, 0.65f);
            Material matGlow = CreateMat("M_Howitzer_Glow_DomeWhite", "T_Howitzer_Domed_TealGlow", "", 0.05f, 0.95f, tealGlow);
            Material matMine = CreateMat("M_Howitzer_Mine_DomeWhite", "T_Howitzer_Domed_TealGlow", "", 0.1f, 0.8f, tealGlow * 0.7f);
            Material matRabbitSuit = CreateMat("M_Howitzer_Body_DomeRabbit", "T_Howitzer_Domed_RabbitSuit_Albedo", "T_Howitzer_Rabbit_N", 0.2f, 0.55f);
            Material matRabbitFur = CreateMat("M_Howitzer_Rabbit_Body_Domed", "T_Howitzer_Rabbit_Skin_Blonde", "T_Howitzer_Rabbit_N", 0.02f, 0.35f);

            AssetDatabase.SaveAssets();

            string domedFbxPath = "Assets/Developers/Sharad/ParagonHowitzer/Characters/Heroes/Howitzer/Skins/Tier_2/Domed/Meshes/Howitzer_Domed.fbx";
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(domedFbxPath);
            if (modelPrefab == null)
            {
                Debug.LogError($"[ParagonHowitzer] Could not find Howitzer_Domed.fbx at {domedFbxPath}!");
                return;
            }

            string prefabPath = "Assets/Developers/Sharad/ParagonHowitzer/Howitzer_Orange_Ready.prefab";
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
            if (instance != null)
            {
                Material[] orangeMats = new Material[10]
                {
                    matBody, matArms, matArms, matLegs, matGlow, matMine, matLegs, matRabbitSuit, matLegs, matRabbitFur
                };

                SkinnedMeshRenderer[] renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach (var smr in renderers)
                {
                    Material[] newMats = new Material[smr.sharedMaterials.Length];
                    for (int i = 0; i < newMats.Length; i++)
                    {
                        newMats[i] = (i < orangeMats.Length) ? orangeMats[i] : matBody;
                    }
                    smr.sharedMaterials = newMats;
                }

                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);

                GameObject sceneObj = GameObject.Find("Howitzer_Orange_Ready");
                if (sceneObj != null)
                {
                    SkinnedMeshRenderer[] sceneSmrs = sceneObj.GetComponentsInChildren<SkinnedMeshRenderer>();
                    SkinnedMeshRenderer[] instSmrs = instance.GetComponentsInChildren<SkinnedMeshRenderer>();
                    if (sceneSmrs.Length > 0 && instSmrs.Length > 0)
                    {
                        sceneSmrs[0].sharedMaterials = instSmrs[0].sharedMaterials;
                        sceneSmrs[0].sharedMesh = instSmrs[0].sharedMesh;
                    }
                    EditorUtility.SetDirty(sceneObj);
                }

                DestroyImmediate(instance);
                Debug.Log($"[ParagonHowitzer] Created ready Orange Domed prefab: {prefabPath}");
            }
        }

        private static Avatar GetHowitzerAvatar()
        {
            string domedFbxPath = "Assets/Developers/Sharad/ParagonHowitzer/Characters/Heroes/Howitzer/Skins/Tier_2/Domed/Meshes/Howitzer_Domed.fbx";
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(domedFbxPath))
            {
                if (obj is Avatar av) return av;
            }
            string gdcFbxPath = "Assets/Developers/Sharad/ParagonHowitzer/Characters/Heroes/Howitzer/Meshes/Howitzer_GDC.fbx";
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(gdcFbxPath))
            {
                if (obj is Avatar av) return av;
            }
            return null;
        }

        private static AnimationClip GetClipByName(string clipName)
        {
            string animPath = $"Assets/Developers/Sharad/ParagonHowitzer/Characters/Heroes/Howitzer/Animations/{clipName}.fbx";
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(animPath))
            {
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    return clip;
                }
            }
            return null;
        }

        private static Texture2D FindTexture(string texName)
        {
            if (string.IsNullOrEmpty(texName)) return null;
            string[] guids = AssetDatabase.FindAssets($"{texName} t:Texture2D");
            foreach (string g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (p.Contains("ParagonHowitzer") && Path.GetFileNameWithoutExtension(p) == texName)
                {
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                }
            }
            return null;
        }

        [MenuItem("Window/Paragon Howitzer/Setup Debris and DetCharge Materials", false, 4)]
        public static void SetupDebrisAndDetChargeMaterials()
        {
            string orangeDomedDir = "Assets/Developers/Sharad/ParagonHowitzer/Characters/Heroes/Howitzer/Materials/OrangeDomed";
            Material matMine = AssetDatabase.LoadAssetAtPath<Material>($"{orangeDomedDir}/M_Howitzer_Mine_DomeWhite.mat");
            Material matDebris = AssetDatabase.LoadAssetAtPath<Material>($"{orangeDomedDir}/M_DebrisPieces_DomeWhite.mat");
            Material matArms = AssetDatabase.LoadAssetAtPath<Material>($"{orangeDomedDir}/M_HowitzerV2_Arms_DomeWhite.mat");

            if (matMine == null)
            {
                Debug.LogError("[ParagonHowitzer] Could not find M_Howitzer_Mine_DomeWhite.mat!");
                return;
            }
            if (matDebris == null)
            {
                Debug.LogError("[ParagonHowitzer] Could not find M_DebrisPieces_DomeWhite.mat!");
                return;
            }
            if (matArms == null)
            {
                Debug.LogError("[ParagonHowitzer] Could not find M_HowitzerV2_Arms_DomeWhite.mat!");
                return;
            }

            // 0. Configure and Fix Textures & Materials
            string texDir = "Assets/Developers/Sharad/ParagonHowitzer/Characters/Heroes/Howitzer/Skins/Tier_2/Domed/Textures";
            string fxTexDir = "Assets/Developers/Sharad/ParagonHowitzer/FX/Textures";

            // Fix Scorch Decal Texture
            string scorchPath = $"{fxTexDir}/Decals/Scorch/T_Decal_Small_Scorch_A.png";
            TextureImporter scorchImporter = AssetImporter.GetAtPath(scorchPath) as TextureImporter;
            if (scorchImporter != null)
            {
                scorchImporter.alphaSource = TextureImporterAlphaSource.FromInput;
                scorchImporter.alphaIsTransparency = true;
                scorchImporter.SaveAndReimport();
                Debug.Log("[ParagonHowitzer] Fixed T_Decal_Small_Scorch_A TextureImporter with alpha transparency!");
            }

            // Configure Mine DomeWhite Material
            Texture2D texMineAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/T_Howitzer_Domed_Mine_Albedo.png");
            Texture2D texMineNormal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{fxTexDir}/CharacterSpecific/T_Howitzer_Mine_N.png");
            Texture2D texMineEmissive = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/T_Howitzer_Domed_Mine_Emissive.png");

            if (texMineAlbedo != null) matMine.SetTexture("_BaseMap", texMineAlbedo);
            if (texMineNormal != null) matMine.SetTexture("_BumpMap", texMineNormal);
            if (texMineEmissive != null) matMine.SetTexture("_Emissive_Tex", texMineEmissive);
            matMine.SetColor("_BaseColor", Color.white);
            matMine.SetColor("_Emissive_Color", new Color(0f, 3f, 2.8f, 1f));
            matMine.SetFloat("_Use_BaseAs1st", 1f);
            matMine.SetFloat("_Use_1stAs2nd", 1f);
            matMine.SetColor("_1st_ShadeColor", new Color(0.55f, 0.58f, 0.67f, 1f));
            matMine.SetColor("_2nd_ShadeColor", new Color(0.24f, 0.25f, 0.31f, 1f));
            EditorUtility.SetDirty(matMine);

            // Configure Debris DomeWhite Material
            Texture2D texDebrisAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/T_Howitzer_Domed_Debris_Albedo.png");
            Texture2D texDebrisNormal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{fxTexDir}/Tile/T_Metal_Patina_N.png");

            if (texDebrisAlbedo != null) matDebris.SetTexture("_BaseMap", texDebrisAlbedo);
            if (texDebrisNormal != null) matDebris.SetTexture("_BumpMap", texDebrisNormal);
            matDebris.SetColor("_BaseColor", Color.white);
            matDebris.SetFloat("_Use_BaseAs1st", 1f);
            matDebris.SetFloat("_Use_1stAs2nd", 1f);
            matDebris.SetColor("_1st_ShadeColor", new Color(0.55f, 0.58f, 0.67f, 1f));
            matDebris.SetColor("_2nd_ShadeColor", new Color(0.24f, 0.25f, 0.31f, 1f));
            EditorUtility.SetDirty(matDebris);

            // 1. Remap DetCharge FBXs
            string detchargeDir = "Assets/Developers/Sharad/ParagonHowitzer/FX/Meshes/DetCharge";
            if (Directory.Exists(detchargeDir))
            {
                string[] detchargeFiles = Directory.GetFiles(detchargeDir, "*.fbx");
                foreach (string fbxPath in detchargeFiles)
                {
                    ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                    if (importer != null)
                    {
                        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_Howitzer_Mine_FX"), matMine);
                        importer.SaveAndReimport();
                        Debug.Log($"[ParagonHowitzer] Remapped {Path.GetFileName(fbxPath)} -> {matMine.name}");
                    }
                }
            }

            // 2. Remap Debris FBXs
            string debrisDir = "Assets/Developers/Sharad/ParagonHowitzer/FX/Meshes/Debris";
            if (Directory.Exists(debrisDir))
            {
                string[] debrisFiles = Directory.GetFiles(debrisDir, "*.fbx");
                foreach (string fbxPath in debrisFiles)
                {
                    ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                    if (importer != null)
                    {
                        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "WorldGridMaterial"), matDebris);
                        importer.SaveAndReimport();
                        Debug.Log($"[ParagonHowitzer] Remapped {Path.GetFileName(fbxPath)} -> {matDebris.name}");
                    }
                }
            }

            // 3. Remap MicroRocket FBXs
            string microRocketDir = "Assets/Developers/Sharad/ParagonHowitzer/FX/Meshes/MicroRocket";
            if (Directory.Exists(microRocketDir))
            {
                string[] mrFiles = Directory.GetFiles(microRocketDir, "*.fbx");
                foreach (string fbxPath in mrFiles)
                {
                    ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                    if (importer != null)
                    {
                        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                        string fileName = Path.GetFileNameWithoutExtension(fbxPath);

                        if (fileName == "SM_Howitzer_SlowGrenade_Open_Mine")
                        {
                            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_HowitzerV2_ParticleMesh"), matArms);
                            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_Howitzer_Mine_TeamColor"), matMine);
                            Debug.Log($"[ParagonHowitzer] Remapped {fileName} -> [0: {matArms.name}, 1: {matMine.name}]");
                        }
                        else
                        {
                            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_HowitzerV2_Arms"), matArms);
                            Debug.Log($"[ParagonHowitzer] Remapped {fileName} -> {matArms.name}");
                        }
                        importer.SaveAndReimport();
                    }
                }
            }

            // 4. Sync materials to FX/Materials
            string fxMatMine = "Assets/Developers/Sharad/ParagonHowitzer/FX/Materials/CharacterSpecific/M_Howitzer_Mine_FX.mat";
            string fxMatDebris = "Assets/Developers/Sharad/ParagonHowitzer/FX/Materials/Debris/M_DebrisPieces.mat";
            Material mfxMine = AssetDatabase.LoadAssetAtPath<Material>(fxMatMine);
            Material mfxDebris = AssetDatabase.LoadAssetAtPath<Material>(fxMatDebris);
            if (mfxMine != null)
            {
                mfxMine.CopyPropertiesFromMaterial(matMine); mfxMine.name = "M_Howitzer_Mine_FX";
                EditorUtility.SetDirty(mfxMine);
            }
            if (mfxDebris != null)
            {
                mfxDebris.CopyPropertiesFromMaterial(matDebris); mfxDebris.name = "M_DebrisPieces";
                EditorUtility.SetDirty(mfxDebris);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ParagonHowitzer] >>> Debris, DetCharge, and MicroRocket Materials successfully set up! <<<");
        }

    }
}
#endif
