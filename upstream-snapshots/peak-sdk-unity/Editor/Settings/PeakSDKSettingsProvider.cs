using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Peak.Settings;

namespace Peak.Editor.Settings
{
    /// <summary>
    /// Settings provider for Unity Project Settings integration
    /// Adds Peak SDK settings to the Project Settings window
    /// </summary>
    public class PeakSDKSettingsProvider : SettingsProvider
    {
        private PeakSDKSettings _settings;
        private SerializedObject _serializedSettings;
        private ValidationResult _validationResult;
        private bool _foldoutEnvironment = true;
        private bool _foldoutSecurity = true;
        private bool _foldoutDebug = true;

        private const string SettingsPath = "Project/Peak SDK";

        public PeakSDKSettingsProvider(string path, SettingsScope scope = SettingsScope.Project)
            : base(path, scope)
        {
        }

        public override void OnActivate(string searchContext, UnityEngine.UIElements.VisualElement rootElement)
        {
            LoadSettings();
        }

        public override void OnGUI(string searchContext)
        {
            if (_settings == null)
            {
                DrawCreateSettingsUI();
                return;
            }

            if (_serializedSettings == null)
            {
                _serializedSettings = new SerializedObject(_settings);
            }

            _serializedSettings.Update();

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawEnvironmentSection();
            EditorGUILayout.Space(5);

            DrawSecuritySection();
            EditorGUILayout.Space(5);

            DrawDebugSection();
            EditorGUILayout.Space(5);

            DrawValidationSection();
            EditorGUILayout.Space(10);

            DrawQuickActions();

            if (_serializedSettings.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_settings);
                AssetDatabase.SaveAssets();
                ValidateSettings();
            }
        }

        private void DrawCreateSettingsUI()
        {
            EditorGUILayout.Space(20);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Peak SDK Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox("No Peak SDK settings file found. Create one to configure your SDK.", MessageType.Info);
            EditorGUILayout.Space(10);

            if (GUILayout.Button("Create Peak SDK Settings", GUILayout.Height(30)))
            {
                CreateSettingsAsset();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical("box");

            var titleStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 16
            };

            EditorGUILayout.LabelField("Peak SDK Configuration", titleStyle);
            EditorGUILayout.Space(5);

            var currentEnv = _settings.Environment.ToString();
            var currentUrl = _settings.CurrentApiUrl;

            EditorGUILayout.LabelField($"Current Environment: {currentEnv}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"API URL: {currentUrl}", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawEnvironmentSection()
        {
            _foldoutEnvironment = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutEnvironment, "Environment Configuration");

            if (_foldoutEnvironment)
            {
                EditorGUI.indentLevel++;

                var environmentProperty = _serializedSettings.FindProperty("environment");
                EditorGUILayout.PropertyField(environmentProperty, new GUIContent("Target Environment"));

                EditorGUILayout.Space(5);

                var devUrlProperty = _serializedSettings.FindProperty("developmentApiUrl");
                var stagingUrlProperty = _serializedSettings.FindProperty("stagingApiUrl");
                var prodUrlProperty = _serializedSettings.FindProperty("productionApiUrl");

                EditorGUILayout.PropertyField(devUrlProperty, new GUIContent("Development API URL"));
                EditorGUILayout.PropertyField(stagingUrlProperty, new GUIContent("Staging API URL"));
                EditorGUILayout.PropertyField(prodUrlProperty, new GUIContent("Production API URL"));

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawSecuritySection()
        {
            _foldoutSecurity = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutSecurity, "Security Settings");

            if (_foldoutSecurity)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.HelpBox("🔒 The Project API Key is automatically excluded from version control for security.", MessageType.Info);

                var apiKeyProperty = _serializedSettings.FindProperty("projectApiKey");
                EditorGUILayout.PropertyField(apiKeyProperty, new GUIContent("Project API Key"));

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawDebugSection()
        {
            _foldoutDebug = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutDebug, "Debug & Development");

            if (_foldoutDebug)
            {
                EditorGUI.indentLevel++;

                var debugLoggingProperty = _serializedSettings.FindProperty("enableDebugLogging");
                var validateConnectivityProperty = _serializedSettings.FindProperty("validateApiConnectivity");

                EditorGUILayout.PropertyField(debugLoggingProperty, new GUIContent("Enable Debug Logging"));
                EditorGUILayout.PropertyField(validateConnectivityProperty, new GUIContent("Validate API Connectivity"));

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawValidationSection()
        {
            EditorGUILayout.LabelField("Settings Validation", EditorStyles.boldLabel);

            if (_validationResult != null)
            {
                if (_validationResult.IsValid)
                {
                    EditorGUILayout.HelpBox("✓ Configuration is valid", MessageType.Info);
                }
                else
                {
                    foreach (var error in _validationResult.Errors)
                    {
                        EditorGUILayout.HelpBox($"❌ {error}", MessageType.Error);
                    }
                }

                foreach (var warning in _validationResult.Warnings)
                {
                    EditorGUILayout.HelpBox($"⚠️ {warning}", MessageType.Warning);
                }
            }

            if (GUILayout.Button("Validate Settings"))
            {
                ValidateSettings();
            }
        }

        private void DrawQuickActions()
        {
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Open Settings Window"))
            {
                PeakSDKSettingsWindow.ShowWindow();
            }

            if (GUILayout.Button("Reset to Defaults"))
            {
                if (EditorUtility.DisplayDialog("Reset Settings",
                    "Reset all settings to default values? (API key will be preserved)",
                    "Reset", "Cancel"))
                {
                    ResetToDefaults();
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Export Config"))
            {
                ExportConfiguration();
            }

            if (GUILayout.Button("Test Connection"))
            {
                TestApiConnection();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void LoadSettings()
        {
            _settings = PeakSDKSettings.LoadFromResources();

            if (_settings != null)
            {
                _serializedSettings = new SerializedObject(_settings);
                ValidateSettings();
            }
        }

        private void CreateSettingsAsset()
        {
            var window = PeakSDKSettingsWindow.GetWindow<PeakSDKSettingsWindow>();
            window.Close(); // Close if already open

            // The settings window will create the asset
            PeakSDKSettingsWindow.ShowWindow();

            // Reload settings after creation
            EditorApplication.delayCall += () =>
            {
                LoadSettings();
                Repaint();
            };
        }

        private void ValidateSettings()
        {
            if (_settings != null)
            {
                _validationResult = _settings.ValidateSettings();
            }
        }

        private void ResetToDefaults()
        {
            PeakSDKSettingsEditorHelper.ResetToDefaults(_settings);
            ValidateSettings();
            _serializedSettings = new SerializedObject(_settings);
        }

        private void TestApiConnection()
        {
            PeakSDKSettingsEditorHelper.TestApiConnection(_settings);
        }

        private void ExportConfiguration()
        {
            PeakSDKSettingsEditorHelper.ExportConfiguration(_settings);
        }

        [SettingsProvider]
        public static SettingsProvider CreatePeakSDKSettingsProvider()
        {
            var provider = new PeakSDKSettingsProvider(SettingsPath, SettingsScope.Project)
            {
                label = "Peak SDK",
                keywords = GetSearchKeywordsFromGUIContentProperties<Styles>()
            };

            return provider;
        }

        private class Styles
        {
            public static readonly GUIContent Environment = new GUIContent("Environment");
            public static readonly GUIContent ApiUrl = new GUIContent("API URL");
            public static readonly GUIContent ProjectApiKey = new GUIContent("Project API Key");
            public static readonly GUIContent DebugLogging = new GUIContent("Debug Logging");
            public static readonly GUIContent Validation = new GUIContent("Validation");
        }
    }
}