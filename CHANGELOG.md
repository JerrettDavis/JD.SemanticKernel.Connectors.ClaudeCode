# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
Version numbers are managed by [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning).

## [Unreleased]

### Changed
- Made authentication compliance-first: API-key sources are prioritized, and `CLAUDE_CODE_OAUTH_TOKEN` is no longer a credential source.
- Added `ClaudeCodeSessionOptions.EnableOAuthTokenSupport` (default `false`).
- Enforced OAuth safeguards in `ClaudeCodeSessionProvider`: OAuth tokens now require explicit opt-in and an interactive session.
- Updated README and DocFX documentation to remove unattended OAuth guidance and clarify API-key usage for automation/CI.

## [0.1] — 2026-02-19

### Added
- `ClaudeCodeSessionProvider` — multi-source credential resolution (options → env vars → local session)
- `ClaudeCodeSessionHttpHandler` — `DelegatingHandler` injecting OAuth Bearer or API key headers
- `KernelBuilderExtensions.UseClaudeCodeChatCompletion()` — one-liner Semantic Kernel registration (net8.0+)
- `ServiceCollectionExtensions.AddClaudeCodeAuthentication()` — DI registration with delegate or IConfiguration binding
- `ClaudeCodeHttpClientFactory` — static factory for bring-your-own-client scenarios
- `ClaudeModels` — well-known model identifier constants (Opus, Sonnet, Haiku)
- Model ID passthrough via `ConfigureOptions` middleware in `ChatClientBuilder` pipeline
- Central Package Management (CPM) via `Directory.Packages.props`
- Multi-TFM support: `netstandard2.0`, `net8.0`, `net10.0`
- Full DocFX documentation site with API reference, getting-started guides, and sample tool docs

### Sample CLI Tools
- `jdgerkinator` (JD.Tools.GherkinGenerator) — AI-powered acceptance criteria → Gherkin spec generator
- `jdpr` (JD.Tools.PullRequestReviewer) — Multi-provider PR review agent (GitHub, Azure DevOps, GitLab)
- `jdxplr` (JD.Tools.CodebaseExplorer) — Codebase profiler generating structured knowledgebases
- TodoExtractor — Minimal library demo extracting structured todos from natural language
