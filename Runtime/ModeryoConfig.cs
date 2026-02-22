using UnityEngine;

namespace Moderyo
{
    /// <summary>
    /// Configuration for Moderyo SDK
    /// </summary>
    [CreateAssetMenu(fileName = "ModeryoConfig", menuName = "Moderyo/Config", order = 1)]
    public class ModeryoConfig : ScriptableObject
    {
        [Header("API Settings")]
        [Tooltip("Your Moderyo API key")]
        [SerializeField] private string apiKey;
        
        [Tooltip("API base URL")]
        [SerializeField] private string baseUrl = "https://api.moderyo.com";
        
        [Header("Request Settings")]
        [Tooltip("Request timeout in seconds")]
        [SerializeField] private float timeout = 30f;
        
        [Tooltip("Maximum retry attempts")]
        [SerializeField] private int maxRetries = 3;
        
        [Tooltip("Base retry delay in seconds")]
        [SerializeField] private float retryDelay = 1f;
        
        [Header("Behavior")]
        [Tooltip("Default model for moderation")]
        [SerializeField] private string defaultModel = "omni-moderation-latest";
        
        [Tooltip("Enable debug logging")]
        [SerializeField] private bool enableLogging = false;
        
        [Tooltip("Offline mode behavior")]
        [SerializeField] private OfflineMode offlineMode = OfflineMode.AllowAll;

        [Header("Moderation Defaults")]
        [Tooltip("Default mode: enforce or shadow")]
        [SerializeField] private string defaultMode = "";

        [Tooltip("Default risk level: conservative, balanced, or aggressive")]
        [SerializeField] private string defaultRisk = "";
        
        [Header("Local Filter (Fallback)")]
        [Tooltip("Words to filter locally when offline")]
        [SerializeField] private string[] localFilterWords = new string[0];

        // Properties
        public string ApiKey => apiKey;
        public string BaseUrl => baseUrl;
        public float Timeout => timeout;
        public int MaxRetries => maxRetries;
        public float RetryDelay => retryDelay;
        public string DefaultModel => defaultModel;
        public bool EnableLogging => enableLogging;
        public OfflineMode OfflineMode => offlineMode;
        public string DefaultMode => defaultMode;
        public string DefaultRisk => defaultRisk;
        public string[] LocalFilterWords => localFilterWords;

        /// <summary>
        /// Validate configuration
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(baseUrl);
        }

        /// <summary>
        /// Create config from values
        /// </summary>
        public static ModeryoConfig Create(string apiKey, string baseUrl = null)
        {
            var config = CreateInstance<ModeryoConfig>();
            config.apiKey = apiKey;
            if (!string.IsNullOrEmpty(baseUrl))
            {
                config.baseUrl = baseUrl;
            }
            return config;
        }
    }

    /// <summary>
    /// Behavior when API is unavailable
    /// </summary>
    public enum OfflineMode
    {
        /// <summary>Allow all content when offline</summary>
        AllowAll,
        
        /// <summary>Block all content when offline</summary>
        BlockAll,
        
        /// <summary>Use local word filter when offline</summary>
        UseLocalFilter,
        
        /// <summary>Queue for later moderation</summary>
        Queue
    }
}
