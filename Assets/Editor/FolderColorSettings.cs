using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Ghostkwebb.EditorTools
{
    [Serializable]
    public class FolderColorEntry
    {
        public string guid;
        public Color color;
    }

    public class FolderColorSettings : ScriptableObject
    {
        public List<FolderColorEntry> folderColors = new List<FolderColorEntry>();

        public static FolderColorSettings GetOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<FolderColorSettings>("Assets/Editor/FolderColorSettings.asset");
            if (settings == null)
            {
                settings = CreateInstance<FolderColorSettings>();
                if (!AssetDatabase.IsValidFolder("Assets/Editor"))
                    AssetDatabase.CreateFolder("Assets", "Editor");

                AssetDatabase.CreateAsset(settings, "Assets/Editor/FolderColorSettings.asset");
                AssetDatabase.SaveAssets();
            }
            return settings;
        }

        public bool TryGetColor(string guid, out Color color)
        {
            var entry = folderColors.Find(e => e.guid == guid);
            if (entry != null)
            {
                color = entry.color;
                return true;
            }
            color = Color.white;
            return false;
        }

        public void SetColor(string guid, Color color)
        {
            var entry = folderColors.Find(e => e.guid == guid);
            if (entry != null) entry.color = color;
            else folderColors.Add(new FolderColorEntry { guid = guid, color = color });

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        public void RemoveColor(string guid)
        {
            folderColors.RemoveAll(e => e.guid == guid);
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }
}