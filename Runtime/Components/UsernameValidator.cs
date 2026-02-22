using System;
using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

namespace Moderyo.Components
{
    /// <summary>
    /// Validates usernames through the Moderyo moderation API.
    /// Supports both local format checks and content moderation.
    /// </summary>
    [AddComponentMenu("Moderyo/Username Validator")]
    public class UsernameValidator : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private ModeryoConfig config;

        [Header("Validation Rules")]
        [SerializeField] private int minLength = 3;
        [SerializeField] private int maxLength = 20;
        [SerializeField] private bool allowNumbers = true;
        [SerializeField] private bool allowUnderscores = true;
        [SerializeField] private string allowedPattern = "";

        [Header("Events")]
        public UnityEvent<string> OnUsernameValid;
        public UnityEvent<string, string> OnUsernameInvalid;
        public UnityEvent<ModeryoException> OnValidationError;

        private ModeryoClient _client;
        private bool _isValidating;

        public bool IsValidating => _isValidating;

        private void Awake()
        {
            if (config != null)
                _client = new ModeryoClient(config);
        }

        /// <summary>Validate username using coroutine API</summary>
        public void ValidateUsername(string username, Action<UsernameValidationResult> callback)
        {
            StartCoroutine(ValidateUsernameCoroutine(username, callback));
        }

        /// <summary>Validate username using async API</summary>
        public async void ValidateUsernameAsync(string username, Action<UsernameValidationResult> callback)
        {
            var result = new UsernameValidationResult { Username = username };

            if (!ValidateBasic(username, result))
            {
                callback?.Invoke(result);
                return;
            }

            try
            {
                _isValidating = true;
                var moderationResult = await _client.ModerateAsync(username);

                if (moderationResult.IsBlocked() || moderationResult.Flagged)
                {
                    result.IsValid = false;
                    result.Reason = "Username contains inappropriate content";
                    result.TriggeredCategories = moderationResult.GetTriggeredCategories();
                    OnUsernameInvalid?.Invoke(username, result.Reason);
                }
                else
                {
                    result.IsValid = true;
                    OnUsernameValid?.Invoke(username);
                }
                result.ModerationResult = moderationResult;
            }
            catch (ModeryoException ex)
            {
                result.IsValid = false;
                result.Reason = "Validation service unavailable";
                result.Error = ex;
                OnValidationError?.Invoke(ex);
            }
            finally
            {
                _isValidating = false;
            }

            callback?.Invoke(result);
        }

        private IEnumerator ValidateUsernameCoroutine(string username, Action<UsernameValidationResult> callback)
        {
            var result = new UsernameValidationResult { Username = username };

            if (!ValidateBasic(username, result))
            {
                callback?.Invoke(result);
                yield break;
            }

            _isValidating = true;
            var operation = _client.Moderate(username);
            yield return operation;
            _isValidating = false;

            if (operation.IsError)
            {
                result.IsValid = false;
                result.Reason = "Validation service unavailable";
                result.Error = operation.Error;
                OnValidationError?.Invoke(operation.Error);
            }
            else
            {
                var moderationResult = operation.Result;
                if (moderationResult.IsBlocked() || moderationResult.Flagged)
                {
                    result.IsValid = false;
                    result.Reason = "Username contains inappropriate content";
                    result.TriggeredCategories = moderationResult.GetTriggeredCategories();
                    OnUsernameInvalid?.Invoke(username, result.Reason);
                }
                else
                {
                    result.IsValid = true;
                    OnUsernameValid?.Invoke(username);
                }
                result.ModerationResult = moderationResult;
            }

            callback?.Invoke(result);
        }

        private bool ValidateBasic(string username, UsernameValidationResult result)
        {
            if (string.IsNullOrEmpty(username))
            {
                result.IsValid = false;
                result.Reason = "Username cannot be empty";
                OnUsernameInvalid?.Invoke(username, result.Reason);
                return false;
            }
            if (username.Length < minLength)
            {
                result.IsValid = false;
                result.Reason = $"Username must be at least {minLength} characters";
                OnUsernameInvalid?.Invoke(username, result.Reason);
                return false;
            }
            if (username.Length > maxLength)
            {
                result.IsValid = false;
                result.Reason = $"Username cannot exceed {maxLength} characters";
                OnUsernameInvalid?.Invoke(username, result.Reason);
                return false;
            }

            foreach (char c in username)
            {
                if (char.IsLetter(c)) continue;
                if (allowNumbers && char.IsDigit(c)) continue;
                if (allowUnderscores && c == '_') continue;

                result.IsValid = false;
                result.Reason = "Username contains invalid characters";
                OnUsernameInvalid?.Invoke(username, result.Reason);
                return false;
            }

            if (!string.IsNullOrEmpty(allowedPattern))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(username, allowedPattern))
                {
                    result.IsValid = false;
                    result.Reason = "Username format is invalid";
                    OnUsernameInvalid?.Invoke(username, result.Reason);
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>Username validation result</summary>
    [Serializable]
    public class UsernameValidationResult
    {
        public string Username;
        public bool IsValid;
        public string Reason;
        public List<string> TriggeredCategories;
        public ModerationResult ModerationResult;
        public ModeryoException Error;
        public bool HasError => Error != null;
    }
}
