using UnityEngine;
using Moderyo;

/// <summary>
/// Coroutine-based chat moderation example.
/// Compatible with all Unity versions (no async/await needed).
/// </summary>
public class CoroutineChatExample : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private ModeryoConfig config;

    private ModeryoClient _client;

    private void Awake()
    {
        _client = new ModeryoClient(config);
    }

    public void SendChatMessage(string message)
    {
        StartCoroutine(ModerateAndSend(message));
    }

    private System.Collections.IEnumerator ModerateAndSend(string message)
    {
        // Use coroutine API — works on all platforms
        var operation = _client.Moderate(message);
        yield return operation;

        if (operation.IsError)
        {
            Debug.LogError($"Moderation failed: {operation.Error.Message}");
            yield break;
        }

        var result = operation.Result;

        if (result.ShouldBlock())
        {
            Debug.Log($"Message blocked: {result.Explanation}");
            yield break;
        }

        if (result.NeedsReview())
        {
            Debug.Log($"Message flagged: {string.Join(", ", result.GetTriggeredCategories())}");
        }

        Debug.Log($"Message allowed: {message}");
    }

    /// <summary>Coroutine with ModerationOptions</summary>
    public void SendGamingMessage(string message, string playerId)
    {
        StartCoroutine(ModerateGaming(message, playerId));
    }

    private System.Collections.IEnumerator ModerateGaming(string message, string playerId)
    {
        var options = new ModerationOptions
        {
            Mode = "enforce",
            Risk = "conservative",
            PlayerId = playerId
        };

        var operation = _client.Moderate(message, options);
        yield return operation;

        if (operation.IsError)
        {
            Debug.LogError($"Error: {operation.Error.Message}");
            yield break;
        }

        var result = operation.Result;
        Debug.Log($"Decision: {result.Action}, Flagged: {result.Flagged}");

        if (result.DetectedPhrases != null)
        {
            foreach (var p in result.DetectedPhrases)
                Debug.Log($"  [{p.Label}] {p.Text}");
        }
    }
}
