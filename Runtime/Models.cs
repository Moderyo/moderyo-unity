using System;
using System.Collections.Generic;

namespace Moderyo
{
    // ───────────────────────── Enums ─────────────────────────

    /// <summary>Moderation decision type</summary>
    public enum Decision { Allow, Flag, Warn, Block }

    /// <summary>Processing mode (normal, shadow, or degraded/offline)</summary>
    public enum ProcessingMode { Normal, Shadow, Degraded }

    // ───────────────────────── Category Keys ─────────────────────────

    /// <summary>
    /// All 27 moderation category keys used by the Moderyo API.
    /// </summary>
    public static class CategoryKeys
    {
        // ── Standard 11 ──
        public const string Hate                = "hate";
        public const string HateThreatening     = "hate/threatening";
        public const string Harassment           = "harassment";
        public const string HarassmentThreatening = "harassment/threatening";
        public const string SelfHarm             = "self-harm";
        public const string SelfHarmIntent       = "self-harm/intent";
        public const string SelfHarmInstructions = "self-harm/instructions";
        public const string Sexual               = "sexual";
        public const string SexualMinors         = "sexual/minors";
        public const string Violence             = "violence";
        public const string ViolenceGraphic      = "violence/graphic";

        // ── Self-harm engine ──
        public const string SelfHarmIdeation     = "self_harm_ideation";
        public const string SelfHarmIntentEngine = "self_harm_intent";
        public const string SelfHarmInstruction  = "self_harm_instruction";
        public const string SelfHarmSupport      = "self_harm_support";

        // ── Violence engine ──
        public const string ViolenceGeneral       = "violence_general";
        public const string ViolenceSevere        = "violence_severe";
        public const string ViolenceInstruction   = "violence_instruction";
        public const string ViolenceGlorification = "violence_glorification";

        // ── Child protection ──
        public const string ChildSexualContent  = "child_sexual_content";
        public const string MinorSexualization  = "minor_sexualization";
        public const string ChildGrooming       = "child_grooming";
        public const string AgeMentionRisk      = "age_mention_risk";

        // ── Extremism ──
        public const string ExtremismViolenceCall    = "extremism_violence_call";
        public const string ExtremismPropaganda      = "extremism_propaganda";
        public const string ExtremismSupport         = "extremism_support";
        public const string ExtremismSymbolReference = "extremism_symbol_reference";

        /// <summary>All 27 category keys in order</summary>
        public static readonly string[] All = new[]
        {
            Hate, HateThreatening, Harassment, HarassmentThreatening,
            SelfHarm, SelfHarmIntent, SelfHarmInstructions,
            Sexual, SexualMinors, Violence, ViolenceGraphic,
            SelfHarmIdeation, SelfHarmIntentEngine, SelfHarmInstruction, SelfHarmSupport,
            ViolenceGeneral, ViolenceSevere, ViolenceInstruction, ViolenceGlorification,
            ChildSexualContent, MinorSexualization, ChildGrooming, AgeMentionRisk,
            ExtremismViolenceCall, ExtremismPropaganda, ExtremismSupport, ExtremismSymbolReference
        };
    }

    // ───────────────────────── Categories ─────────────────────────

    /// <summary>
    /// Boolean flags for each of 27 categories.
    /// Access via indexer: categories["hate"], categories["violence"], etc.
    /// </summary>
    [Serializable]
    public class Categories
    {
        private readonly Dictionary<string, bool> _data = new Dictionary<string, bool>();

        public bool this[string key]
        {
            get => _data.TryGetValue(key, out var v) && v;
            set => _data[key] = value;
        }

        /// <summary>Return list of category keys that are true</summary>
        public List<string> GetTriggered()
        {
            var list = new List<string>();
            foreach (var kvp in _data)
                if (kvp.Value) list.Add(kvp.Key);
            return list;
        }

        /// <summary>True if any category is flagged</summary>
        public bool HasAny()
        {
            foreach (var kvp in _data)
                if (kvp.Value) return true;
            return false;
        }

        public Dictionary<string, bool> ToDictionary() => new Dictionary<string, bool>(_data);
    }

    // ───────────────────────── Category Scores ─────────────────────────

