using UnityEngine;
using UnityEditor;
using Peak.Settings;
using System.IO;

namespace Peak.Editor.Settings
{
    /// <summary>
    /// Main settings window for Peak SDK configuration
    /// Provides centralized settings management with validation and security features
    /// </summary>
    public class PeakSDKSettingsWindow : EditorWindow
    {
        private PeakSDKSettings _settings;
        private Vector2 _scrollPosition;
        private ValidationResult _validationResult;
        private bool _settingsChanged = false;
        // Settings will be created in the project's Assets folder, not in the package
        private const string SettingsPath = "Assets/Resources/PeakSettings.asset";
        private const string ResourcesPath = "Assets/Resources";

        [MenuItem("Peak SDK/Settings", priority = 1)]
        public static void ShowWindow()
        {
            var window = GetWindow<PeakSDKSettingsWindow>("Peak SDK Settings");
            window.minSize = new Vector2(500, 600);
            window.Show();
        }

        private void OnEnable()
        {
            LoadOrCreateSettings();
            ValidateSettings();
        }

        private void OnGUI()
        {
            if (_settings == null)
            {
                EditorGUILayout.HelpBox("Settings file could not be loaded. Please create a new one.", MessageType.Error);
                if (GUILayout.Button("Create New Settings"))
                {
                    CreateNewSettings();
                }
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawEnvironmentSection();
            EditorGUILayout.Space(10);

            DrawApiEndpointsSection();
            EditorGUILayout.Space(10);

            DrawSecuritySection();
            EditorGUILayout.Space(10);

            DrawDebugSection();
            EditorGUILayout.Space(10);

            DrawValidationSection();
            EditorGUILayout.Space(10);

            DrawActionsSection();

            EditorGUILayout.EndScrollView();

            // Handle unsaved changes
            if (_settingsChanged)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox("Settings have been modified. Don't forget to save!", MessageType.Warning);
                if (GUILayout.Button("Save", GUILayout.Width(60)))
                {
                    SaveSettings();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical("box");

            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };

            EditorGUILayout.LabelField("Peak SDK Settings", headerStyle);
            EditorGUILayout.Space(5);

            var descriptionStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                fontSize = 12
            };

            EditorGUILayout.LabelField("Configure your Peak SDK settings for different environments. " +
                                     "Sensitive information is automatically excluded from version control.",
                                     descriptionStyle);

            EditorGUILayout.EndVertical();
        }

        private void DrawEnvironmentSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Environment Configuration", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            _settings.Environment = (EnvironmentType)EditorGUILayout.EnumPopup(
                new GUIContent("Environment", "Select the target environment for API calls"),
                _settings.Environment);

            EditorGUILayout.HelpBox($"Current API URL: {_settings.CurrentApiUrl}", MessageType.Info);

            if (EditorGUI.EndChangeCheck())
            {
                _settingsChanged = true;
                ValidateSettings();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawApiEndpointsSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("API Endpoints", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            _settings.DevelopmentApiUrl = EditorGUILayout.TextField(
                new GUIContent("Development URL", "API endpoint for development environment"),
                _settings.DevelopmentApiUrl);

            _settings.StagingApiUrl = EditorGUILayout.TextField(
                new GUIContent("Staging URL", "API endpoint for staging environment"),
                _settings.StagingApiUrl);

            _settings.ProductionApiUrl = EditorGUILayout.TextField(
                new GUIContent("Production URL", "API endpoint for production environment"),
                _settings.ProductionApiUrl);

            if (EditorGUI.EndChangeCheck())
            {
                _settingsChanged = true;
                ValidateSettings();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSecuritySection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Security Settings", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox("⚠️ Security Notice: The Project API Key will be excluded from version control.", MessageType.Warning);

            EditorGUI.BeginChangeCheck();

            // Custom property field for password field
            var serializedObject = new SerializedObject(_settings);
            var projectApiKeyProperty = serializedObject.FindProperty("projectApiKey");
            EditorGUILayout.PropertyField(projectApiKeyProperty, new GUIContent("Project API Key", "Your Peak SDK project API key"));
            serializedObject.ApplyModifiedProperties();

            if (EditorGUI.EndChangeCheck())
            {
                _settingsChanged = true;
                ValidateSettings();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawDebugSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Debug Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            _settings.EnableDebugLogging = EditorGUILayout.Toggle(
                new GUIContent("Enable Debug Logging", "Show detailed debug information in console"),
                _settings.EnableDebugLogging);

            _settings.ValidateApiConnectivity = EditorGUILayout.Toggle(
                new GUIContent("Validate API Connectivity", "Test API connection on initialization"),
                _settings.ValidateApiConnectivity);

            if (EditorGUI.EndChangeCheck())
            {
                _settingsChanged = true;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawValidationSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Settings Validation", EditorStyles.boldLabel);

            if (_validationResult != null)
            {
                if (_validationResult.IsValid)
                {
                    EditorGUILayout.HelpBox("✓ All settings are valid", MessageType.Info);
                }
                else
                {
                    foreach (var error in _validationResult.Errors)
                    {
                        EditorGUILayout.HelpBox($"❌ Error: {error}", MessageType.Error);
                    }
                }

                foreach (var warning in _validationResult.Warnings)
                {
                    EditorGUILayout.HelpBox($"⚠️ Warning: {warning}", MessageType.Warning);
                }
            }

            if (GUILayout.Button("Validate Settings"))
            {
                ValidateSettings();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawActionsSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Save Settings"))
            {
                SaveSettings();
            }

            if (GUILayout.Button("Reset to Defaults"))
            {
                if (EditorUtility.DisplayDialog("Reset Settings",
                    "Are you sure you want to reset all settings to their default values?",
                    "Yes", "Cancel"))
                {
                    ResetToDefaults();
                }
            }

            if (GUILayout.Button("Open Settings File"))
            {
                EditorGUIUtility.PingObject(_settings);
                Selection.activeObject = _settings;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Test API Connection"))
            {
                TestApiConnection();
            }

            if (GUILayout.Button("Export Configuration"))
            {
                ExportConfiguration();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void LoadOrCreateSettings()
        {
            _settings = PeakSDKSettings.LoadFromResources();

            if (_settings == null)
            {
                CreateNewSettings();
            }
        }

        private void CreateNewSettings()
        {
            // Ensure Resources directory exists
            if (!Directory.Exists(ResourcesPath))
            {
                Directory.CreateDirectory(ResourcesPath);
                AssetDatabase.Refresh();
            }

            _settings = PeakSDKSettings.CreateDefault();
            AssetDatabase.CreateAsset(_settings, SettingsPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Peak SDK] New settings file created at " + SettingsPath);
        }

        private void SaveSettings()
        {
            if (_settings != null)
            {
                EditorUtility.SetDirty(_settings);
                AssetDatabase.SaveAssets();
                _settingsChanged = false;

                ValidateSettings();
                Debug.Log("[Peak SDK] Settings saved successfully");
            }
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
            _settingsChanged = true;
            ValidateSettings();
        }

        private void TestApiConnection()
        {
            PeakSDKSettingsEditorHelper.TestApiConnection(_settings);
        }

        private void ExportConfiguration()
        {
            PeakSDKSettingsEditorHelper.ExportConfiguration(_settings);
        }

        private void OnDestroy()
        {
            if (_settingsChanged)
            {
                if (EditorUtility.DisplayDialog("Unsaved Changes",
                    "You have unsaved changes to Peak SDK settings. Do you want to save them?",
                    "Save", "Discard"))
                {
                    SaveSettings();
                }
            }
        }
    }
}