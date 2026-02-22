using UnityEngine;
using Moderyo;

/// <summary>
/// Basic async chat moderation example.
/// Shows how to moderate messages before sending them to the server.
/// </summary>
public class BasicChatExample : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private ModeryoConfig config;

    private ModeryoClient _client;

    private void Awake()
    {
        _client = new ModeryoClient(config);
        Debug.Log($"Moderyo SDK initialized — {ModeryoClient.ALL_CATEGORIES.Length} categories supported");
    }

    /// <summary>Send a chat message with moderation check</summary>
    public async void SendChatMessage(string message)
    {
        try
        {
            // Basic moderation
            var result = await _client.ModerateAsync(message);

            if (result.IsBlocked())
            {
                Debug.Log($"Message blocked: {result.Explanation}");
                ShowBlockedMessage(result);
            }
            else if (result.IsFlagged())
            {
                Debug.Log("Message flagged for review");
                var triggered = result.GetTriggeredCategories();
                Debug.Log($"Triggered: {string.Join(", ", triggered)}");
                SendMessageToServer(message, flagged: true);
            }
            else
            {
                Debug.Log("Message allowed");
                SendMessageToServer(message);
            }

            // Check simplified scores
            if (result.Scores != null && result.Scores.Toxicity > 0.5f)
            {
                Debug.LogWarning($"High toxicity score: {result.Scores.Toxicity:P0}");
            }

            // Check detected phrases
            if (result.DetectedPhrases != null)
            {
                foreach (var phrase in result.DetectedPhrases)
                    Debug.Log($"Detected [{phrase.Label}]: \"{phrase.Text}\"");
            }
        }
        catch (RateLimitException ex)
        {
            Debug.LogWarning($"Rate limited. Retry after {ex.RetryAfter} seconds");
        }
        catch (NetworkException)
        {
            Debug.LogWarning("Network error — using offline mode");
        }
        catch (ModeryoException ex)
        {
            Debug.LogError($"Moderation error: {ex.Message}");
        }
    }

    /// <summary>Send with mode/risk options (gaming scenario)</summary>
    public async void SendGamingMessage(string message, string playerId)
    {
        var options = new ModerationOptions
        {
            Mode = "enforce",
            Risk = "conservative",
            PlayerId = playerId
        };

        try
        {
            var result = await _client.ModerateAsync(message, options);
            if (result.IsAllowed())
                SendMessageToServer(message);
            else
                ShowBlockedMessage(result);
        }
        catch (ModeryoException ex)
        {
            Debug.LogError($"Moderation error: {ex.Message}");
        }
    }

    private void ShowBlockedMessage(ModerationResult result)
    {
        // Show UI feedback to the player
        Debug.Log($"Policy decision: {result.PolicyDecision?.DecisionValue}");
        Debug.Log($"Reason: {result.PolicyDecision?.Reason}");
    }

    private void SendMessageToServer(string message, bool flagged = false)
    {
        // Send to your game server
    }
}
