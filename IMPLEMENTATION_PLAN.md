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
- [ ] Expand GitHub integration test coverage with mock/dry-run scenarios
- [ ] Add remaining Phase 1 documentation completeness checks

## Phase 2: Dashboard and Web

- [x] SVG dashboard renderer
- [x] Static HTML renderer
- [x] Activity history JSON and trend views
- [ ] GitHub Pages publisher
- [ ] Snapshot tests for SVG/HTML outputs

## Phase 3: External Channels

- [ ] Email HTML renderer
- [ ] Email plain-text renderer
- [ ] Slack Block Kit renderer
- [ ] Email/Slack publishers with dry-run support
- [ ] Channel-specific validation and tests

## Phase 4: Public-only AI Summary

- [ ] AI provider abstraction hardening
- [ ] Public-only OpenAI/Copilot summarizer adapters
- [ ] Token/time/cost limits and retry/timeout controls
- [ ] Rule-based fallback and verification tests

## Current focus

- [x] Fix failing private aggregation expectation test (`TotalEventCount`)
- [x] Run and keep full test suite green while continuing feature work
