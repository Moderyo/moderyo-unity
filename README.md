# Moderyo Unity SDK

Official Unity client library for the Moderyo Content Moderation API — v2.0.7.

[![Unity](https://img.shields.io/badge/Unity-2021.3%2B-black.svg)](https://unity.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## Features

- ✅ Unity 2021.3 LTS and newer
- ✅ Async/await (Task-based) and Coroutine APIs
- ✅ WebGL, Mobile, Desktop, Console support
- ✅ Automatic retry with exponential backoff
- ✅ Rate limiting handling
- ✅ Editor tools for testing
- ✅ Offline mode / fallback support

## Installation

### Via Unity Package Manager (Git URL)

1. Open Window > Package Manager
2. Click "+" > "Add package from git URL"
3. Enter: `https://github.com/Moderyo/moderyo-unity.git`

### Via .unitypackage

Download the latest `.unitypackage` from [Releases](https://github.com/Moderyo/moderyo-unity/releases)

### Manual Installation

Copy the `Moderyo` folder to your project's `Assets/Plugins/` directory.

## Quick Start

```csharp
using Moderyo;
using UnityEngine;

public class ChatManager : MonoBehaviour
{
    private ModeryoClient _moderyo;

    void Start()
    {
        _moderyo = new ModeryoClient("your-api-key");
    }

    public async void SendMessage(string message)
    {
        var result = await _moderyo.ModerateAsync(message);
        
        if (result.Action == Decision.Block)
        {
            Debug.Log("Message blocked!");
            return;
        }
        
        // Send the message...
    }
}
```

## Configuration

### Via Script

```csharp
var client = new ModeryoClient(new ModeryoConfig
{
    ApiKey = "your-api-key",
    BaseUrl = "https://api.moderyo.com",
    Timeout = 30f,
    MaxRetries = 3,
    EnableLogging = true
});
```

### Via ScriptableObject

1. Create: Right-click in Project > Create > Moderyo > Config
2. Fill in your API key
3. Reference in your script:

```csharp
public class GameManager : MonoBehaviour
{
    [SerializeField] private ModeryoConfig config;
    private ModeryoClient _moderyo;

    void Awake()
    {
        _moderyo = new ModeryoClient(config);
    }
}
```

## Usage Examples

### Async/Await (Recommended)

```csharp
using Moderyo;
using Cysharp.Threading.Tasks;

public class ChatModerator : MonoBehaviour
{
    private ModeryoClient _moderyo;

    async UniTaskVoid Start()
    {
        _moderyo = new ModeryoClient("your-api-key");
    }

    public async UniTask<bool> ValidateMessage(string message)
    {
        try
        {
            var result = await _moderyo.ModerateAsync(message);
            return result.Action != Decision.Block;
        }
        catch (ModeryoException ex)
        {
            Debug.LogWarning($"Moderation failed: {ex.Message}");
            return true; // Allow on error (fail-open)
        }
    }
}
```

### Coroutine-Based (Legacy)

```csharp
using Moderyo;
using UnityEngine;
using System.Collections;

public class LegacyChatModerator : MonoBehaviour
{
    private ModeryoClient _moderyo;

    void Start()
    {
        _moderyo = new ModeryoClient("your-api-key");
    }

    public void ValidateMessage(string message, System.Action<bool> callback)
    {
        StartCoroutine(ValidateMessageCoroutine(message, callback));
    }

    private IEnumerator ValidateMessageCoroutine(string message, System.Action<bool> callback)
    {
        var request = _moderyo.Moderate(message);
        
        yield return request;
        
        if (request.IsError)
        {
            Debug.LogWarning($"Moderation failed: {request.Error}");
            callback(true); // Allow on error
            yield break;
        }
        
        callback(request.Result.Action != Decision.Block);
    }
}
```

### With Player Context

```csharp
var result = await _moderyo.ModerateAsync(new ModerationRequest
{
    Content = message,
    Context = new ModerationContext
    {
        UserId = PlayerManager.CurrentPlayer.Id,
        ContentType = "game_chat",
        Platform = Application.platform.ToString(),
        Language = Application.systemLanguage.ToString()
    }
});
```

### Batch Processing

```csharp
var messages = new[] { "msg1", "msg2", "msg3" };
var results = await _moderyo.ModerateBatchAsync(messages);

foreach (var result in results.Results)
{
    if (result.Action == Decision.Block)
    {
        Debug.Log($"Blocked: {result.Id}");
    }
}
```

## Game-Specific Features

### Chat Filter Component

```csharp
using Moderyo;
using Moderyo.Components;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChatUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Button sendButton;
    [SerializeField] private ChatFilter chatFilter;

    void Start()
    {
        sendButton.onClick.AddListener(OnSendClicked);
    }

    async void OnSendClicked()
    {
        var message = inputField.text;
        
        var filterResult = await chatFilter.FilterAsync(message);
        
        switch (filterResult.Action)
        {
            case Decision.Allow:
                SendToServer(message);
                break;
                
            case Decision.Flag:
                // Show warning but allow
                ShowWarning("Your message may violate community guidelines.");
                SendToServer(message);
                break;
                
            case Decision.Block:
                ShowError("Message blocked. Please follow community guidelines.");
                break;
        }
        
        inputField.text = "";
    }
}
```

### Player Report System

```csharp
using Moderyo;

public class ReportSystem : MonoBehaviour
{
    private ModeryoClient _moderyo;

    public async UniTask<ReportResult> ReportPlayer(
        string reporterId,
        string reportedId,
        string reason,
        string evidence)
    {
        var result = await _moderyo.ModerateAsync(new ModerationRequest
        {
            Content = evidence,
            Context = new ModerationContext
            {
                UserId = reporterId,
                ContentType = "player_report",
                AdditionalData = new Dictionary<string, object>
                {
                    ["reported_player"] = reportedId,
                    ["reason"] = reason
                }
            }
        });

        return new ReportResult
        {
            ShouldAutoAction = result.Action == Decision.Block,
            Severity = result.Risk,
            Categories = GetTriggeredCategories(result)
        };
    }
}
```

### Username Validation

```csharp
public async UniTask<UsernameValidationResult> ValidateUsername(string username)
{
    var result = await _moderyo.ModerateAsync(new ModerationRequest
    {
        Content = username,
        Context = new ModerationContext
        {
            ContentType = "username"
        }
    });

    return new UsernameValidationResult
    {
        IsValid = result.Action == Decision.Allow,
        Reason = result.Explanation
    };
}
```

## Offline Mode / Fallback

```csharp
var client = new ModeryoClient(new ModeryoConfig
{
    ApiKey = "your-api-key",
    OfflineMode = OfflineMode.AllowAll, // or BlockAll, UseLocalFilter
    LocalFilterWords = new[] { "badword1", "badword2" }
});
```

## Error Handling

```csharp
try
{
    var result = await _moderyo.ModerateAsync(message);
}
catch (AuthenticationException)
{
    Debug.LogError("Invalid API key!");
}
catch (RateLimitException ex)
{
    Debug.LogWarning($"Rate limited. Retry in {ex.RetryAfter}s");
    await UniTask.Delay(TimeSpan.FromSeconds(ex.RetryAfter));
}
catch (NetworkException)
{
    Debug.LogWarning("Network error - using fallback");
    // Use local filter or allow
}
catch (ModeryoException ex)
{
    Debug.LogError($"Moderation error: {ex.Message}");
}
```

## Editor Tools

### Test Window

Window > Moderyo > Test Panel

- Test moderation directly in editor
- View category scores
- Debug API responses

### Inspector Integration

The `ChatFilter` component shows real-time moderation results in the Inspector during Play mode.

## Performance Tips

1. **Batch requests** when possible
2. **Cache results** for identical content
3. Use **async/await** over coroutines
4. Enable **connection pooling** for high-frequency games
5. Consider **client-side pre-filtering** for obvious violations

## Platform Notes

### WebGL
- Uses UnityWebRequest (no native sockets)
- Consider increased timeout for slow connections

### Mobile
- Handles network transitions gracefully
- Respects low-power mode

### Console
- Platform-specific TLS handling
- Contact support for console-specific setup

## Requirements

- Unity 2021.3 LTS or newer
- .NET Standard 2.1 or .NET 4.x
- (Optional) UniTask for async/await

## Links

- **Documentation:** [docs.moderyo.com/sdk/unity](https://docs.moderyo.com/sdk/unity)
- **GitHub:** [github.com/Moderyo/moderyo-unity](https://github.com/Moderyo/moderyo-unity)
- **Website:** [moderyo.com](https://moderyo.com)

## License

MIT License - see [LICENSE](LICENSE) for details.