    /// <summary>
    /// Float scores (0.0–1.0) for each of 27 categories.
    /// Access via indexer: scores["hate"], scores["violence"], etc.
    /// </summary>
    [Serializable]
    public class CategoryScores
    {
        private readonly Dictionary<string, float> _data = new Dictionary<string, float>();

        public float this[string key]
        {
            get => _data.TryGetValue(key, out var v) ? v : 0f;
            set => _data[key] = value;
        }

        /// <summary>Get the maximum score across all categories</summary>
        public float GetMax()
        {
            float max = 0f;
            foreach (var kvp in _data)
                if (kvp.Value > max) max = kvp.Value;
            return max;
        }

        /// <summary>Get the category with the highest score</summary>
        public KeyValuePair<string, float>? GetHighest()
        {
            KeyValuePair<string, float>? best = null;
            foreach (var kvp in _data)
                if (!best.HasValue || kvp.Value > best.Value.Value)
                    best = kvp;
            return best;
        }

        /// <summary>Get all categories with scores at or above threshold</summary>
        public Dictionary<string, float> Above(float threshold)
        {
            var result = new Dictionary<string, float>();
            foreach (var kvp in _data)
                if (kvp.Value >= threshold) result[kvp.Key] = kvp.Value;
            return result;
        }

        public Dictionary<string, float> ToDictionary() => new Dictionary<string, float>(_data);
    }

    // ───────────────────────── Simplified Scores ─────────────────────────

    /// <summary>
    /// Simplified risk scores returned at the top level of the API response.
    /// Six high-level dimensions: toxicity, hate, harassment, scam, violence, fraud.
    /// </summary>
    [Serializable]
    public class SimplifiedScores
    {
        public float Toxicity;
        public float Hate;
        public float Harassment;
        public float Scam;
        public float Violence;
        public float Fraud;
    }

    // ───────────────────────── Policy Decision ─────────────────────────

    /// <summary>Rule that triggered a policy decision</summary>
    [Serializable]
    public class TriggeredRule
    {
        public string Id;
        public string Type;
        public string Category;
        public float Threshold;
        public float ActualValue;
        public string Matched;
    }

    /// <summary>Text highlight identifying problematic content</summary>
    [Serializable]
    public class Highlight
    {
        public string Text;
        public string Category;
        public int StartIndex;
        public int EndIndex;
    }

    /// <summary>Policy decision from the Moderyo backend</summary>
    [Serializable]
    public class PolicyDecision
    {
        /// <summary>ALLOW, FLAG, WARN, or BLOCK</summary>
        public string DecisionValue;
        public string RuleId;
        public string RuleName;
        public string Reason;
        public float Confidence;
        public string Severity;
        public TriggeredRule TriggeredRule;
        public List<Highlight> Highlights;
    }

    // ───────────────────────── Detected Phrases ─────────────────────────

    /// <summary>A phrase detected in the content (profanity, threat, etc.)</summary>
    [Serializable]
    public class DetectedPhrase
    {
        public string Text;
        /// <summary>profanity, insult, scam, threat, hate, violence</summary>
        public string Label;
    }

    // ───────────────────────── Long Text Analysis ─────────────────────────

    /// <summary>Sentence-level analysis within long text</summary>
    [Serializable]
    public class SentenceAnalysis
    {
        public string Text;
        public float Toxicity;
        public bool Flagged;
        public List<string> Categories;
    }

    /// <summary>Highlight within long text analysis</summary>
    [Serializable]
    public class LongTextHighlight
    {
        public string Text;
        public string Category;
        public int SentenceIndex;
    }

    /// <summary>Processing statistics for long text</summary>
    [Serializable]
    public class ProcessingInfo
    {
        public string Mode;
        public int OriginalCharCount;
        public int ProcessedCharCount;
        public bool Truncated;
        public float InferenceTimeMs;
    }

    /// <summary>Full long text analysis result</summary>
    [Serializable]
    public class LongTextAnalysis
    {
        public float OverallToxicity;
        public float MaxToxicity;
        public float Top3MeanToxicity;
        public float DecisionConfidence;
        public float SignalConfidence;
        public List<SentenceAnalysis> Sentences;
        public List<LongTextHighlight> Highlights;
        public ProcessingInfo Processing;
    }

    // ───────────────────────── Request ─────────────────────────

    /// <summary>
    /// Request body for POST /v1/moderation.
    /// </summary>
    [Serializable]
    public class ModerationRequest
    {
        /// <summary>Text to moderate</summary>
        public string Input;

