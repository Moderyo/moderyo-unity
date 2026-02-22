using UnityEngine;
using UnityEditor;
using System;
using System.Threading.Tasks;

namespace Moderyo.Editor
{
    /// <summary>
    /// Editor window for testing content moderation directly in the Unity Editor.
    /// Available via Tools > Moderyo > Test Window.
    /// </summary>
    public class ModeryoTestWindow : EditorWindow
    {
        private ModeryoConfig _config;
        private string _testContent = "Enter text to test moderation...";
        private string _apiKey = "";
        private bool _useConfigAsset = true;
        private Vector2 _scrollPosition;
        private ModerationResult _lastResult;
        private bool _isLoading;
        private string _errorMessage;

        // Options
        private int _modeIndex = 0; // 0=default, 1=enforce, 2=shadow
        private int _riskIndex = 0; // 0=default, 1=conservative, 2=balanced, 3=aggressive
        private readonly string[] _modeOptions = { "Default", "enforce", "shadow" };
        private readonly string[] _riskOptions = { "Default", "conservative", "balanced", "aggressive" };

        [MenuItem("Tools/Moderyo/Test Window")]
        public static void ShowWindow()
        {
            var window = GetWindow<ModeryoTestWindow>("Moderyo Tester");
            window.minSize = new Vector2(400, 600);
        }

