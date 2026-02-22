using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;

namespace Moderyo.Components
{
    /// <summary>
    /// Drop-in chat filter component for Unity UI.
    /// Moderates messages before sending via both coroutine and async APIs.
    /// </summary>
    [AddComponentMenu("Moderyo/Chat Filter")]
    public class ChatFilter : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Configuration")]
        [SerializeField] private ModeryoConfig config;
        [SerializeField] private bool useSharedClient = true;

        [Header("Input")]
        [SerializeField] private InputField inputField;
        [SerializeField] private bool submitOnEnter = true;

        [Header("Filtering Options")]
        [SerializeField] private float throttleDelay = 0.5f;
        [SerializeField] private bool blockOnError = true;
        [SerializeField] private bool censorInsteadOfBlock = false;

        [Header("Events")]
        public UnityEvent<string> OnMessageApproved;
        public UnityEvent<string, ModerationResult> OnMessageBlocked;
        public UnityEvent<string, ModerationResult> OnMessageFlagged;
        public UnityEvent<ModeryoException> OnModerationError;

        #endregion

        #region Private Fields

        private ModeryoClient _client;
        private static ModeryoClient _sharedClient;
        private float _lastModerationTime;
        private bool _isProcessing;
        private ModerationOptions _options;

        #endregion

        #region Properties

        /// <summary>Moderation options (mode, risk, playerId)</summary>
        public ModerationOptions Options
        {
            get => _options ??= new ModerationOptions();
            set => _options = value;
        }

        public bool IsProcessing => _isProcessing;
        public ModeryoClient Client => _client ?? _sharedClient;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeClient();
        }

        private void Start()
        {
            if (inputField != null)
                inputField.onEndEdit.AddListener(OnInputEndEdit);
        }

        private void OnDestroy()
        {
            if (inputField != null)
                inputField.onEndEdit.RemoveListener(OnInputEndEdit);
            if (!useSharedClient)
                _client = null;
        }

        #endregion

        #region Public Methods

        /// <summary>Filter a message using coroutine API</summary>
        public void FilterMessage(string message, Action<FilterResult> callback)
        {
            if (string.IsNullOrEmpty(message))
            {
                callback?.Invoke(new FilterResult
                {
                    IsApproved = true,
                    OriginalMessage = message,
                    FilteredMessage = message
                });
                return;
            }
            StartCoroutine(FilterMessageCoroutine(message, callback));
        }

        /// <summary>Filter a message using async API</summary>
        public async void FilterMessageAsync(string message, Action<FilterResult> callback)
        {
            if (string.IsNullOrEmpty(message))
            {
                callback?.Invoke(new FilterResult
                {
                    IsApproved = true,
                    OriginalMessage = message,
                    FilteredMessage = message
                });
                return;
            }

            try
            {
                _isProcessing = true;
                var client = GetClient();
                var result = await client.ModerateAsync(message, _options);
                var filterResult = ProcessModerationResult(message, result);
                callback?.Invoke(filterResult);
            }
            catch (ModeryoException ex)
            {
                OnModerationError?.Invoke(ex);
                callback?.Invoke(new FilterResult
                {
                    IsApproved = !blockOnError,
                    OriginalMessage = message,
                    FilteredMessage = blockOnError ? "" : message,
                    Error = ex
                });
            }
            finally
            {
                _isProcessing = false;
            }
        }

        /// <summary>Set player ID for moderation tracking</summary>
        public ChatFilter WithPlayerId(string playerId)
        {
            Options.PlayerId = playerId;
            return this;
        }

        /// <summary>Set moderation mode (enforce or shadow)</summary>
        public ChatFilter WithMode(string mode)
        {
            Options.Mode = mode;
            return this;
        }

        /// <summary>Set risk level (conservative, balanced, aggressive)</summary>
        public ChatFilter WithRisk(string risk)
        {
            Options.Risk = risk;
            return this;
        }

        #endregion

        #region Private Methods

        private void InitializeClient()
        {
            if (config == null)
            {
                Debug.LogError("[Moderyo] ChatFilter: Config is not assigned!");
                return;
            }
            if (useSharedClient)
            {
                if (_sharedClient == null)
                    _sharedClient = new ModeryoClient(config);
            }
            else
            {
                _client = new ModeryoClient(config);
            }
        }

        private ModeryoClient GetClient()
        {
            return useSharedClient ? _sharedClient : _client;
        }

        private void OnInputEndEdit(string text)
        {
            if (!submitOnEnter) return;
            if (!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter)) return;
            if (string.IsNullOrEmpty(text)) return;
            if (Time.time - _lastModerationTime < throttleDelay) return;
            _lastModerationTime = Time.time;

            FilterMessage(text, result =>
            {
                if (result.IsApproved)
                {
                    OnMessageApproved?.Invoke(result.FilteredMessage);
                    inputField.text = "";
                }
                else if (censorInsteadOfBlock && !string.IsNullOrEmpty(result.FilteredMessage))
                {
                    OnMessageApproved?.Invoke(result.FilteredMessage);
                    inputField.text = "";
                }
            });
        }

        private IEnumerator FilterMessageCoroutine(string message, Action<FilterResult> callback)
        {
            _isProcessing = true;
            var client = GetClient();
            var operation = client.Moderate(message, _options);
            yield return operation;
            _isProcessing = false;

            if (operation.IsError)
            {
                OnModerationError?.Invoke(operation.Error);
                callback?.Invoke(new FilterResult
                {
                    IsApproved = !blockOnError,
                    OriginalMessage = message,
                    FilteredMessage = blockOnError ? "" : message,
                    Error = operation.Error
                });
            }
            else
            {
                var filterResult = ProcessModerationResult(message, operation.Result);
                callback?.Invoke(filterResult);
            }
        }

        private FilterResult ProcessModerationResult(string originalMessage, ModerationResult result)
        {
            var filterResult = new FilterResult
            {
                OriginalMessage = originalMessage,
                ModerationResult = result
            };

            switch (result.Action)
            {
                case Decision.Allow:
                    filterResult.IsApproved = true;
                    filterResult.FilteredMessage = originalMessage;
                    OnMessageApproved?.Invoke(originalMessage);
                    break;

                case Decision.Flag:
                case Decision.Warn:
                    filterResult.IsApproved = true;
                    filterResult.FilteredMessage = originalMessage;
                    filterResult.NeedsReview = true;
                    OnMessageFlagged?.Invoke(originalMessage, result);
                    break;

                case Decision.Block:
                    filterResult.IsApproved = false;
                    if (censorInsteadOfBlock)
                    {
                        filterResult.FilteredMessage = CensorMessage(originalMessage);
                        filterResult.IsApproved = true;
                    }
                    else
                    {
                        filterResult.FilteredMessage = "";
                    }
                    OnMessageBlocked?.Invoke(originalMessage, result);
                    break;
            }

            return filterResult;
        }

        private string CensorMessage(string message)
        {
            var chars = message.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsWhiteSpace(chars[i]))
                    chars[i] = '*';
            }
            return new string(chars);
        }

        #endregion
    }

    /// <summary>Result of a chat filter operation</summary>
    [Serializable]
    public class FilterResult
    {
        public bool IsApproved;
        public bool NeedsReview;
        public string OriginalMessage;
        public string FilteredMessage;
        public ModerationResult ModerationResult;
        public ModeryoException Error;
        public bool HasError => Error != null;
    }
}
