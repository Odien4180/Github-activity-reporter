# Unresolved Issues

This document tracks operational verification that cannot be performed safely without user-owned credentials or external side effects. The planned local implementation through Phase 4 is complete.

## Completed live verification

- GitHub Models public activity summary using a user-provided token with `models: read` permission. The generated preview was reviewed by the user, and the resulting feedback was incorporated into the richer narrative summary format.

## Live smoke tests

- Deploy an enabled static website to a real GitHub Pages environment.
- Send one test email with user-provided SMTP credentials.
- Send one test Slack message with a user-provided incoming webhook.
- Run one OpenAI Responses API summary with a user-provided API key.

These checks are deliberately not run by the automated test suite because they can publish externally, send messages, consume paid API quota, or require repository-level configuration.
