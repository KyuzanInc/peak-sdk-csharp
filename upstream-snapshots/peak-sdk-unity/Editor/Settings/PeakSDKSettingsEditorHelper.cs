using UnityEngine;
using UnityEditor;
using Peak.Settings;

namespace Peak.Editor.Settings
{
    /// <summary>
    /// Shared helper methods for Peak SDK settings editors
    /// Used by both PeakSDKSettingsWindow and PeakSDKSettingsProvider
    /// </summary>
    public static class PeakSDKSettingsEditorHelper
    {
        /// <summary>
        /// Tests API connection (placeholder - not fully implemented)
        /// </summary>
        public static void TestApiConnection(PeakSDKSettings settings)
        {
            if (settings == null) return;

            var validation = settings.ValidateSettings();
            if (!validation.IsValid)
            {
                EditorUtility.DisplayDialog("Connection Test Failed",
                    "Cannot test connection: Settings validation failed.\n\n" +
                    string.Join("\n", validation.Errors), "OK");
                return;
            }

            EditorUtility.DisplayDialog("API Connection Test",
                $"Testing connection to: {settings.CurrentApiUrl}\n\n" +
                "Note: Full connectivity testing will be implemented in future versions.", "OK");
        }

        /// <summary>
        /// Exports configuration to a JSON file
        /// </summary>
        public static void ExportConfiguration(PeakSDKSettings settings)
        {
            if (settings == null) return;

            var path = EditorUtility.SaveFilePanel("Export Peak SDK Configuration",
                "", "peak-sdk-config.json", "json");

            if (!string.IsNullOrEmpty(path))
            {
                var config = JsonUtility.ToJson(settings.GetSdkOptions(), true);
                System.IO.File.WriteAllText(path, config);

                EditorUtility.DisplayDialog("Export Complete",
                    $"Configuration exported to:\n{path}\n\n" +
                    "Note: Sensitive information has been excluded.", "OK");
            }
        }

        /// <summary>
        /// Resets settings to defaults while preserving the API key
        /// </summary>
        public static void ResetToDefaults(PeakSDKSettings settings)
        {
            if (settings == null) return;

            var defaultSettings = PeakSDKSettings.CreateDefault();
            var currentApiKey = settings.ProjectApiKey;

            settings.Environment = defaultSettings.Environment;
            settings.DevelopmentApiUrl = defaultSettings.DevelopmentApiUrl;
            settings.StagingApiUrl = defaultSettings.StagingApiUrl;
            settings.ProductionApiUrl = defaultSettings.ProductionApiUrl;
            settings.EnableDebugLogging = defaultSettings.EnableDebugLogging;
            settings.ValidateApiConnectivity = defaultSettings.ValidateApiConnectivity;

            // Restore API key
            settings.ProjectApiKey = currentApiKey;

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }
}
