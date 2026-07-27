# GitHub Activity Reporter Implementation Plan

## Phase 1: Core MVP

- [x] Core domain models (public/private separation)
- [x] Public/private classification and collection pipeline skeleton
- [x] Private activity aggregation and deduplication
- [x] Privacy validator baseline with security-focused tests
- [x] Markdown and JSON renderers
- [x] README marker updater and GitHub profile publishing path
- [x] CLI commands: `init`, `configure`, `preview`, `run`, `doctor`, `install-workflow`, `validate`
- [x] State file read/write
- [x] GitHub Actions workflow generation
- [x] Expand GitHub integration test coverage with mock/dry-run scenarios
- [x] Add remaining Phase 1 documentation completeness checks

## Phase 2: Dashboard and Web

- [x] SVG dashboard renderer
- [x] Static HTML renderer
- [x] Activity history JSON and trend views
- [x] GitHub Pages publisher
- [x] Snapshot tests for SVG/HTML outputs

## Phase 3: External Channels

- [x] Email HTML renderer
- [x] Email plain-text renderer
- [x] Slack Block Kit renderer
- [x] Email/Slack publishers with dry-run support
- [x] Channel-specific validation and tests

## Phase 4: Public-only AI Summary

- [x] AI provider abstraction hardening
- [x] Public-only OpenAI/GitHub Models summarizer adapters
- [x] Input/output token-proxy limits and retry/timeout controls
- [x] Rule-based fallback and verification tests

## Current focus

- [x] Complete Phases 1-4
- [x] Complete the opt-in GitHub Models live smoke test with a user-provided token
- [x] Improve AI summaries from live feedback with a period headline, outcome highlights and repository-level details
- [ ] Perform opt-in live smoke tests for GitHub Pages, SMTP, Slack and the OpenAI provider with user-owned credentials
- [ ] Continue production hardening if the remaining live integrations reveal operational issues
