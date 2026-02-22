using System;
using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

namespace Moderyo.Components
{
    /// <summary>
    /// In-game report system with automatic content moderation.
    /// Handles report submission, rate limiting, and auto-moderation.
    /// </summary>
    [AddComponentMenu("Moderyo/Report System")]
    public class ReportSystem : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private ModeryoConfig config;

        [Header("Report Settings")]
        [SerializeField] private int maxReportsPerSession = 10;
        [SerializeField] private float reportCooldown = 30f;
        [SerializeField] private bool autoModerateReports = true;

        [Header("Events")]
        public UnityEvent<Report> OnReportSubmitted;
        public UnityEvent<Report, ModerationResult> OnReportModerated;
        public UnityEvent<string> OnReportLimitReached;
        public UnityEvent<float> OnReportCooldown;
        public UnityEvent<ModeryoException> OnReportError;

        private ModeryoClient _client;
        private readonly Dictionary<string, int> _reportCounts = new Dictionary<string, int>();
        private readonly Dictionary<string, float> _lastReportTimes = new Dictionary<string, float>();
        private readonly List<Report> _pendingReports = new List<Report>();

        public IReadOnlyList<Report> PendingReports => _pendingReports.AsReadOnly();

        private void Awake()
        {
            if (config != null)
                _client = new ModeryoClient(config);
        }

        /// <summary>Submit a new report</summary>
        public void SubmitReport(
            string reporterId,
            string reportedUserId,
            string content,
            ReportReason reason,
            Action<ReportResult> callback = null)
        {
            var report = new Report
            {
                Id = Guid.NewGuid().ToString(),
                ReporterId = reporterId,
                ReportedUserId = reportedUserId,
                Content = content,
                Reason = reason,
                Timestamp = DateTime.UtcNow,
                Status = ReportStatus.Pending
            };
            SubmitReport(report, callback);
        }

        /// <summary>Submit an existing report object</summary>
        public void SubmitReport(Report report, Action<ReportResult> callback = null)
        {
            StartCoroutine(SubmitReportCoroutine(report, callback));
        }

        /// <summary>Check if a reporter can submit</summary>
        public bool CanSubmitReport(string reporterId, out string reason)
        {
            reason = null;

            if (_reportCounts.TryGetValue(reporterId, out int count) && count >= maxReportsPerSession)
            {
                reason = "Maximum report limit reached for this session";
                return false;
            }

            if (_lastReportTimes.TryGetValue(reporterId, out float lastTime))
            {
                var elapsed = Time.time - lastTime;
                if (elapsed < reportCooldown)
                {
                    reason = $"Please wait {Mathf.CeilToInt(reportCooldown - elapsed)} seconds before reporting again";
                    return false;
                }
            }

            return true;
        }

        public float GetRemainingCooldown(string reporterId)
        {
            if (_lastReportTimes.TryGetValue(reporterId, out float lastTime))
            {
                var elapsed = Time.time - lastTime;
                return Mathf.Max(0, reportCooldown - elapsed);
            }
            return 0;
        }

        public void ClearReportLimits(string reporterId)
        {
            _reportCounts.Remove(reporterId);
            _lastReportTimes.Remove(reporterId);
        }

        public void ClearAllReportLimits()
        {
            _reportCounts.Clear();
            _lastReportTimes.Clear();
        }

        private IEnumerator SubmitReportCoroutine(Report report, Action<ReportResult> callback)
        {
            var result = new ReportResult { Report = report };

            if (!CanSubmitReport(report.ReporterId, out string limitReason))
            {
                result.Success = false;
                result.Error = limitReason;
                if (limitReason.Contains("limit"))
                    OnReportLimitReached?.Invoke(report.ReporterId);
                else
                    OnReportCooldown?.Invoke(GetRemainingCooldown(report.ReporterId));
                callback?.Invoke(result);
                yield break;
            }

            // Track report counts
            if (!_reportCounts.ContainsKey(report.ReporterId))
                _reportCounts[report.ReporterId] = 0;
            _reportCounts[report.ReporterId]++;
            _lastReportTimes[report.ReporterId] = Time.time;

            _pendingReports.Add(report);
            OnReportSubmitted?.Invoke(report);

            // Auto-moderate the reported content
            if (autoModerateReports && _client != null)
            {
                var options = new ModerationOptions();
                if (!string.IsNullOrEmpty(report.ReportedUserId))
                    options.PlayerId = report.ReportedUserId;

                var operation = _client.Moderate(report.Content, options);
                yield return operation;

                if (!operation.IsError)
                {
                    report.ModerationResult = operation.Result;
                    report.Status = operation.Result.IsBlocked()
                        ? ReportStatus.Confirmed
                        : ReportStatus.PendingReview;
                    OnReportModerated?.Invoke(report, operation.Result);
                    result.ModerationResult = operation.Result;
                }
                else
                {
                    OnReportError?.Invoke(operation.Error);
                    result.ModerationError = operation.Error;
                }
            }

            result.Success = true;
            callback?.Invoke(result);
        }
    }

    // ───────────────────────── Report Types ─────────────────────────

    [Serializable]
    public class Report
    {
        public string Id;
        public string ReporterId;
        public string ReportedUserId;
        public string Content;
        public ReportReason Reason;
        public DateTime Timestamp;
        public ReportStatus Status;
        public ModerationResult ModerationResult;
        public string AdminNotes;
    }

    public enum ReportReason
    {
        Harassment,
        HateSpeech,
        Violence,
        SexualContent,
        Spam,
        Cheating,
        InappropriateName,
        Other
    }

    public enum ReportStatus
    {
        Pending,
        PendingReview,
        Confirmed,
        Dismissed,
        ActionTaken
    }

    [Serializable]
    public class ReportResult
    {
        public Report Report;
        public bool Success;
        public string Error;
        public ModerationResult ModerationResult;
        public ModeryoException ModerationError;
    }
}
