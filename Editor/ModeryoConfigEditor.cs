using UnityEngine;
using UnityEditor;

namespace Moderyo.Editor
{
    /// <summary>
    /// Custom inspector for ModeryoConfig
    /// </summary>
    [CustomEditor(typeof(ModeryoConfig))]
    public class ModeryoConfigEditor : UnityEditor.Editor
    {
        private bool _showAdvanced = false;
        private bool _showLocalFilter = false;

        public override void OnInspectorGUI()
        {
            var config = (ModeryoConfig)target;

            serializedObject.Update();

            DrawHeader();
            EditorGUILayout.Space(10);
            DrawApiSettings(config);
            EditorGUILayout.Space(10);
            DrawAdvancedSettings();
            EditorGUILayout.Space(10);
            DrawLocalFilter();
            EditorGUILayout.Space(10);
            DrawActions(config);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            
            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft
            };
            EditorGUILayout.LabelField("🛡️ Moderyo Configuration", headerStyle, GUILayout.Height(25));
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("Configure your Moderyo API settings. Get your API key from dashboard.moderyo.com", MessageType.Info);
        }

        private void DrawApiSettings(ModeryoConfig config)
        {
            EditorGUILayout.LabelField("API Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            // API Key with validation
            EditorGUILayout.PropertyField(serializedObject.FindProperty("apiKey"), new GUIContent("API Key"));
            
            if (string.IsNullOrEmpty(config.ApiKey))
            {
                EditorGUILayout.HelpBox("API Key is required", MessageType.Warning);
            }
            else if (!config.ApiKey.StartsWith("mod_"))
            {
                EditorGUILayout.HelpBox("API Key should start with 'mod_'", MessageType.Warning);
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("baseUrl"), new GUIContent("Base URL"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultModel"), new GUIContent("Default Model"));

            EditorGUILayout.EndVertical();
        }

        private void DrawAdvancedSettings()
        {
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced Settings", true);
            
            if (_showAdvanced)
            {
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.PropertyField(serializedObject.FindProperty("timeout"), new GUIContent("Timeout (seconds)"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("maxRetries"), new GUIContent("Max Retries"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("retryDelay"), new GUIContent("Retry Delay (seconds)"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("enableLogging"), new GUIContent("Enable Logging"));
                
                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("offlineMode"), new GUIContent("Offline Mode"));
                
                EditorGUILayout.HelpBox(
                    "Offline Mode: What happens when API is unavailable\n" +
                    "• AllowAll: Allow all content\n" +
                    "• BlockAll: Block all content\n" +
                    "• UseLocalFilter: Use local word filter\n" +
                    "• Queue: Throw exception (handle manually)", 
                    MessageType.None);

                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultMode"), new GUIContent("Default Mode"));
                EditorGUILayout.HelpBox("enforce = apply policy, shadow = log only. Leave empty for API default.", MessageType.None);

                EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultRisk"), new GUIContent("Default Risk"));
                EditorGUILayout.HelpBox("conservative, balanced, or aggressive. Leave empty for API default.", MessageType.None);

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawLocalFilter()
        {
            _showLocalFilter = EditorGUILayout.Foldout(_showLocalFilter, "Local Filter Words", true);
            
            if (_showLocalFilter)
            {
                EditorGUILayout.BeginVertical("box");
                
                EditorGUILayout.HelpBox("Words to block when using LocalFilter offline mode. One word per line.", MessageType.None);
                
                EditorGUILayout.PropertyField(serializedObject.FindProperty("localFilterWords"), new GUIContent("Filter Words"), true);

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawActions(ModeryoConfig config)
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Test Connection", GUILayout.Height(25)))
            {
                TestConnection(config);
            }

            if (GUILayout.Button("Open Dashboard", GUILayout.Height(25)))
            {
                Application.OpenURL("https://dashboard.moderyo.com");
            }

            if (GUILayout.Button("Documentation", GUILayout.Height(25)))
            {
                Application.OpenURL("https://docs.moderyo.com/unity");
            }

            EditorGUILayout.EndHorizontal();
        }

        private async void TestConnection(ModeryoConfig config)
        {
            if (!config.IsValid())
            {
                EditorUtility.DisplayDialog("Invalid Config", "Please configure a valid API key first.", "OK");
                return;
            }

            EditorUtility.DisplayProgressBar("Testing Connection", "Connecting to Moderyo API...", 0.5f);

            try
            {
                var client = new ModeryoClient(config);
                var isHealthy = await client.HealthCheckAsync();

                EditorUtility.ClearProgressBar();

                if (isHealthy)
                {
                    EditorUtility.DisplayDialog("Success", "Successfully connected to Moderyo API! ✓", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Connection Failed", "Could not connect to Moderyo API. Please check your configuration.", "OK");
                }
            }
            catch (AuthenticationException)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Authentication Failed", "Invalid API key. Please check your API key and try again.", "OK");
            }
            catch (System.Exception ex)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Error", $"Connection test failed: {ex.Message}", "OK");
            }
        }
    }
}
