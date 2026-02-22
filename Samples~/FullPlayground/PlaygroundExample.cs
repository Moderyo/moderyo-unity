using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Moderyo;

/// <summary>
/// Full playground demonstrating all Moderyo Unity SDK v0.2.0 features.
/// Attach to a GameObject and call RunAllExamples() or individual methods.
/// </summary>
public class PlaygroundExample : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private ModeryoConfig config;

    private ModeryoClient _client;

    private void Awake()
    {
        _client = new ModeryoClient(config);
    }

    /// <summary>Run all playground examples sequentially</summary>
    public async void RunAllExamples()
    {
        Debug.Log("=== Moderyo Unity SDK v2.0.7 Playground ===");
        Debug.Log($"Total categories: {ModeryoClient.ALL_CATEGORIES.Length}");

        await Example1_BasicModeration();
        await Example2_PolicyDecision();
        await Example3_DetectedPhrases();
        await Example4_SkipFlags();
        await Example5_ModeAndRisk();
        await Example6_LongText();
        await Example7_BatchModeration();
        await Example8_CategoryInspection();
        await Example9_HealthCheck();

        Debug.Log("=== All examples complete ===");
    }

    // ── 1. Basic moderation ──

    private async System.Threading.Tasks.Task Example1_BasicModeration()
    {
        Debug.Log("\n--- Example 1: Basic Moderation ---");

        var result = await _client.ModerateAsync("Hello, how are you today?");
        Debug.Log($"Allowed: {result.IsAllowed()}, Flagged: {result.Flagged}");
        Debug.Log($"Decision: {result.Action}");

        if (result.Scores != null)
        {
            Debug.Log($"Toxicity: {result.Scores.Toxicity:P1}");
            Debug.Log($"Hate: {result.Scores.Hate:P1}");
            Debug.Log($"Violence: {result.Scores.Violence:P1}");
        }
    }

    // ── 2. Policy decision details ──

    private async System.Threading.Tasks.Task Example2_PolicyDecision()
    {
        Debug.Log("\n--- Example 2: Policy Decision ---");

        var result = await _client.ModerateAsync("I will kill you and your family");

        if (result.PolicyDecision != null)
        {
            var pd = result.PolicyDecision;
            Debug.Log($"Decision: {pd.DecisionValue}");
            Debug.Log($"Rule: {pd.RuleName} (ID: {pd.RuleId})");
            Debug.Log($"Reason: {pd.Reason}");
            Debug.Log($"Confidence: {pd.Confidence:P0}");
            Debug.Log($"Severity: {pd.Severity}");

            if (pd.TriggeredRule != null)
            {
                Debug.Log($"Triggered: category={pd.TriggeredRule.Category}, " +
                          $"threshold={pd.TriggeredRule.Threshold}, " +
                          $"actual={pd.TriggeredRule.ActualValue}");
            }

            if (pd.Highlights != null)
            {
                foreach (var h in pd.Highlights)
                    Debug.Log($"Highlight: \"{h.Text}\" [{h.Category}] at {h.StartIndex}-{h.EndIndex}");
            }
        }

        Debug.Log($"Triggered categories: {string.Join(", ", result.GetTriggeredCategories())}");
    }

    // ── 3. Detected phrases ──

    private async System.Threading.Tasks.Task Example3_DetectedPhrases()
    {
        Debug.Log("\n--- Example 3: Detected Phrases ---");

        var result = await _client.ModerateAsync("You stupid idiot, go to hell");

        if (result.DetectedPhrases != null)
        {
            Debug.Log($"Found {result.DetectedPhrases.Count} detected phrases:");
            foreach (var phrase in result.DetectedPhrases)
                Debug.Log($"  [{phrase.Label}] \"{phrase.Text}\"");
        }
    }

    // ── 4. Skip flags ──

    private async System.Threading.Tasks.Task Example4_SkipFlags()
    {
        Debug.Log("\n--- Example 4: Skip Flags ---");

        var request = new ModerationRequest
        {
            Input = "damn this is annoying as hell",
            SkipProfanity = true,  // Skip profanity detection
            SkipThreat = true      // Skip threat detection
        };

        var result = await _client.ModerateAsync(request);
        Debug.Log($"With skip flags — Blocked: {result.IsBlocked()}, Flagged: {result.Flagged}");
    }

    // ── 5. Mode and risk options ──

    private async System.Threading.Tasks.Task Example5_ModeAndRisk()
    {
        Debug.Log("\n--- Example 5: Mode & Risk Options ---");

        // Shadow mode (log-only, no blocking)
        var shadowOptions = new ModerationOptions
        {
            Mode = "shadow",
            Risk = "aggressive"
        };
        var result1 = await _client.ModerateAsync("some test text", shadowOptions);
        Debug.Log($"Shadow mode — Decision: {result1.PolicyDecision?.DecisionValue}");

        // Conservative mode (strict blocking)
        var conservativeOptions = new ModerationOptions
        {
            Mode = "enforce",
            Risk = "conservative",
            PlayerId = "player_42"
        };
        var result2 = await _client.ModerateAsync("borderline content here", conservativeOptions);
        Debug.Log($"Conservative — Decision: {result2.PolicyDecision?.DecisionValue}");
    }

    // ── 6. Long text mode ──

    private async System.Threading.Tasks.Task Example6_LongText()
    {
        Debug.Log("\n--- Example 6: Long Text Mode ---");

        var longText = "This is a long piece of text that could be a game story, " +
                       "NPC dialogue, or user-generated content. " +
                       "The moderation engine will analyze it sentence by sentence. " +
                       string.Join(" ", new string[20].Select(_ => "More content here."));

        var request = new ModerationRequest
        {
            Input = longText,
            LongTextMode = true,
            LongTextThreshold = 0.3f
        };

        var result = await _client.ModerateAsync(request);
        Debug.Log($"Long text — Blocked: {result.IsBlocked()}");

        if (result.LongTextAnalysis != null)
        {
            var lta = result.LongTextAnalysis;
            Debug.Log($"Overall toxicity: {lta.OverallToxicity:P1}");
            Debug.Log($"Max toxicity: {lta.MaxToxicity:P1}");
            Debug.Log($"Sentences analyzed: {lta.Sentences?.Count ?? 0}");

            if (lta.Processing != null)
            {
                Debug.Log($"Chars: {lta.Processing.OriginalCharCount} → {lta.Processing.ProcessedCharCount}");
                Debug.Log($"Inference: {lta.Processing.InferenceTimeMs}ms");
            }
        }
    }

    // ── 7. Batch moderation ──

    private async System.Threading.Tasks.Task Example7_BatchModeration()
    {
        Debug.Log("\n--- Example 7: Batch Moderation ---");

        var messages = new[]
        {
            "Hello everyone!",
            "Great game today!",
            "I will destroy you",
            "Let's play together"
        };

        var batch = await _client.ModerateBatchAsync(messages);
        Debug.Log($"Total: {batch.Total}, Blocked: {batch.BlockedCount}, Flagged: {batch.FlaggedCount}");
        Debug.Log($"Has blocked: {batch.HasBlocked()}");

        var blocked = batch.GetBlocked();
        foreach (var r in blocked)
            Debug.Log($"  Blocked: {r.Explanation}");
    }

    // ── 8. Category inspection ──

    private async System.Threading.Tasks.Task Example8_CategoryInspection()
    {
        Debug.Log("\n--- Example 8: Category Inspection ---");

        var result = await _client.ModerateAsync("violent threatening content");

        // Check specific categories
        Debug.Log($"violence: {result.Categories[CategoryKeys.Violence]}");
        Debug.Log($"hate: {result.Categories[CategoryKeys.Hate]}");
        Debug.Log($"child_grooming: {result.Categories[CategoryKeys.ChildGrooming]}");

        // Check scores
        Debug.Log($"violence score: {result.CategoryScores[CategoryKeys.Violence]:P1}");

        // Get highest risk
        var highest = result.CategoryScores.GetHighest();
        if (highest.HasValue)
            Debug.Log($"Highest: {highest.Value.Key} = {highest.Value.Value:P1}");

        // Get all scores above threshold
        var risky = result.CategoryScores.Above(0.3f);
        Debug.Log($"Categories above 30%: {risky.Count}");
        foreach (var kvp in risky)
            Debug.Log($"  {kvp.Key}: {kvp.Value:P1}");
    }

    // ── 9. Health check ──

    private async System.Threading.Tasks.Task Example9_HealthCheck()
    {
        Debug.Log("\n--- Example 9: Health Check ---");

        var healthy = await _client.HealthCheckAsync();
        Debug.Log($"API healthy: {healthy}");
    }

    // ── Coroutine variant ──

    /// <summary>Same as Example1 but using coroutine API</summary>
    public void RunCoroutineExample()
    {
        StartCoroutine(CoroutineModeration());
    }

    private System.Collections.IEnumerator CoroutineModeration()
    {
        Debug.Log("\n--- Coroutine Example ---");

        var options = new ModerationOptions
        {
            Mode = "enforce",
            Risk = "balanced",
            PlayerId = "player_99"
        };

        var operation = _client.Moderate("test message from coroutine", options);
        yield return operation;

        if (operation.IsError)
        {
            Debug.LogError($"Error: {operation.Error.Message}");
            yield break;
        }

        var result = operation.Result;
        Debug.Log($"Coroutine result — Decision: {result.Action}, Flagged: {result.Flagged}");
    }
}
