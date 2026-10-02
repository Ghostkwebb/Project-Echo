#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace ParagonWraith
{
    public class ParagonWraithUnitySetup : EditorWindow
    {
        [MenuItem("Window/Paragon Wraith/Setup Assets for Unity")]
        public static void ShowWindow()
        {
            GetWindow<ParagonWraithUnitySetup>("Paragon Wraith Setup");
        }

        private Vector2 scrollPos;
        private string logOutput = "Click below to automatically configure imported FBX models, animations, and textures for Unity testing.";

        private void OnGUI()
        {
            GUILayout.Label("Paragon Wraith - Unity Auto-Setup Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox("This tool automatically:\n" +
                "1. Configures Wraith character FBXs as Humanoid Rigs\n" +
                "2. Retargets Animation FBXs to the Wraith Humanoid Avatar\n" +
                "3. Marks Normal map textures as NormalMap type", MessageType.Info);

            EditorGUILayout.Space();

            if (GUILayout.Button("1. Configure Normal Maps", GUILayout.Height(32)))
            {
                FixNormalMaps();
            }

            if (GUILayout.Button("2. Setup Humanoid Rig on Character Models", GUILayout.Height(32)))
            {
                SetupCharacterRigs();
            }

            if (GUILayout.Button("3. Setup Humanoid Rig on Animations", GUILayout.Height(32)))
            {
                SetupAnimations();
            }

            if (GUILayout.Button("4. Run Complete One-Click Setup", GUILayout.Height(40)))
            {
                FixNormalMaps();
                SetupCharacterRigs();
                SetupAnimations();
                logOutput += "\n>>> COMPLETE SETUP COMPLETED SUCCESSFULLY! <<<";
            }

            EditorGUILayout.Space();
            GUILayout.Label("Log Output:", EditorStyles.boldLabel);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(200));
            EditorGUILayout.TextArea(logOutput, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void FixNormalMaps()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D");
            int count = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("ParagonWraith")) continue;

                string filename = Path.GetFileNameWithoutExtension(path).ToLower();
                if (filename.EndsWith("_n") || filename.Contains("_normal") || filename.Contains("_n_demo") || filename.EndsWith("_nrm"))
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
            logOutput += $"\nConfigured {count} textures as Normal Maps.";
            Debug.Log($"[ParagonWraith] Configured {count} textures as Normal Maps.");
        }

        private void SetupCharacterRigs()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model");
            int count = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("ParagonWraith")) continue;

                string name = Path.GetFileNameWithoutExtension(path);
                if (name == "Wraith" || name == "Wraith_LunarOps" || name == "Wraith_ODGreen")
                {
                    ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (importer != null && importer.animationType != ModelImporterAnimationType.Human)
                    {
                        importer.animationType = ModelImporterAnimationType.Human;
                        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                        importer.SaveAndReimport();
                        count++;
                    }
                }
            }
            logOutput += $"\nConfigured {count} character models as Humanoid rigs.";
            Debug.Log($"[ParagonWraith] Configured {count} character models as Humanoid.");
        }

        private void SetupAnimations()
        {
            // Find base Wraith avatar
            Avatar wraithAvatar = null;
            string[] charGuids = AssetDatabase.FindAssets("Wraith t:Model");
            foreach (string guid in charGuids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(p) == "Wraith" && p.Contains("ParagonWraith"))
                {
                    Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(p);
                    foreach (Object sub in subAssets)
                    {
                        if (sub is Avatar av)
                        {
                            wraithAvatar = av;
                            break;
                        }
                    }
                    if (wraithAvatar != null) break;
                }
            }

            string[] guids = AssetDatabase.FindAssets("t:Model");
            int count = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("ParagonWraith") || !path.Contains("Animations")) continue;

                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer != null && importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    if (wraithAvatar != null)
                    {
                        importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                        importer.sourceAvatar = wraithAvatar;
                    }
                    importer.SaveAndReimport();
                    count++;
                }
            }
            logOutput += $"\nConfigured {count} animations as Humanoid.";
            Debug.Log($"[ParagonWraith] Configured {count} animations as Humanoid.");
        }
    }
}
#endif
