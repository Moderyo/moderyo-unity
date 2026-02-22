# Moderyo Unity SDK Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.7] - 2025-06-09

### Added
- Full 27-category moderation support matching Moderyo API v2
- Async/await (`Task`-based) and Coroutine (`IEnumerator`) dual API
- `ModeryoClient` with `ModerateAsync`, `ModerateBatchAsync`, `HealthCheckAsync`
- `ModerationOperation` CustomYieldInstruction for coroutine-based calls
- `ModeryoConfig` ScriptableObject for Inspector-driven configuration
- Policy decision parsing: decision, rule, confidence, severity, highlights
- Simplified scores: toxicity, hate, harassment, scam, violence, fraud
- Detected phrases and long-text analysis support
- Offline fallback modes: AllowAll, BlockAll, UseLocalFilter, Queue
- Game components: `ChatFilter`, `UsernameValidator`, `ReportSystem`
- Editor tools: Test Window, Config Inspector with connection test
- Automatic retry with exponential backoff and rate-limit handling
- 4 sample scripts: BasicChat, ChatFilterComponent, FullPlayground, CoroutineChat

### Fixed
- API key prefix validation now checks `mod_` instead of `sk-`
- Package samples paths corrected to match actual directory names
- Added missing `System.Linq` import in PlaygroundExample

### Platform Support
- Windows, macOS, Linux (Standalone)
- iOS, Android
- WebGL
- PlayStation 4/5, Xbox, Nintendo Switch (via .NET API)

### Requirements
- Unity 2021.3 LTS or later
- .NET Standard 2.1
