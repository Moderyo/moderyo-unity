using UnityEngine;
using UnityEngine.UI;
using Moderyo;
using Moderyo.Components;

/// <summary>
/// Example using the ChatFilter component for UI-integrated moderation.
/// </summary>
public class ChatFilterExample : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private InputField chatInput;
    [SerializeField] private Text chatDisplay;
    [SerializeField] private Text statusText;

    [Header("Moderation")]
    [SerializeField] private ChatFilter chatFilter;

    private void Start()
    {
        // Configure the chat filter with player ID and risk level
        chatFilter
            .WithPlayerId("player_123")
            .WithMode("enforce")
            .WithRisk("balanced");

        // Register event handlers
        chatFilter.OnMessageApproved.AddListener(OnMessageApproved);
        chatFilter.OnMessageBlocked.AddListener(OnMessageBlocked);
        chatFilter.OnMessageFlagged.AddListener(OnMessageFlagged);
        chatFilter.OnModerationError.AddListener(OnModerationError);
    }

    public void OnSendButtonClick()
    {
        var message = chatInput.text;
        if (string.IsNullOrEmpty(message)) return;

        statusText.text = "Moderating...";

        chatFilter.FilterMessage(message, result =>
        {
            if (result.IsApproved)
            {
                AddToChatDisplay(result.FilteredMessage);
                chatInput.text = "";
                statusText.text = "";
            }
            else
            {
                statusText.text = "Message blocked";
            }
        });
    }

    private void OnMessageApproved(string message)
    {
        Debug.Log($"Message approved: {message}");
    }

    private void OnMessageBlocked(string message, ModerationResult result)
    {
        Debug.Log($"Message blocked: {message}");
        Debug.Log($"Triggered: {string.Join(", ", result.GetTriggeredCategories())}");

        if (result.PolicyDecision != null)
            Debug.Log($"Reason: {result.PolicyDecision.Reason}");
    }

    private void OnMessageFlagged(string message, ModerationResult result)
    {
        Debug.Log($"Message flagged for review: {message}");
    }

    private void OnModerationError(ModeryoException error)
    {
        Debug.LogError($"Moderation error: {error.Message}");
        statusText.text = "Moderation unavailable";
    }

    private void AddToChatDisplay(string message)
    {
        chatDisplay.text += $"\n[You]: {message}";
    }
}
