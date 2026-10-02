#pragma warning disable CS0619
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace Ghostkwebb.EditorTools
{
    [InitializeOnLoad]
    public static class FolderColorCustomizer
    {
        private static Texture2D s_FolderClosedIcon;
        private static Texture2D s_FolderOpenedIcon;
        private static FolderColorSettings s_CachedSettings;

        private static readonly HashSet<string> s_ExpandedFolderPaths = new HashSet<string>();
        private static double s_LastExpandedCheckTime;
        private static Type s_ProjectBrowserType;

        static FolderColorCustomizer()
        {
            EditorApplication.projectWindowItemOnGUI -= DrawFolderColorOverlay;
            EditorApplication.projectWindowItemOnGUI += DrawFolderColorOverlay;
        }

        public static FolderColorSettings GetSettings()
        {
            if (s_CachedSettings == null)
            {
                s_CachedSettings = FolderColorSettings.GetOrCreateSettings();
            }
            return s_CachedSettings;
        }

        public static Texture2D GetFolderIcon(bool isOpen)
        {
            if (isOpen)
            {
                if (s_FolderOpenedIcon == null)
                {
                    var content = EditorGUIUtility.IconContent("FolderOpened Icon");
                    if (content != null && content.image != null)
                        s_FolderOpenedIcon = content.image as Texture2D;

                    if (s_FolderOpenedIcon == null)
                    {
                        content = EditorGUIUtility.IconContent("FolderOpened On Icon");
                        if (content != null && content.image != null)
                            s_FolderOpenedIcon = content.image as Texture2D;
                    }

                    if (s_FolderOpenedIcon == null)
                    {
                        content = EditorGUIUtility.IconContent("d_FolderOpened Icon");
                        if (content != null && content.image != null)
                            s_FolderOpenedIcon = content.image as Texture2D;
                    }
                }
                if (s_FolderOpenedIcon != null) return s_FolderOpenedIcon;
            }

            if (s_FolderClosedIcon == null)
            {
                var content = EditorGUIUtility.IconContent("Folder Icon");
                if (content != null && content.image != null)
                    s_FolderClosedIcon = content.image as Texture2D;

                if (s_FolderClosedIcon == null)
                {
                    content = EditorGUIUtility.IconContent("d_Folder Icon");
                    if (content != null && content.image != null)
                        s_FolderClosedIcon = content.image as Texture2D;
                }
            }
            return s_FolderClosedIcon;
        }

        private static bool IsFolderExpanded(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            string normalizedPath = path.Replace('\\', '/').TrimEnd('/');

            Event currentEvent = Event.current;
            if (currentEvent != null && (currentEvent.type == EventType.MouseDown || currentEvent.type == EventType.MouseUp))
            {
                s_LastExpandedCheckTime = 0;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now - s_LastExpandedCheckTime > 0.05)
            {
                s_LastExpandedCheckTime = now;
                RefreshExpandedIDs();
            }

            return s_ExpandedFolderPaths.Contains(normalizedPath);
        }

        private static void RefreshExpandedIDs()
        {
            s_ExpandedFolderPaths.Clear();
            try
            {
                if (s_ProjectBrowserType == null)
                    s_ProjectBrowserType = typeof(Editor).Assembly.GetType("UnityEditor.ProjectBrowser");

                if (s_ProjectBrowserType == null) return;

                var browsers = Resources.FindObjectsOfTypeAll(s_ProjectBrowserType);
                if (browsers == null || browsers.Length == 0) return;

                var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                MethodInfo getPathByEntityId = null;

                foreach (var browser in browsers)
                {
                    if (browser == null) continue;

                    var fields = s_ProjectBrowserType.GetFields(flags);
                    foreach (var field in fields)
                    {
                        if (field.Name == "m_FolderTree" || field.Name == "m_AssetTree" || field.FieldType.Name.Contains("Tree"))
                        {
                            var tree = field.GetValue(browser);
                            if (tree == null) continue;

                            var state = tree.GetType().GetProperty("state", flags)?.GetValue(tree)
                                     ?? tree.GetType().GetField("m_TreeViewState", flags)?.GetValue(tree);

                            if (state != null)
                            {
                                var expObj = state.GetType().GetField("expandedIDs", flags)?.GetValue(state)
                                          ?? state.GetType().GetProperty("expandedIDs", flags)?.GetValue(state);

                                if (expObj is System.Collections.IEnumerable list)
                                {
                                    foreach (var item in list)
                                    {
                                        if (item == null) continue;
                                        if (getPathByEntityId == null)
                                        {
                                            getPathByEntityId = typeof(AssetDatabase).GetMethod("GetAssetPath",
                                                BindingFlags.Public | BindingFlags.Static, null, new Type[] { item.GetType() }, null);
                                        }

                                        string itemPath = null;
                                        if (getPathByEntityId != null)
                                        {
                                            itemPath = getPathByEntityId.Invoke(null, new object[] { item }) as string;
                                        }

                                        if (!string.IsNullOrEmpty(itemPath))
                                        {
                                            s_ExpandedFolderPaths.Add(itemPath.Replace('\\', '/').TrimEnd('/'));
                                        }
                                    }
                                }
                            }

                            var data = tree.GetType().GetProperty("data", flags)?.GetValue(tree);
                            if (data != null)
                            {
                                var getExpMethod = data.GetType().GetMethod("GetExpandedIDs", flags);
                                if (getExpMethod != null && getExpMethod.Invoke(data, null) is System.Collections.IEnumerable dataList)
                                {
                                    foreach (var item in dataList)
                                    {
                                        if (item == null) continue;
                                        if (getPathByEntityId == null)
                                        {
                                            getPathByEntityId = typeof(AssetDatabase).GetMethod("GetAssetPath",
                                                BindingFlags.Public | BindingFlags.Static, null, new Type[] { item.GetType() }, null);
                                        }

                                        string itemPath = null;
                                        if (getPathByEntityId != null)
                                        {
                                            itemPath = getPathByEntityId.Invoke(null, new object[] { item }) as string;
                                        }

                                        if (!string.IsNullOrEmpty(itemPath))
                                        {
                                            s_ExpandedFolderPaths.Add(itemPath.Replace('\\', '/').TrimEnd('/'));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch {}
        }

        private static void DrawFolderColorOverlay(string guid, Rect selectionRect)
        {
            if (string.IsNullOrEmpty(guid)) return;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path)) return;

            bool isListView = selectionRect.height <= 20f;

            // Track folder hierarchy for tree view: if a child folder is rendered, its parent is open
            if (isListView)
            {
                string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(parent) && parent != "Assets" && parent != "Packages")
                {
                    s_ExpandedFolderPaths.Add(parent);
                }
            }

            var settings = GetSettings();
            if (settings == null || !settings.TryGetColor(guid, out Color color)) return;

            bool isExpanded = isListView && IsFolderExpanded(path);
            Texture2D icon = GetFolderIcon(isExpanded);
            if (icon == null) return;

            if (isListView)
            {
                // In Unity's Project TreeView, row icons are drawn inside a 16x16 square at selectionRect.x, selectionRect.y
                Rect iconRect = new Rect(selectionRect.x, selectionRect.y, 16f, 16f);

                Color originalColor = GUI.color;
                GUI.color = color;
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
                GUI.color = originalColor;
            }
            else
            {
                // In Unity's Project GridView, icons are drawn inside a square of selectionRect.width by selectionRect.width
                float iconSize = Mathf.Min(selectionRect.width, selectionRect.height);
                Rect iconRect = new Rect(selectionRect.x, selectionRect.y, iconSize, iconSize);

                Color originalColor = GUI.color;
                GUI.color = color;
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
                GUI.color = originalColor;
            }
        }

        [MenuItem("Assets/Set Folder Color...", true)]
        private static bool ValidateCustomFolderColor()
        {
            if (Selection.activeObject == null) return false;
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            return AssetDatabase.IsValidFolder(path);
        }

        [MenuItem("Assets/Set Folder Color...", false, 20)]
        private static void OpenFolderColorPicker()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            string guid = AssetDatabase.AssetPathToGUID(path);
            FolderColorPickerWindow.ShowWindow(guid, path);
        }

        [MenuItem("Assets/Clear Folder Color", true)]
        private static bool ValidateResetFolderColor()
        {
            if (Selection.activeObject == null) return false;
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!AssetDatabase.IsValidFolder(path)) return false;
            string guid = AssetDatabase.AssetPathToGUID(path);
            return GetSettings().TryGetColor(guid, out _);
        }

        [MenuItem("Assets/Clear Folder Color", false, 21)]
        private static void ResetFolderColor()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            string guid = AssetDatabase.AssetPathToGUID(path);
            var settings = GetSettings();
            settings.RemoveColor(guid);
            EditorApplication.RepaintProjectWindow();
        }
    }

    /// <summary>
    /// Professional, clean Unreal Engine-inspired color picker for folders.
    /// </summary>
    public class FolderColorPickerWindow : EditorWindow
    {
        private string folderGuid;
        private string folderPath;
        private string folderName;

        private Color initialColor = Color.white;
        private Color selectedColor = Color.white;
        private string hexString = "FFFFFF";

        // Professional curated Unreal-style palette: 16 colors (2 rows of 8)
        private static readonly Color[] PaletteColors = new Color[]
        {
            new Color(0.92f, 0.25f, 0.25f), // Red
            new Color(0.96f, 0.45f, 0.20f), // Coral
            new Color(0.96f, 0.62f, 0.15f), // Orange
            new Color(0.94f, 0.80f, 0.15f), // Gold
            new Color(0.40f, 0.82f, 0.35f), // Lime
            new Color(0.15f, 0.75f, 0.50f), // Emerald
            new Color(0.12f, 0.75f, 0.80f), // Teal
            new Color(0.20f, 0.68f, 0.95f), // Cyan

            new Color(0.25f, 0.50f, 0.95f), // Blue
            new Color(0.45f, 0.38f, 0.92f), // Indigo
            new Color(0.68f, 0.30f, 0.92f), // Purple
            new Color(0.90f, 0.28f, 0.70f), // Magenta
            new Color(0.95f, 0.48f, 0.65f), // Rose
            new Color(0.65f, 0.46f, 0.35f), // Warm Brown
            new Color(0.58f, 0.64f, 0.72f), // Slate
            new Color(0.30f, 0.33f, 0.40f)  // Charcoal
        };

        public static void ShowWindow(string guid, string path)
        {
            var window = GetWindow<FolderColorPickerWindow>(true, "Color Picker", true);
            window.folderGuid = guid;
            window.folderPath = path;
            window.folderName = System.IO.Path.GetFileName(path);

            var settings = FolderColorCustomizer.GetSettings();
            if (settings.TryGetColor(guid, out Color existing))
            {
                window.initialColor = existing;
                window.selectedColor = existing;
            }
            else
            {
                window.initialColor = new Color(0.2f, 0.7f, 0.9f);
                window.selectedColor = window.initialColor;
            }

            window.hexString = ColorUtility.ToHtmlStringRGB(window.selectedColor);

            window.minSize = new Vector2(290, 260);
            window.maxSize = new Vector2(290, 260);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            // Background padding
            EditorGUILayout.BeginVertical();
            GUILayout.Space(8);

            // Folder Title Header
            DrawHeader();
            GUILayout.Space(8);

            // Unreal-style Old vs New comparison bar
            DrawComparisonBar();
            GUILayout.Space(10);

            // Color Swatch Palette (Unreal style compact chips)
            DrawPalette();
            GUILayout.Space(8);

            // Custom Color Field & Hex Input
            DrawColorFields();
            GUILayout.Space(12);

            // Unreal style Footer Action Buttons
            DrawActionButtons();

            EditorGUILayout.EndVertical();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10);

            Texture2D folderIcon = FolderColorCustomizer.GetFolderIcon(false);
            if (folderIcon != null)
            {
                Color oldC = GUI.color;
                GUI.color = selectedColor;
                GUILayout.Label(new GUIContent(folderIcon), GUILayout.Width(20), GUILayout.Height(20));
                GUI.color = oldC;
                GUILayout.Space(4);
            }

            EditorGUILayout.BeginVertical();
            GUIStyle nameStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            EditorGUILayout.LabelField(string.IsNullOrEmpty(folderName) ? "Folder" : folderName, nameStyle);

            GUIStyle pathStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.4f, 0.4f, 0.4f) }
            };
            EditorGUILayout.LabelField(string.IsNullOrEmpty(folderPath) ? "" : folderPath, pathStyle);
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawComparisonBar()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10);

            Rect barRect = GUILayoutUtility.GetRect(270, 24);

            // Draw outer border
            EditorGUI.DrawRect(barRect, new Color(0.12f, 0.12f, 0.12f));

            // Split into Old (left) and New (right)
            float halfW = (barRect.width - 2) * 0.5f;
            Rect oldRect = new Rect(barRect.x + 1, barRect.y + 1, halfW, barRect.height - 2);
            Rect newRect = new Rect(barRect.x + 1 + halfW, barRect.y + 1, halfW, barRect.height - 2);

            EditorGUI.DrawRect(oldRect, initialColor);
            EditorGUI.DrawRect(newRect, selectedColor);

            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };

            labelStyle.normal.textColor = (initialColor.grayscale > 0.5f) ? Color.black : Color.white;
            GUI.Label(oldRect, "Old", labelStyle);

            labelStyle.normal.textColor = (selectedColor.grayscale > 0.5f) ? Color.black : Color.white;
            GUI.Label(newRect, "New", labelStyle);

            GUILayout.Space(10);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPalette()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10);

            EditorGUILayout.BeginVertical();
            GUIStyle subTitleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.65f, 0.65f, 0.65f) : new Color(0.35f, 0.35f, 0.35f) }
            };
            EditorGUILayout.LabelField("PRESET PALETTE", subTitleStyle);
            GUILayout.Space(3);

            // 2 rows of 8 swatches
            for (int row = 0; row < 2; row++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int col = 0; col < 8; col++)
                {
                    int index = row * 8 + col;
                    Color color = PaletteColors[index];
                    Rect swatchRect = GUILayoutUtility.GetRect(30, 20);

                    Event e = Event.current;
                    if (e.type == EventType.MouseDown && swatchRect.Contains(e.mousePosition))
                    {
                        ApplyColor(color);
                        e.Use();
                    }

                    // Draw swatch
                    EditorGUI.DrawRect(swatchRect, color);

                    // If active swatch, draw white outline
                    if (Mathf.Abs(selectedColor.r - color.r) < 0.02f &&
                        Mathf.Abs(selectedColor.g - color.g) < 0.02f &&
                        Mathf.Abs(selectedColor.b - color.b) < 0.02f)
                    {
                        DrawOutline(swatchRect, Color.white, 2);
                    }

                    if (col < 7) GUILayout.Space(4);
                }
                EditorGUILayout.EndHorizontal();
                if (row == 0) GUILayout.Space(4);
            }

            EditorGUILayout.EndVertical();

            GUILayout.Space(10);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawColorFields()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10);

            // Unity standard native color field (with eye-dropper)
            EditorGUI.BeginChangeCheck();
            Color pick = EditorGUILayout.ColorField(selectedColor, GUILayout.Width(150), GUILayout.Height(20));
            if (EditorGUI.EndChangeCheck())
            {
                ApplyColor(pick);
            }

            GUILayout.Space(8);

            // Hex code field
            GUILayout.Label("#", EditorStyles.miniBoldLabel, GUILayout.Width(12));
            EditorGUI.BeginChangeCheck();
            hexString = EditorGUILayout.TextField(hexString, GUILayout.Width(76), GUILayout.Height(18));
            if (EditorGUI.EndChangeCheck())
            {
                if (ColorUtility.TryParseHtmlString("#" + hexString, out Color hexColor))
                {
                    selectedColor = hexColor;
                    var settings = FolderColorCustomizer.GetSettings();
                    settings.SetColor(folderGuid, selectedColor);
                    EditorApplication.RepaintProjectWindow();
                }
            }

            GUILayout.Space(10);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10);

            // Clear Color (Unreal style secondary button)
            if (GUILayout.Button("Clear Color", GUILayout.Height(24), GUILayout.Width(84)))
            {
                var settings = FolderColorCustomizer.GetSettings();
                settings.RemoveColor(folderGuid);
                EditorApplication.RepaintProjectWindow();
                Close();
            }

            GUILayout.FlexibleSpace();

            // Cancel button
            if (GUILayout.Button("Cancel", GUILayout.Height(24), GUILayout.Width(66)))
            {
                // Revert to initial color
                var settings = FolderColorCustomizer.GetSettings();
                if (initialColor != Color.white)
                    settings.SetColor(folderGuid, initialColor);
                else
                    settings.RemoveColor(folderGuid);

                EditorApplication.RepaintProjectWindow();
                Close();
            }

            GUILayout.Space(6);

            // OK button (accent / primary)
            GUI.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.2f, 0.5f, 0.9f) : new Color(0.15f, 0.45f, 0.85f);
            if (GUILayout.Button("OK", GUILayout.Height(24), GUILayout.Width(66)))
            {
                var settings = FolderColorCustomizer.GetSettings();
                settings.SetColor(folderGuid, selectedColor);
                EditorApplication.RepaintProjectWindow();
                Close();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(10);
            EditorGUILayout.EndHorizontal();
        }

        private void ApplyColor(Color c)
        {
            selectedColor = c;
            hexString = ColorUtility.ToHtmlStringRGB(selectedColor);
            var settings = FolderColorCustomizer.GetSettings();
            settings.SetColor(folderGuid, selectedColor);
            EditorApplication.RepaintProjectWindow();
            Repaint();
        }

        private static void DrawOutline(Rect r, Color c, int thickness)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, thickness), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - thickness, r.width, thickness), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, thickness, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - thickness, r.y, thickness, r.height), c);
        }
    }
}