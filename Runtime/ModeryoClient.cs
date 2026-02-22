using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Moderyo
{
    /// <summary>
    /// Moderyo content moderation client for Unity.
    /// Provides both async (Task) and coroutine (IEnumerator) APIs.
    /// </summary>
    public class ModeryoClient
    {
        private readonly ModeryoConfig _config;
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly float _timeout;
        private readonly int _maxRetries;
        private readonly float _retryDelay;
        private readonly bool _enableLogging;
        private readonly OfflineMode _offlineMode;
        private readonly HashSet<string> _localFilterWords;

        private const string SdkVersion = "2.0.7";

        /// <summary>All 27 moderation category keys</summary>
        public static readonly string[] ALL_CATEGORIES = CategoryKeys.All;

        #region Constructors

        /// <summary>Create client with API key</summary>
        public ModeryoClient(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
                throw new ValidationException("API key is required", "apiKey");

            _apiKey = apiKey;
            _baseUrl = "https://api.moderyo.com";
            _timeout = 30f;
            _maxRetries = 3;
            _retryDelay = 1f;
            _enableLogging = false;
            _offlineMode = OfflineMode.AllowAll;
            _localFilterWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Create client with ScriptableObject config</summary>
        public ModeryoClient(ModeryoConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            if (!config.IsValid())
                throw new ValidationException("Invalid configuration", "config");

            _config = config;
            _apiKey = config.ApiKey;
            _baseUrl = config.BaseUrl.TrimEnd('/');
            _timeout = config.Timeout;
            _maxRetries = config.MaxRetries;
            _retryDelay = config.RetryDelay;
            _enableLogging = config.EnableLogging;
            _offlineMode = config.OfflineMode;
            _localFilterWords = new HashSet<string>(
                config.LocalFilterWords ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
        }

        #endregion

        #region Async API

        /// <summary>Moderate text asynchronously</summary>
        public Task<ModerationResult> ModerateAsync(string input)
        {
            return ModerateAsync(ModerationRequest.Of(input), null);
        }

        /// <summary>Moderate text with options asynchronously</summary>
        public Task<ModerationResult> ModerateAsync(string input, ModerationOptions options)
        {
            return ModerateAsync(ModerationRequest.Of(input), options);
        }

        /// <summary>Moderate request asynchronously</summary>
        public Task<ModerationResult> ModerateAsync(ModerationRequest request)
        {
            return ModerateAsync(request, null);
        }

        /// <summary>Moderate request with options asynchronously</summary>
        public async Task<ModerationResult> ModerateAsync(ModerationRequest request, ModerationOptions options)
        {
            if (string.IsNullOrEmpty(request.Input))
                throw new ValidationException("Input is required", "input");
            if (request.Input.Length > 10000)
                throw new ValidationException("Input must be 10,000 characters or fewer", "input");

            var jsonBody = BuildRequestJson(request);

            try
            {
                var response = await SendRequestAsync("POST", "/v1/moderation", jsonBody, options);
                return ParseResponse(response);
            }
            catch (NetworkException) when (_offlineMode != OfflineMode.Queue)
            {
                return HandleOfflineMode(request.Input);
            }
        }

        /// <summary>Moderate multiple texts asynchronously</summary>
        public async Task<BatchModerationResult> ModerateBatchAsync(
            IEnumerable<string> inputs, ModerationOptions options = null)
        {
            var batch = new BatchModerationResult();
            var inputList = new List<string>(inputs);

            for (int i = 0; i < inputList.Count; i++)
            {
                try
                {
                    var result = await ModerateAsync(inputList[i], options);
                    batch.Results.Add(result);
                }
                catch (ModeryoException ex)
                {
                    Log($"Batch item {i} failed: {ex.Message}");
                    // Add a blocked result for failed items
                    batch.Results.Add(new ModerationResult
                    {
                        Id = GenerateId(),
                        Model = "error",
                        Flagged = true,
                        Categories = new Categories(),
                        CategoryScores = new CategoryScores(),
                        Scores = new SimplifiedScores(),
                        PolicyDecision = new PolicyDecision
                        {
                            DecisionValue = "BLOCK",
                            Reason = $"Error: {ex.Message}"
                        }
                    });
                }
            }

            return batch;
        }

        /// <summary>Health check</summary>
        public async Task<bool> HealthCheckAsync()
        {
            try
            {
                await SendRequestAsync("GET", "/health", null, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Coroutine API

        /// <summary>Moderate text (coroutine-based)</summary>
        public ModerationOperation Moderate(string input)
        {
            return Moderate(ModerationRequest.Of(input), null);
        }

        /// <summary>Moderate text with options (coroutine-based)</summary>
        public ModerationOperation Moderate(string input, ModerationOptions options)
        {
            return Moderate(ModerationRequest.Of(input), options);
        }

        /// <summary>Moderate request (coroutine-based)</summary>
        public ModerationOperation Moderate(ModerationRequest request)
        {
            return Moderate(request, null);
        }

        /// <summary>Moderate request with options (coroutine-based)</summary>
        public ModerationOperation Moderate(ModerationRequest request, ModerationOptions options)
        {
            return new ModerationOperation(this, request, options);
        }

        /// <summary>Internal coroutine for ModerationOperation</summary>
        internal IEnumerator ModerateCoroutine(
            ModerationRequest request,
            ModerationOptions options,
            Action<ModerationResult> onSuccess,
            Action<ModeryoException> onError)
        {
            if (string.IsNullOrEmpty(request.Input))
            {
                onError?.Invoke(new ValidationException("Input is required", "input"));
                yield break;
            }
            if (request.Input.Length > 10000)
            {
                onError?.Invoke(new ValidationException("Input must be 10,000 characters or fewer", "input"));
                yield break;
            }

            var jsonBody = BuildRequestJson(request);
            var url = $"{_baseUrl}/v1/moderation";

            using (var webRequest = new UnityWebRequest(url, "POST"))
            {
                var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
                webRequest.uploadHandler = new UploadHandlerRaw(bodyBytes);
                webRequest.downloadHandler = new DownloadHandlerBuffer();

                // Set headers
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
                webRequest.SetRequestHeader("User-Agent", $"moderyo-sdk-unity/{SdkVersion}");
                SetModerationHeaders(webRequest, options);
                webRequest.timeout = (int)_timeout;

                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var result = ParseResponse(webRequest.downloadHandler.text);
                        onSuccess?.Invoke(result);
                    }
                    catch (Exception ex)
                    {
                        onError?.Invoke(new ModeryoException($"Failed to parse response: {ex.Message}"));
                    }
                }
                else if (webRequest.result == UnityWebRequest.Result.ConnectionError)
                {
                    if (_offlineMode != OfflineMode.Queue)
                    {
                        onSuccess?.Invoke(HandleOfflineMode(request.Input));
                    }
                    else
                    {
                        onError?.Invoke(new NetworkException(webRequest.error));
                    }
                }
                else
                {
                    var exception = HandleErrorResponse(webRequest);
                    onError?.Invoke(exception);
                }
            }
        }

        #endregion

        #region Request Building

        private string BuildRequestJson(ModerationRequest request)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"input\":\"{EscapeJson(request.Input)}\"");

            if (!string.IsNullOrEmpty(request.Model))
                sb.Append($",\"model\":\"{EscapeJson(request.Model)}\"");
            if (request.LongTextMode.HasValue)
                sb.Append($",\"long_text_mode\":{(request.LongTextMode.Value ? "true" : "false")}");
            if (request.LongTextThreshold.HasValue)
                sb.Append($",\"long_text_threshold\":{request.LongTextThreshold.Value.ToString(CultureInfo.InvariantCulture)}");
            if (request.SkipProfanity.HasValue && request.SkipProfanity.Value)
                sb.Append(",\"skip_profanity\":true");
            if (request.SkipThreat.HasValue && request.SkipThreat.Value)
                sb.Append(",\"skip_threat\":true");
            if (request.SkipMaskedWord.HasValue && request.SkipMaskedWord.Value)
                sb.Append(",\"skip_masked_word\":true");

            sb.Append("}");
            return sb.ToString();
        }

        private void SetModerationHeaders(UnityWebRequest webRequest, ModerationOptions options)
        {
            // From options
            if (options != null)
            {
                if (!string.IsNullOrEmpty(options.Mode))
                    webRequest.SetRequestHeader("X-Moderyo-Mode", options.Mode);
                if (!string.IsNullOrEmpty(options.Risk))
                    webRequest.SetRequestHeader("X-Moderyo-Risk", options.Risk);
                if (options.Debug.HasValue && options.Debug.Value)
                    webRequest.SetRequestHeader("X-Moderyo-Debug", "true");
                if (!string.IsNullOrEmpty(options.PlayerId))
                    webRequest.SetRequestHeader("X-Moderyo-Player-Id", options.PlayerId);
            }

            // Fallback to config defaults
            if (_config != null)
            {
                if (options == null || string.IsNullOrEmpty(options.Mode))
                {
                    if (!string.IsNullOrEmpty(_config.DefaultMode))
                        webRequest.SetRequestHeader("X-Moderyo-Mode", _config.DefaultMode);
                }
                if (options == null || string.IsNullOrEmpty(options.Risk))
                {
                    if (!string.IsNullOrEmpty(_config.DefaultRisk))
                        webRequest.SetRequestHeader("X-Moderyo-Risk", _config.DefaultRisk);
                }
            }
        }

        #endregion

        #region Response Parsing

        private ModerationResult ParseResponse(string json)
        {
            var result = new ModerationResult
            {
                Id = ExtractJsonString(json, "id") ?? GenerateId(),
                Model = ExtractJsonString(json, "model") ?? "omni-moderation-latest",
                Categories = new Categories(),
                CategoryScores = new CategoryScores(),
                Scores = new SimplifiedScores()
            };

            // Parse results[0] for categories and category_scores
            var resultsArray = ExtractJsonArray(json, "results");
            if (resultsArray != null && resultsArray.Count > 0)
            {
                var firstResult = resultsArray[0];
                result.Flagged = ExtractJsonBool(firstResult, "flagged");

                // categories
                var categoriesBlock = ExtractJsonObject(firstResult, "categories");
                if (!string.IsNullOrEmpty(categoriesBlock))
                {
                    foreach (var key in CategoryKeys.All)
                        result.Categories[key] = ExtractJsonBool(categoriesBlock, key);
                }

                // category_scores
                var scoresBlock = ExtractJsonObject(firstResult, "category_scores");
                if (!string.IsNullOrEmpty(scoresBlock))
                {
                    foreach (var key in CategoryKeys.All)
                        result.CategoryScores[key] = ExtractJsonFloat(scoresBlock, key);
                }
            }

            // Parse top-level scores (simplified)
            var topScores = ExtractJsonObject(json, "scores");
            if (!string.IsNullOrEmpty(topScores))
            {
                result.Scores.Toxicity = ExtractJsonFloat(topScores, "toxicity");
                result.Scores.Hate = ExtractJsonFloat(topScores, "hate");
                result.Scores.Harassment = ExtractJsonFloat(topScores, "harassment");
                result.Scores.Scam = ExtractJsonFloat(topScores, "scam");
                result.Scores.Violence = ExtractJsonFloat(topScores, "violence");
                result.Scores.Fraud = ExtractJsonFloat(topScores, "fraud");
            }

            // Parse policy_decision
            var policyBlock = ExtractJsonObject(json, "policy_decision");
            if (!string.IsNullOrEmpty(policyBlock))
            {
                result.PolicyDecision = ParsePolicyDecision(policyBlock);
            }

            // Parse detected_phrases
            var phrasesArray = ExtractJsonArray(json, "detected_phrases");
            if (phrasesArray != null && phrasesArray.Count > 0)
            {
                result.DetectedPhrases = new List<DetectedPhrase>();
                foreach (var phraseJson in phrasesArray)
                {
                    result.DetectedPhrases.Add(new DetectedPhrase
                    {
                        Text = ExtractJsonString(phraseJson, "text"),
                        Label = ExtractJsonString(phraseJson, "label")
                    });
                }
            }

            // Parse long_text_analysis
            var longTextBlock = ExtractJsonObject(json, "long_text_analysis");
            if (!string.IsNullOrEmpty(longTextBlock))
            {
                result.LongTextAnalysis = ParseLongTextAnalysis(longTextBlock);
            }

            return result;
        }

        private PolicyDecision ParsePolicyDecision(string json)
        {
            var pd = new PolicyDecision
            {
                DecisionValue = ExtractJsonString(json, "decision"),
                RuleId = ExtractJsonString(json, "rule_id"),
                RuleName = ExtractJsonString(json, "rule_name"),
                Reason = ExtractJsonString(json, "reason"),
                Confidence = ExtractJsonFloat(json, "confidence"),
                Severity = ExtractJsonString(json, "severity")
            };

            // triggered_rule
            var ruleBlock = ExtractJsonObject(json, "triggered_rule");
            if (!string.IsNullOrEmpty(ruleBlock))
            {
                pd.TriggeredRule = new TriggeredRule
                {
                    Id = ExtractJsonString(ruleBlock, "id"),
                    Type = ExtractJsonString(ruleBlock, "type"),
                    Category = ExtractJsonString(ruleBlock, "category"),
                    Threshold = ExtractJsonFloat(ruleBlock, "threshold"),
                    ActualValue = ExtractJsonFloat(ruleBlock, "actual_value"),
                    Matched = ExtractJsonString(ruleBlock, "matched")
                };
            }

            // highlights
            var highlightsArray = ExtractJsonArray(json, "highlights");
            if (highlightsArray != null && highlightsArray.Count > 0)
            {
                pd.Highlights = new List<Highlight>();
                foreach (var h in highlightsArray)
                {
                    pd.Highlights.Add(new Highlight
                    {
                        Text = ExtractJsonString(h, "text"),
                        Category = ExtractJsonString(h, "category"),
                        StartIndex = ExtractJsonInt(h, "start_index"),
                        EndIndex = ExtractJsonInt(h, "end_index")
                    });
                }
            }

            return pd;
        }

        private LongTextAnalysis ParseLongTextAnalysis(string json)
        {
            var lta = new LongTextAnalysis
            {
                OverallToxicity = ExtractJsonFloat(json, "overall_toxicity"),
                MaxToxicity = ExtractJsonFloat(json, "max_toxicity"),
                Top3MeanToxicity = ExtractJsonFloat(json, "top3_mean_toxicity"),
                DecisionConfidence = ExtractJsonFloat(json, "decision_confidence"),
                SignalConfidence = ExtractJsonFloat(json, "signal_confidence")
            };

            // sentences
            var sentencesArray = ExtractJsonArray(json, "sentences");
            if (sentencesArray != null)
            {
                lta.Sentences = new List<SentenceAnalysis>();
                foreach (var s in sentencesArray)
                {
                    lta.Sentences.Add(new SentenceAnalysis
                    {
                        Text = ExtractJsonString(s, "text"),
                        Toxicity = ExtractJsonFloat(s, "toxicity"),
                        Flagged = ExtractJsonBool(s, "flagged"),
                        Categories = ExtractJsonStringArray(s, "categories")
                    });
                }
            }

            // highlights
            var ltHighlightsArray = ExtractJsonArray(json, "highlights");
            if (ltHighlightsArray != null)
            {
                lta.Highlights = new List<LongTextHighlight>();
                foreach (var h in ltHighlightsArray)
                {
                    lta.Highlights.Add(new LongTextHighlight
                    {
                        Text = ExtractJsonString(h, "text"),
                        Category = ExtractJsonString(h, "category"),
                        SentenceIndex = ExtractJsonInt(h, "sentence_index")
                    });
                }
            }

            // processing
            var processingBlock = ExtractJsonObject(json, "processing");
            if (!string.IsNullOrEmpty(processingBlock))
            {
                lta.Processing = new ProcessingInfo
                {
                    Mode = ExtractJsonString(processingBlock, "mode"),
                    OriginalCharCount = ExtractJsonInt(processingBlock, "original_char_count"),
                    ProcessedCharCount = ExtractJsonInt(processingBlock, "processed_char_count"),
                    Truncated = ExtractJsonBool(processingBlock, "truncated"),
                    InferenceTimeMs = ExtractJsonFloat(processingBlock, "inference_time_ms")
                };
            }

            return lta;
        }

        #endregion

        #region HTTP

        private async Task<string> SendRequestAsync(
            string method, string path, string jsonBody, ModerationOptions options)
        {
            var url = $"{_baseUrl}{path}";
            Exception lastException = null;

            for (int attempt = 0; attempt <= _maxRetries; attempt++)
            {
                try
                {
                    using (var webRequest = new UnityWebRequest(url, method))
                    {
                        if (!string.IsNullOrEmpty(jsonBody))
                        {
                            var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
                            webRequest.uploadHandler = new UploadHandlerRaw(bodyBytes);
                        }

                        webRequest.downloadHandler = new DownloadHandlerBuffer();
                        webRequest.SetRequestHeader("Content-Type", "application/json");
                        webRequest.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
                        webRequest.SetRequestHeader("User-Agent", $"moderyo-sdk-unity/{SdkVersion}");
                        SetModerationHeaders(webRequest, options);
                        webRequest.timeout = (int)_timeout;

                        var operation = webRequest.SendWebRequest();
                        while (!operation.isDone)
                            await Task.Yield();

                        if (webRequest.result == UnityWebRequest.Result.Success)
                            return webRequest.downloadHandler.text;

                        if (webRequest.result == UnityWebRequest.Result.ConnectionError)
                            throw new NetworkException(webRequest.error);

                        var exception = HandleErrorResponse(webRequest);

                        // Retry on rate limit
                        if (exception is RateLimitException rle && attempt < _maxRetries)
                        {
                            Log($"Rate limited. Retrying in {rle.RetryAfter}s...");
                            await Task.Delay(TimeSpan.FromSeconds(rle.RetryAfter));
                            continue;
                        }

                        // Retry on server error
                        if (webRequest.responseCode >= 500 && attempt < _maxRetries)
                        {
                            var delay = _retryDelay * Mathf.Pow(2, attempt);
                            Log($"Server error {webRequest.responseCode}. Retrying in {delay}s...");
                            await Task.Delay(TimeSpan.FromSeconds(delay));
                            continue;
                        }

                        throw exception;
                    }
                }
                catch (NetworkException) { throw; }
                catch (ModeryoException) { throw; }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (attempt < _maxRetries)
                    {
                        var delay = _retryDelay * Mathf.Pow(2, attempt);
                        await Task.Delay(TimeSpan.FromSeconds(delay));
                        continue;
                    }
                }
            }

            throw new ModeryoException($"Request failed: {lastException?.Message}", lastException);
        }

        private ModeryoException HandleErrorResponse(UnityWebRequest webRequest)
        {
            var statusCode = (int)webRequest.responseCode;
            var body = webRequest.downloadHandler?.text ?? "";
            var message = ExtractJsonString(body, "message")
                       ?? ExtractJsonString(body, "error")
                       ?? webRequest.error
                       ?? "Unknown error";

            return statusCode switch
            {
                400 => new ValidationException(message),
                401 => new AuthenticationException(message),
                402 => new QuotaExceededException(message),
                422 => new ValidationException(message),
                429 => new RateLimitException(message, ExtractRetryAfter(webRequest)),
                _   => new ModeryoException(message, "API_ERROR", statusCode)
            };
        }

        #endregion

        #region Offline Mode

        private ModerationResult HandleOfflineMode(string input)
        {
            Log($"Offline mode: {_offlineMode}");
            return _offlineMode switch
            {
                OfflineMode.AllowAll      => CreateOfflineResult(false, "ALLOW"),
                OfflineMode.BlockAll      => CreateOfflineResult(true, "BLOCK"),
                OfflineMode.UseLocalFilter => ApplyLocalFilter(input),
                _                         => CreateOfflineResult(false, "ALLOW")
            };
        }

        private ModerationResult CreateOfflineResult(bool flagged, string decision)
        {
            return new ModerationResult
            {
                Id = GenerateId(),
                Model = "offline",
                Flagged = flagged,
                Mode = ProcessingMode.Degraded,
                Categories = new Categories(),
                CategoryScores = new CategoryScores(),
                Scores = new SimplifiedScores(),
                PolicyDecision = new PolicyDecision
                {
                    DecisionValue = decision,
                    Reason = "Offline mode — API unavailable"
                }
            };
        }

        private ModerationResult ApplyLocalFilter(string input)
        {
            var words = input.Split(
                new[] { ' ', '\t', '\n', '\r', '.', ',', '!', '?' },
                StringSplitOptions.RemoveEmptyEntries);

            foreach (var word in words)
            {
                if (_localFilterWords.Contains(word))
                {
                    var categories = new Categories();
                    categories[CategoryKeys.Harassment] = true;
                    var scores = new CategoryScores();
                    scores[CategoryKeys.Harassment] = 1f;

                    return new ModerationResult
                    {
                        Id = GenerateId(),
                        Model = "local-filter",
                        Flagged = true,
                        Mode = ProcessingMode.Degraded,
                        Categories = categories,
                        CategoryScores = scores,
                        Scores = new SimplifiedScores { Toxicity = 1f },
                        PolicyDecision = new PolicyDecision
                        {
                            DecisionValue = "BLOCK",
                            Reason = "Blocked by local word filter"
                        }
                    };
                }
            }

            return CreateOfflineResult(false, "ALLOW");
        }

        #endregion

        #region JSON Helpers

        private static string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str.Replace("\\", "\\\\")
                      .Replace("\"", "\\\"")
                      .Replace("\n", "\\n")
                      .Replace("\r", "\\r")
                      .Replace("\t", "\\t");
        }

        private static string ExtractJsonString(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var pattern = $"\"{Regex.Escape(key)}\"\\s*:\\s*\"([^\"\\\\]*(?:\\\\.[^\"\\\\]*)*)\"";
            var match = Regex.Match(json, pattern);
            return match.Success ? match.Groups[1].Value.Replace("\\\"", "\"").Replace("\\n", "\n") : null;
        }

        private static bool ExtractJsonBool(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return false;
            var pattern = $"\"{Regex.Escape(key)}\"\\s*:\\s*(true|false)";
            var match = Regex.Match(json, pattern, RegexOptions.IgnoreCase);
            return match.Success && match.Groups[1].Value.ToLower() == "true";
        }

        private static float ExtractJsonFloat(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return 0f;
            var pattern = $"\"{Regex.Escape(key)}\"\\s*:\\s*(-?[\\d.eE+-]+)";
            var match = Regex.Match(json, pattern);
            if (match.Success && float.TryParse(match.Groups[1].Value,
                NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                return value;
            return 0f;
        }

        private static int ExtractJsonInt(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return 0;
            var pattern = $"\"{Regex.Escape(key)}\"\\s*:\\s*(-?\\d+)";
            var match = Regex.Match(json, pattern);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var value))
                return value;
            return 0;
        }

        private static string ExtractJsonObject(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var startPattern = $"\"{Regex.Escape(key)}\"\\s*:\\s*{{";
            var match = Regex.Match(json, startPattern);
            if (!match.Success) return null;

            var startIndex = match.Index + match.Length - 1;
            var depth = 1;
            var endIndex = startIndex + 1;
            var inString = false;
            var escape = false;

            while (endIndex < json.Length && depth > 0)
            {
                var c = json[endIndex];
                if (escape) { escape = false; }
                else if (c == '\\') { escape = true; }
                else if (c == '"') { inString = !inString; }
                else if (!inString)
                {
                    if (c == '{') depth++;
                    else if (c == '}') depth--;
                }
                endIndex++;
            }

            return json.Substring(startIndex, endIndex - startIndex);
        }

        /// <summary>Extract a JSON array and return individual element strings</summary>
        private static List<string> ExtractJsonArray(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var startPattern = $"\"{Regex.Escape(key)}\"\\s*:\\s*\\[";
            var match = Regex.Match(json, startPattern);
            if (!match.Success) return null;

            var arrayStart = match.Index + match.Length - 1;
            var depth = 1;
            var pos = arrayStart + 1;
            var inString = false;
            var escape = false;

            // Find array end
            while (pos < json.Length && depth > 0)
            {
                var c = json[pos];
                if (escape) { escape = false; }
                else if (c == '\\') { escape = true; }
                else if (c == '"') { inString = !inString; }
                else if (!inString)
                {
                    if (c == '[') depth++;
                    else if (c == ']') depth--;
                }
                pos++;
            }

            var arrayContent = json.Substring(arrayStart + 1, pos - arrayStart - 2).Trim();
            if (string.IsNullOrEmpty(arrayContent)) return new List<string>();

            // Split into top-level elements
            var elements = new List<string>();
            var elementStart = 0;
            var braceDepth = 0;
            var bracketDepth = 0;
            inString = false;
            escape = false;

            for (int i = 0; i < arrayContent.Length; i++)
            {
                var c = arrayContent[i];
                if (escape) { escape = false; continue; }
                if (c == '\\') { escape = true; continue; }
                if (c == '"') { inString = !inString; continue; }
                if (inString) continue;

                if (c == '{') braceDepth++;
                else if (c == '}') braceDepth--;
                else if (c == '[') bracketDepth++;
                else if (c == ']') bracketDepth--;
                else if (c == ',' && braceDepth == 0 && bracketDepth == 0)
                {
                    elements.Add(arrayContent.Substring(elementStart, i - elementStart).Trim());
                    elementStart = i + 1;
                }
            }

            var last = arrayContent.Substring(elementStart).Trim();
            if (!string.IsNullOrEmpty(last))
                elements.Add(last);

            return elements;
        }

        /// <summary>Extract a JSON string array like ["a","b","c"]</summary>
        private static List<string> ExtractJsonStringArray(string json, string key)
        {
            var elements = ExtractJsonArray(json, key);
            if (elements == null) return null;

            var result = new List<string>();
            foreach (var el in elements)
            {
                var trimmed = el.Trim().Trim('"');
                if (!string.IsNullOrEmpty(trimmed))
                    result.Add(trimmed);
            }
            return result;
        }

        #endregion

        #region Utilities

        private float ExtractRetryAfter(UnityWebRequest webRequest)
        {
            var retryAfter = webRequest.GetResponseHeader("Retry-After");
            if (float.TryParse(retryAfter, out var seconds))
                return seconds;
            return 60f;
        }

        private static string GenerateId()
        {
            return $"modr-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        }

        private void Log(string message)
        {
            if (_enableLogging)
                Debug.Log($"[Moderyo] {message}");
        }

        #endregion
    }

    // ───────────────────────── Coroutine Operation ─────────────────────────

    /// <summary>
    /// CustomYieldInstruction that wraps a coroutine-based moderation call.
    /// Can be yielded in a coroutine: yield return client.Moderate("text");
    /// </summary>
    public class ModerationOperation : CustomYieldInstruction
    {
        private readonly ModeryoClient _client;
        private readonly ModerationRequest _request;
        private readonly ModerationOptions _options;
        private bool _isDone;
        private ModerationResult _result;
        private ModeryoException _error;

        public override bool keepWaiting => !_isDone;
        public bool IsError => _error != null;
        public ModeryoException Error => _error;
        public ModerationResult Result => _result;

        internal ModerationOperation(ModeryoClient client, ModerationRequest request, ModerationOptions options)
        {
            _client = client;
            _request = request;
            _options = options;
            CoroutineRunner.Instance.StartCoroutine(Execute());
        }

        private IEnumerator Execute()
        {
            yield return _client.ModerateCoroutine(
                _request, _options,
                result => { _result = result; _isDone = true; },
                error  => { _error = error;   _isDone = true; }
            );
        }
    }

    /// <summary>Singleton MonoBehaviour for running coroutines</summary>
    internal class CoroutineRunner : MonoBehaviour
    {
        private static CoroutineRunner _instance;

        public static CoroutineRunner Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[Moderyo CoroutineRunner]");
                    _instance = go.AddComponent<CoroutineRunner>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }
    }
}
