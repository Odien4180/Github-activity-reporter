# Unresolved Issues

This document tracks intentionally deferred work so implementation can continue into the next phase without losing context.

## Deferred from Phase 1

- Expand GitHub integration test coverage with mock/dry-run scenarios.
- Add the remaining Phase 1 documentation completeness checks.

## Phase 2 Remaining Work

- Implement the GitHub Pages publisher.
- Add snapshot tests for SVG and static HTML outputs.

## Phase 3 Remaining Work

- Implement the email HTML renderer.
- Implement the email plain-text renderer.
- Implement the Slack Block Kit renderer.
- Implement the email and Slack publishers with dry-run support.
- Add channel-specific validation and tests.

## Phase 4 Remaining Work

- Harden the AI provider abstraction.
- Implement public-only OpenAI/Copilot summarizer adapters.
- Add token/time/cost limits with retry and timeout controls.
- Add rule-based fallback verification tests.