        /// <summary>Backward-compatible alias for Input</summary>
        [Obsolete("Use Input instead")]
        public string Content
        {
            get => Input;
            set => Input = value;
        }

        public string Model;
        public bool? LongTextMode;
        public float? LongTextThreshold;
        public bool? SkipProfanity;
        public bool? SkipThreat;
        public bool? SkipMaskedWord;

        /// <summary>Create a simple request from text</summary>
        public static ModerationRequest Of(string input) => new ModerationRequest { Input = input };
    }

    /// <summary>
    /// Options mapped to X-Moderyo-* HTTP headers.
    /// </summary>
    [Serializable]
    public class ModerationOptions
    {
        /// <summary>enforce or shadow</summary>
        public string Mode;
        /// <summary>conservative, balanced, or aggressive</summary>
        public string Risk;
        /// <summary>Enable debug information in the response</summary>
        public bool? Debug;
        /// <summary>Player / user identifier for gaming scenarios</summary>
        public string PlayerId;
    }

    // ───────────────────────── Result ─────────────────────────

    /// <summary>
    /// Full moderation result from the Moderyo API.
    /// </summary>
    [Serializable]
    public class ModerationResult
    {
        public string Id;
        public string Model;
        public bool Flagged;
        public Categories Categories;
        public CategoryScores CategoryScores;
        public SimplifiedScores Scores;
        public PolicyDecision PolicyDecision;
        public List<DetectedPhrase> DetectedPhrases;
        public LongTextAnalysis LongTextAnalysis;
        public ProcessingMode? Mode;

        // ── Convenience methods ──

        /// <summary>True if policy decision is BLOCK</summary>
        public bool IsBlocked() => PolicyDecision?.DecisionValue == "BLOCK";

        /// <summary>True if flagged or policy decision is FLAG</summary>
        public bool IsFlagged() => PolicyDecision?.DecisionValue == "FLAG" || Flagged;

        /// <summary>True if not blocked</summary>
        public bool IsAllowed() => !IsBlocked();

        /// <summary>Backward-compatible: same as IsBlocked()</summary>
        public bool ShouldBlock() => IsBlocked();

        /// <summary>Backward-compatible: same as IsFlagged()</summary>
        public bool NeedsReview() => IsFlagged();

        /// <summary>Get triggered category keys</summary>
        public List<string> GetTriggeredCategories()
        {
            return Categories?.GetTriggered() ?? new List<string>();
        }

        /// <summary>Get highest risk category key</summary>
        public string GetHighestRiskCategory()
        {
            return CategoryScores?.GetHighest()?.Key;
        }

        /// <summary>Decision enum derived from PolicyDecision (backward compat for components)</summary>
        public Decision Action
        {
            get
            {
                if (PolicyDecision == null) return Flagged ? Decision.Flag : Decision.Allow;
                return PolicyDecision.DecisionValue switch
                {
                    "BLOCK" => Decision.Block,
                    "FLAG"  => Decision.Flag,
                    "WARN"  => Decision.Warn,
                    _       => Decision.Allow
                };
            }
        }

        /// <summary>Reason from PolicyDecision (backward compat)</summary>
        public string Explanation => PolicyDecision?.Reason;
    }

    // ───────────────────────── Batch Result ─────────────────────────

    /// <summary>Batch moderation result</summary>
    [Serializable]
    public class BatchModerationResult
    {
        public List<ModerationResult> Results = new List<ModerationResult>();

        public int Total => Results.Count;

        public int BlockedCount
        {
            get { int c = 0; foreach (var r in Results) if (r.IsBlocked()) c++; return c; }
        }

        public int FlaggedCount
        {
            get { int c = 0; foreach (var r in Results) if (r.IsFlagged()) c++; return c; }
        }

        public bool HasBlocked()
        {
            foreach (var r in Results) if (r.IsBlocked()) return true;
            return false;
        }

        public List<ModerationResult> GetBlocked()
        {
            var list = new List<ModerationResult>();
            foreach (var r in Results) if (r.IsBlocked()) list.Add(r);
            return list;
        }

        public List<ModerationResult> GetFlagged()
        {
            var list = new List<ModerationResult>();
            foreach (var r in Results) if (r.IsFlagged()) list.Add(r);
            return list;
        }
    }
}