        [MenuItem("Tools/Moderyo/Create Config Asset")]
        public static void CreateConfigAsset()
        {
            var asset = ScriptableObject.CreateInstance<ModeryoConfig>();
            var path = EditorUtility.SaveFilePanelInProject(
                "Save Moderyo Config", "ModeryoConfig", "asset",
                "Select location for Moderyo config asset");

            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = asset;
                EditorUtility.DisplayDialog("Success",
                    "Moderyo config asset created!\n\nDon't forget to set your API key.", "OK");
            }
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(10);
            DrawConfiguration();
            EditorGUILayout.Space(10);
            DrawTestInput();
            EditorGUILayout.Space(10);
            DrawResults();
            EditorGUILayout.Space(10);
            DrawQuickActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("Moderyo Test Panel", headerStyle, GUILayout.Height(30));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                $"Test content moderation directly in the Editor. SDK v2.0.7 — 27 categories.",
                MessageType.Info);
        }

        private void DrawConfiguration()
        {
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            _useConfigAsset = EditorGUILayout.Toggle("Use Config Asset", _useConfigAsset);

            if (_useConfigAsset)
            {
                _config = (ModeryoConfig)EditorGUILayout.ObjectField(
                    "Config Asset", _config, typeof(ModeryoConfig), false);
                if (_config == null)
                    EditorGUILayout.HelpBox(
                        "Assign a ModeryoConfig asset or create one via Tools > Moderyo > Create Config Asset",
                        MessageType.Warning);
            }
            else
            {
                _apiKey = EditorGUILayout.PasswordField("API Key", _apiKey);
                if (string.IsNullOrEmpty(_apiKey))
                    EditorGUILayout.HelpBox("Enter your Moderyo API key", MessageType.Warning);
            }

            EditorGUILayout.Space(5);
            _modeIndex = EditorGUILayout.Popup("Mode", _modeIndex, _modeOptions);
            _riskIndex = EditorGUILayout.Popup("Risk Level", _riskIndex, _riskOptions);

            EditorGUILayout.EndVertical();
        }

        private void DrawTestInput()
        {
            EditorGUILayout.LabelField("Test Content", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            _testContent = EditorGUILayout.TextArea(_testContent, GUILayout.Height(80));

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = CanTest() && !_isLoading;

            if (GUILayout.Button(_isLoading ? "Testing..." : "Test Moderation", GUILayout.Height(30)))
                TestModeration();

            if (GUILayout.Button("Clear", GUILayout.Width(60), GUILayout.Height(30)))
            {
                _testContent = "";
                _lastResult = null;
                _errorMessage = null;
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawResults()
        {
            EditorGUILayout.LabelField("Results", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            if (!string.IsNullOrEmpty(_errorMessage))
            {
                EditorGUILayout.HelpBox(_errorMessage, MessageType.Error);
            }
            else if (_lastResult != null)
            {
                // Decision banner
                var actionColor = _lastResult.Action switch
                {
                    Decision.Block => Color.red,
                    Decision.Flag  => Color.yellow,
                    Decision.Warn  => new Color(1f, 0.6f, 0f),
                    _              => Color.green
                };
                var prevColor = GUI.backgroundColor;
                GUI.backgroundColor = actionColor;
                EditorGUILayout.BeginHorizontal("box");
                EditorGUILayout.LabelField($"Decision: {_lastResult.Action}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Flagged: {_lastResult.Flagged}");
                EditorGUILayout.EndHorizontal();
                GUI.backgroundColor = prevColor;
                EditorGUILayout.Space(5);

                // Policy decision details
                if (_lastResult.PolicyDecision != null)
                {
                    var pd = _lastResult.PolicyDecision;
                    if (!string.IsNullOrEmpty(pd.RuleName))
                        EditorGUILayout.LabelField($"Rule: {pd.RuleName}");
                    if (!string.IsNullOrEmpty(pd.Reason))
                        EditorGUILayout.HelpBox(pd.Reason, MessageType.None);
                    if (pd.Confidence > 0)
                        EditorGUILayout.LabelField($"Confidence: {pd.Confidence:P0}");
                }

                EditorGUILayout.Space(5);

                // Triggered categories
                EditorGUILayout.LabelField("Triggered Categories:", EditorStyles.boldLabel);
                var triggered = _lastResult.GetTriggeredCategories();
                if (triggered.Count > 0)
                {
                    foreach (var cat in triggered)
                        EditorGUILayout.LabelField($"  ! {cat}");
                }
                else
                {
                    EditorGUILayout.LabelField("  None");
                }

                EditorGUILayout.Space(5);

                // Simplified scores
                if (_lastResult.Scores != null)
                {
                    EditorGUILayout.LabelField("Simplified Scores:", EditorStyles.boldLabel);
                    DrawScoreBar("Toxicity", _lastResult.Scores.Toxicity);
                    DrawScoreBar("Hate", _lastResult.Scores.Hate);
                    DrawScoreBar("Harassment", _lastResult.Scores.Harassment);
                    DrawScoreBar("Violence", _lastResult.Scores.Violence);
                    DrawScoreBar("Scam", _lastResult.Scores.Scam);
                    DrawScoreBar("Fraud", _lastResult.Scores.Fraud);
                }

                EditorGUILayout.Space(5);

                // Top category scores
                if (_lastResult.CategoryScores != null)
                {
                    var topScores = _lastResult.CategoryScores.Above(0.1f);
                    if (topScores.Count > 0)
                    {
                        EditorGUILayout.LabelField("Category Scores (> 10%):", EditorStyles.boldLabel);
                        foreach (var kvp in topScores)
                            DrawScoreBar(kvp.Key, kvp.Value);
                    }
                }

                // Detected phrases
                if (_lastResult.DetectedPhrases != null && _lastResult.DetectedPhrases.Count > 0)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Detected Phrases:", EditorStyles.boldLabel);
                    foreach (var phrase in _lastResult.DetectedPhrases)
                        EditorGUILayout.LabelField($"  [{phrase.Label}] \"{phrase.Text}\"");
                }

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField($"ID: {_lastResult.Id}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Model: {_lastResult.Model}", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField(
                    "No results yet. Enter text and click 'Test Moderation'.",
                    EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawScoreBar(string label, float score)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(140));
            var rect = EditorGUILayout.GetControlRect(GUILayout.Height(16));
            EditorGUI.ProgressBar(rect, score, $"{score:P1}");
            EditorGUILayout.EndHorizontal();
        }

        private void DrawQuickActions()
        {
            EditorGUILayout.LabelField("Quick Test Samples", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Safe Text"))
                _testContent = "Hello! How are you doing today?";
            if (GUILayout.Button("Mild Profanity"))
                _testContent = "This is damn frustrating!";
            if (GUILayout.Button("Spam"))
                _testContent = "BUY NOW!!! BEST DEAL EVER!!! CLICK HERE!!!";
            EditorGUILayout.EndHorizontal();
        }

        private bool CanTest()
        {
            if (string.IsNullOrEmpty(_testContent)) return false;
            if (_useConfigAsset) return _config != null && _config.IsValid();
            return !string.IsNullOrEmpty(_apiKey);
        }

        private async void TestModeration()
        {
            _isLoading = true;
            _errorMessage = null;
            _lastResult = null;
            Repaint();

            try
            {
                ModeryoClient client = _useConfigAsset
                    ? new ModeryoClient(_config)
                    : new ModeryoClient(_apiKey);

                ModerationOptions options = null;
                if (_modeIndex > 0 || _riskIndex > 0)
                {
                    options = new ModerationOptions();
                    if (_modeIndex > 0) options.Mode = _modeOptions[_modeIndex];
                    if (_riskIndex > 0) options.Risk = _riskOptions[_riskIndex];
                }

                _lastResult = await client.ModerateAsync(_testContent, options);
            }
            catch (ModeryoException ex)
            {
                _errorMessage = $"{ex.GetType().Name}: {ex.Message}";
            }
            catch (Exception ex)
            {
                _errorMessage = $"Error: {ex.Message}";
            }
            finally
            {
                _isLoading = false;
                Repaint();
            }
        }
    }
}
