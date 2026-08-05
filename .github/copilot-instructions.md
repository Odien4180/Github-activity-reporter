# GitHub Activity Reporter 전체 작업 명세

## 1. 프로젝트 개요

GitHub 사용자의 Public 및 Private 활동을 주기적으로 수집하고, 공개 범위에 맞게 정리한 뒤 사용자가 선택한 출력 형식과 채널로 배포하는 환경 구성 도구를 개발한다.

이 도구의 핵심 목적은 사용자의 GitHub 프로필이나 외부 리포트에서 다음 내용을 지속적으로 확인할 수 있게 하는 것이다.

* 최근 어떤 공개 프로젝트에서 활동했는지
* 공개 저장소에서 어떤 작업이 진행되었는지
* 비공개 저장소에서 어느 정도의 활동이 있었는지
* 최근 활동량이 증가했는지 감소했는지
* 일간 또는 주간 단위로 어떤 개발 활동이 있었는지

Public 활동은 공개 저장소의 이름, 링크, PR, Issue, Release 등 외부에 이미 공개된 정보를 비교적 구체적으로 정리할 수 있다.

Private 활동은 저장소명이나 작업 내용을 노출하지 않고 다음과 같은 활동별 건수만 집계한다.

* 활동한 비공개 저장소 수
* 커밋 수
* PR 생성 수
* PR 병합 수
* Issue 생성 및 종료 수
* Review 작성 수
* Release 수
* 활동 일수

핵심 공개 정책은 다음과 같다.

```text
Public activity = 내용 및 링크 중심 요약
Private activity = 익명화된 활동 건수 중심 집계
```

---

# 2. 프로젝트 목표

사용자가 CLI를 실행하면 대화형 설정을 통해 다음 작업을 진행할 수 있어야 한다.

1. GitHub 계정 인증 상태 확인
2. GitHub 사용자명 확인
3. GitHub Profile Repository 확인 또는 생성
4. 수집할 Public 및 Private 활동 유형 선택
5. Public 활동 공개 수준 설정
6. Private 활동 집계 항목 설정
7. 출력 형식 선택
8. 게시 대상 선택
9. 실행 주기와 시간대 설정
10. GitHub Actions 워크플로 생성
11. 필요한 Secret 등록 안내
12. 출력 결과 미리보기
13. 초기 보고서 생성 및 검증

개발 환경에서의 실행 예시는 다음과 같다.

```bash
git clone <repository-url>
cd github-activity-reporter

dotnet run --project src/GitHubActivityReporter.Cli -- init
```

글로벌 도구 배포 이후에는 다음 형태를 목표로 한다.

```bash
dotnet tool install -g GitHubActivityReporter
github-activity-reporter init
```

---

# 3. 주요 출력 대상

도구는 하나의 공통 보고서 모델을 기반으로 여러 출력을 생성해야 한다.

지원 대상:

* GitHub Profile README
* SVG 대시보드
* 정적 HTML 웹사이트
* HTML 이메일
* Plain Text 이메일
* Slack 메시지
* JSON 출력
* 로컬 파일 출력

출력물과 게시 채널은 분리한다.

예:

```text
Markdown Renderer
→ GitHub Profile Publisher

SVG Renderer
→ GitHub Profile Publisher

Static HTML Renderer
→ GitHub Pages Publisher

Email HTML Renderer
→ Email Publisher

Slack Renderer
→ Slack Publisher

JSON Renderer
→ Local File Publisher
```

---

# 4. 기술 스택

다음 기술을 기본으로 사용한다.

* .NET 10
* C#
* Spectre.Console
* Spectre.Console.Cli 또는 System.CommandLine
* Octokit 또는 GitHub GraphQL API
* YamlDotNet
* Scriban
* FluentValidation
* xUnit
* NSubstitute 또는 Moq

GitHub 인증과 저장소 생성 등 일부 작업은 `gh` CLI를 subprocess로 호출해도 된다.

단, 핵심 도메인 로직은 `gh` CLI나 특정 GitHub SDK에 직접 종속되지 않도록 인터페이스로 분리한다.

---

# 5. 프로젝트 명칭

임시 프로젝트명:

```text
GitHub Activity Reporter
```

CLI 명령어:

```text
github-activity-reporter
```

기본 네임스페이스:

```text
GitHubActivityReporter
```

---

# 6. 전체 처리 파이프라인

```text
GitHub Activity Collection
        ↓
Visibility Classification
        ↓
Public / Private Processing
        ↓
Privacy Sanitization
        ↓
Report Generation
        ↓
Rendering
        ↓
Validation
        ↓
Publishing
```

Public과 Private은 수집 직후부터 별도 파이프라인으로 처리한다.

```text
Public Repositories
    ↓
상세 이벤트 수집
    ↓
규칙 기반 정리 또는 AI 요약
    ↓
PublicRepositoryActivity

Private Repositories
    ↓
최소 이벤트 정보 수집
    ↓
저장소 식별 정보 제거
    ↓
건수 집계
    ↓
PrivateActivityMetrics
```

두 결과는 최종적으로 `ActivityReport` 모델에 통합한다.

---

# 7. Public 활동 처리 정책

Public 저장소는 외부에 공개된 정보이므로 설정에 따라 다음 항목을 사용할 수 있다.

* 저장소 이름
* 저장소 URL
* 저장소 설명
* 커밋 수
* PR 제목
* PR URL
* PR 상태
* Issue 제목
* Issue URL
* Issue 상태
* Review 수
* Release 이름
* Release URL
* 활동 시각
* 사용 언어
* Repository Topics

Public 활동은 저장소별로 정리한다.

예:

```text
NovelAI Codex Bridge

- VS Code 확장 설정 흐름 개선
- 연결 상태 및 오류 처리 보완
- 설치 문서 업데이트

5 commits · 1 merged PR · 2 closed issues
```

커밋 메시지는 기본적으로 공개하지 않는다.

AI 요약을 사용하는 경우에도 Public 데이터만 전달한다.

---

# 8. Private 활동 처리 정책

## 8.1 기본 원칙

Private 활동은 작업 내용을 요약하지 않는다.

저장소별 세부 내역도 기본적으로 노출하지 않는다.

Private 활동에서 허용되는 기본 출력은 다음과 같다.

* 활동한 비공개 저장소 수
* 커밋 수
* PR 생성 수
* PR 병합 수
* PR 종료 수
* Issue 생성 수
* Issue 종료 수
* Review 작성 수
* Release 수
* 활동 일수
* 마지막 활동 시각

예:

```text
Private Activity

최근 보고 기간 동안 3개의 비공개 저장소에서 활동했습니다.

12 commits
2 pull requests opened
1 pull request merged
4 issues closed
3 reviews submitted
```

## 8.2 노출 금지 정보

Private 저장소와 관련된 다음 정보는 출력하거나 장기 보존하면 안 된다.

* 저장소 이름
* 저장소 URL
* 조직 이름
* 소유자 이름
* 저장소 설명
* 커밋 메시지
* 커밋 SHA
* PR 제목
* PR 본문
* PR URL
* Issue 제목
* Issue 본문
* Issue URL
* 브랜치명
* 태그명
* Release 이름
* 파일 경로
* 파일명
* 클래스명
* 메서드명
* 코드 내용
* 사용 기술 추정
* 프로젝트 장르 추정
* 고객사 이름
* 퍼블리셔 이름
* 참여자 이름
* 프로젝트 내부 용어

## 8.3 AI 사용 제한

Private 원본 데이터는 AI 모델이나 외부 요약 서비스로 전달하면 안 된다.

```text
Public raw activity
→ AI summarizer allowed

Private raw activity
→ AI summarizer forbidden
→ rule-based aggregation only
```

Private 모델이 AI Summarizer 인터페이스에 전달될 수 없도록 타입 수준에서 분리한다.

---

# 9. 솔루션 구조

```text
GitHubActivityReporter.sln

src/
├─ GitHubActivityReporter.Core/
│  ├─ Models/
│  ├─ Configuration/
│  ├─ Abstractions/
│  ├─ Pipelines/
│  ├─ Validation/
│  ├─ Security/
│  └─ State/
│
├─ GitHubActivityReporter.GitHub/
│  ├─ Authentication/
│  ├─ Collectors/
│  ├─ Api/
│  ├─ GraphQL/
│  ├─ Rest/
│  └─ Mapping/
│
├─ GitHubActivityReporter.Summarization/
│  ├─ RuleBased/
│  ├─ Ai/
│  ├─ Prompts/
│  └─ Fallback/
│
├─ GitHubActivityReporter.Rendering/
│  ├─ Markdown/
│  ├─ Svg/
│  ├─ Html/
│  ├─ Email/
│  ├─ Slack/
│  └─ Json/
│
├─ GitHubActivityReporter.Publishing/
│  ├─ GitHubProfile/
│  ├─ GitHubPages/
│  ├─ Email/
│  ├─ Slack/
│  └─ FileSystem/
│
├─ GitHubActivityReporter.Bootstrap/
│  ├─ Templates/
│  ├─ Generators/
│  ├─ GitHubActions/
│  ├─ RepositorySetup/
│  └─ ConfigurationSetup/
│
└─ GitHubActivityReporter.Cli/
   ├─ Commands/
   ├─ Prompts/
   ├─ Presentation/
   ├─ Services/
   └─ Program.cs

tests/
├─ GitHubActivityReporter.Core.Tests/
├─ GitHubActivityReporter.GitHub.Tests/
├─ GitHubActivityReporter.Rendering.Tests/
├─ GitHubActivityReporter.Publishing.Tests/
├─ GitHubActivityReporter.Security.Tests/
└─ GitHubActivityReporter.IntegrationTests/
```

---

# 10. 핵심 도메인 모델

## 10.1 ActivityVisibility

```csharp
public enum ActivityVisibility
{
    Public,
    Private
}
```

## 10.2 ActivityType

```csharp
public enum ActivityType
{
    Commit,
    PullRequestOpened,
    PullRequestMerged,
    PullRequestClosed,
    IssueOpened,
    IssueClosed,
    ReviewSubmitted,
    ReleasePublished
}
```

## 10.3 PublicActivityEvent

```csharp
public sealed record PublicActivityEvent
{
    public required ActivityType Type { get; init; }

    public required string RepositoryName { get; init; }

    public required string RepositoryUrl { get; init; }

    public string? Title { get; init; }

    public string? Url { get; init; }

    public string? Description { get; init; }

    public string? Language { get; init; }

    public IReadOnlyList<string> Topics { get; init; }
        = Array.Empty<string>();

    public required DateTimeOffset OccurredAt { get; init; }
}
```

## 10.4 PrivateActivityEvent

Private 원본 이벤트는 GitHub 수집 및 집계 모듈 내부에서만 사용한다.

```csharp
internal sealed record PrivateActivityEvent
{
    public required string RepositoryOpaqueId { get; init; }

    public required ActivityType Type { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
}
```

`RepositoryOpaqueId`는 활성 저장소 수와 중복 이벤트 계산에만 사용한다.

최종 보고서나 파일에 포함하지 않는다.

## 10.5 PublicActivityMetrics

```csharp
public sealed record PublicActivityMetrics
{
    public int CommitCount { get; init; }

    public int PullRequestOpenedCount { get; init; }

    public int PullRequestMergedCount { get; init; }

    public int PullRequestClosedCount { get; init; }

    public int IssueOpenedCount { get; init; }

    public int IssueClosedCount { get; init; }

    public int ReviewSubmittedCount { get; init; }

    public int ReleasePublishedCount { get; init; }
}
```

## 10.6 PrivateActivityMetrics

```csharp
public sealed record PrivateActivityMetrics
{
    public int ActiveRepositoryCount { get; init; }

    public int CommitCount { get; init; }

    public int PullRequestOpenedCount { get; init; }

    public int PullRequestMergedCount { get; init; }

    public int PullRequestClosedCount { get; init; }

    public int IssueOpenedCount { get; init; }

    public int IssueClosedCount { get; init; }

    public int ReviewSubmittedCount { get; init; }

    public int ReleasePublishedCount { get; init; }

    public int ActiveDayCount { get; init; }

    public DateTimeOffset? LastActivityAt { get; init; }
}
```

## 10.7 PublicRepositoryActivity

```csharp
public sealed record PublicRepositoryActivity
{
    public required string RepositoryName { get; init; }

    public required string RepositoryUrl { get; init; }

    public string? Description { get; init; }

    public string? Language { get; init; }

    public IReadOnlyList<string> Topics { get; init; }
        = Array.Empty<string>();

    public required IReadOnlyList<PublicActivityEvent> Events { get; init; }

    public required PublicActivityMetrics Metrics { get; init; }

    public string? Summary { get; init; }
}
```

## 10.8 ActivityReport

```csharp
public sealed record ActivityReport
{
    public required DateTimeOffset GeneratedAt { get; init; }

    public required DateTimeOffset PeriodStart { get; init; }

    public required DateTimeOffset PeriodEnd { get; init; }

    public required string GitHubUserName { get; init; }

    public required IReadOnlyList<PublicRepositoryActivity>
        PublicActivities { get; init; }

    public required PrivateActivityMetrics
        PrivateMetrics { get; init; }
}
```

---

# 11. 주요 인터페이스

## 11.1 Activity Collector

```csharp
public interface IActivityCollector
{
    Task<CollectedActivity> CollectAsync(
        CollectionRequest request,
        CancellationToken cancellationToken);
}
```

## 11.2 Public Activity Summarizer

```csharp
public interface IPublicActivitySummarizer
{
    Task<IReadOnlyList<PublicRepositoryActivity>> SummarizeAsync(
        IReadOnlyList<PublicActivityEvent> events,
        CancellationToken cancellationToken);
}
```

Private 모델을 입력으로 받는 오버로드는 만들지 않는다.

## 11.3 Private Activity Aggregator

```csharp
internal interface IPrivateActivityAggregator
{
    PrivateActivityMetrics Aggregate(
        IReadOnlyList<PrivateActivityEvent> events);
}
```

## 11.4 Report Renderer

```csharp
public interface IReportRenderer
{
    string RendererId { get; }

    Task<RenderedReport> RenderAsync(
        ActivityReport report,
        RendererContext context,
        CancellationToken cancellationToken);
}
```

## 11.5 Report Publisher

```csharp
public interface IReportPublisher
{
    string PublisherId { get; }

    Task<PublishResult> PublishAsync(
        RenderedReport report,
        PublisherContext context,
        CancellationToken cancellationToken);
}
```

## 11.6 Output Validator

```csharp
public interface IOutputValidator
{
    ValidationResult Validate(
        RenderedReport report,
        ValidationContext context);
}
```

---

# 12. 설정 파일

기본 설정 파일명:

```text
activity-reporter.yml
```

예시:

```yaml
version: 1

github:
  username: example-user

  profile_repository:
    owner: example-user
    name: example-user
    branch: main

collection:
  period:
    mode: since-last-success
    initial_lookback: 24h

  public:
    enabled: true

    event_types:
      commits: true
      pull_requests_opened: true
      pull_requests_merged: true
      pull_requests_closed: false
      issues_opened: true
      issues_closed: true
      reviews: true
      releases: true

  private:
    enabled: true

    event_types:
      commits: true
      pull_requests_opened: true
      pull_requests_merged: true
      pull_requests_closed: false
      issues_opened: false
      issues_closed: true
      reviews: true
      releases: false

privacy:
  public:
    expose_repository_names: true
    expose_repository_links: true
    expose_repository_descriptions: true
    expose_pull_request_titles: true
    expose_issue_titles: true
    expose_release_names: true
    expose_languages: true
    expose_topics: false
    expose_commit_messages: false
    ai_summary: true

  private:
    mode: aggregate-only

    expose_active_repository_count: true
    expose_repository_names: false
    expose_repository_aliases: false
    expose_organization_names: false
    expose_titles: false
    expose_links: false
    expose_commit_messages: false
    expose_branch_names: false
    expose_file_paths: false
    expose_topics: false
    ai_summary: false

summary:
  language: ko
  style: concise
  max_public_repositories: 5
  max_items_per_repository: 3

outputs:
  github_profile:
    enabled: true
    renderer: compact-markdown
    target: generated/activity.md

  dashboard:
    enabled: true
    renderer: svg-dashboard
    target: generated/activity-dashboard.svg

  website:
    enabled: true
    renderer: static-html
    output_directory: generated/site
    history_days: 30

  email:
    enabled: false
    renderer: email-html
    html_target: generated/email.html
    text_target: generated/email.txt

  slack:
    enabled: false
    renderer: slack-blocks
    target: generated/slack.json

  json:
    enabled: true
    renderer: normalized-json
    target: generated/report.json

publishers:
  github_profile:
    enabled: true

  github_pages:
    enabled: true

  email:
    enabled: false
    secret_name: EMAIL_CREDENTIALS

  slack:
    enabled: false
    secret_name: SLACK_WEBHOOK_URL

  local:
    enabled: true
    output_directory: artifacts

schedule:
  enabled: true
  timezone: Asia/Seoul
  local_time: "09:00"
  frequency: daily
```

GitHub Actions cron은 UTC 기준이므로 사용자가 입력한 시간대와 시각을 기준으로 워크플로 생성 시 변환한다.

---

# 13. CLI 명령어

다음 명령어를 구현한다.

```bash
github-activity-reporter init
github-activity-reporter configure
github-activity-reporter preview
github-activity-reporter run
github-activity-reporter doctor
github-activity-reporter install-workflow
github-activity-reporter validate
```

---

# 14. init 명령

대화형 초기 설정을 수행한다.

처리 항목:

* .NET 실행 환경 확인
* GitHub CLI 설치 여부 확인
* GitHub 로그인 여부 확인
* GitHub 사용자명 확인
* Profile Repository 존재 여부 확인
* Profile Repository 생성 여부 선택
* 수집 범위 선택
* Public 활동 공개 설정
* Private 활동 집계 설정
* 출력 형식 선택
* 게시 대상 선택
* 실행 주기 설정
* 설정 파일 생성
* GitHub Actions 생성
* Secret 등록 안내
* 초기 Preview 생성
* Privacy Validator 실행

기존 Profile Repository가 있다면 README 전체를 덮어쓰지 않는다.

---

# 15. configure 명령

기존 설정 파일을 대화형으로 수정한다.

수정 가능 항목:

* 활동 수집 기간
* Public 활동 유형
* Private 활동 유형
* Public 공개 수준
* Private 집계 항목
* AI 요약 사용 여부
* 출력 형식
* 게시 채널
* 실행 일정
* 시간대
* 출력 언어
* 보고서 길이
* 히스토리 보존 기간

---

# 16. preview 명령

실제 외부 게시 없이 전체 출력을 생성한다.

출력 구조:

```text
artifacts/preview/
├─ report.json
├─ profile.md
├─ dashboard.svg
├─ website/
│  ├─ index.html
│  ├─ assets/
│  │  ├─ style.css
│  │  └─ app.js
│  └─ data/
│     ├─ latest.json
│     └─ history.json
├─ email.html
├─ email.txt
└─ slack.json
```

Preview 모드에서는 다음을 실행하지 않는다.

* Git commit
* Git push
* GitHub Pages 배포
* 이메일 전송
* Slack 메시지 전송

---

# 17. run 명령

전체 파이프라인을 한 번 실행한다.

```text
collect
→ classify
→ aggregate
→ summarize
→ compose report
→ render
→ privacy validate
→ publish
→ save state
```

중간 단계 중 하나라도 실패하면 실패 상태를 반환한다.

Privacy 검증 실패 시 Publisher를 호출하지 않는다.

---

# 18. doctor 명령

다음 항목을 검사한다.

* 설정 파일 존재 여부
* 설정 스키마 유효성
* GitHub 인증 상태
* GitHub 사용자명 일치 여부
* Profile Repository 존재 여부
* Profile Repository 쓰기 권한
* GitHub Token 권한
* GitHub Actions 활성화 여부
* 필요한 Secret 존재 여부
* README 마커 상태
* 출력 경로 쓰기 권한
* 템플릿 파일 존재 여부
* 상태 파일 유효성
* Private 정보 노출 가능성
* 사용 중인 Renderer와 Publisher 호환성
* Git 저장소 상태

출력 예:

```text
✓ GitHub authenticated
✓ Profile repository found
✓ Configuration valid
✓ README markers valid
✓ Local output directory writable
⚠ Slack publisher enabled but secret not found
✗ Privacy validation failed for generated/profile.md
```

---

# 19. validate 명령

이미 생성된 출력물에 Private 정보나 Secret이 포함되지 않았는지 검사한다.

검사 대상:

* Markdown
* SVG
* HTML
* CSS
* JavaScript
* JSON
* 이메일 HTML
* 이메일 Plain Text
* Slack payload

---

# 20. 대화형 초기 설정 흐름

## 20.1 GitHub 계정 확인

```text
GitHub 계정을 확인했습니다.

Username: example-user

이 계정을 사용하시겠습니까?
> Yes
```

## 20.2 Profile Repository 확인

```text
GitHub Profile Repository를 확인합니다.

example-user/example-user

상태: 존재하지 않음

새 Public Repository를 생성하시겠습니까?
> Yes
```

## 20.3 활동 기간 선택

```text
보고 기간을 선택하세요.

> 마지막 성공 실행 이후
  지난 24시간
  지난 7일
  사용자 지정
```

초기 실행 시에는 `initial_lookback` 값을 사용한다.

## 20.4 Public 공개 설정

```text
Public 활동에서 표시할 항목을 선택하세요.

[x] 저장소 이름
[x] 저장소 링크
[x] 저장소 설명
[x] 활동별 건수
[x] PR 제목
[x] Issue 제목
[x] Release 정보
[x] 주요 언어
[x] AI 요약
[ ] Repository Topics
[ ] 커밋 메시지
```

## 20.5 Private 집계 설정

```text
Private 활동에서 집계할 항목을 선택하세요.

[x] 활동한 저장소 수
[x] 커밋 수
[x] PR 생성 수
[x] PR 병합 수
[x] Issue 종료 수
[x] Review 수
[ ] Release 수
```

저장소명, 제목, URL, 브랜치명, 파일 경로 등은 선택지로 제공하지 않는다.

## 20.6 출력 형식 선택

```text
생성할 출력물을 선택하세요.

[x] GitHub Profile Markdown
[x] SVG Dashboard
[x] Static HTML Website
[x] JSON Report
[ ] Email HTML
[ ] Slack Message
```

## 20.7 게시 위치 선택

```text
출력물을 게시할 위치를 선택하세요.

[x] GitHub Profile Repository
[x] GitHub Pages
[x] Local Directory
[ ] Email
[ ] Slack
```

## 20.8 실행 일정 선택

```text
갱신 주기를 선택하세요.

> 매일
  평일만
  매주
  수동 실행만
```

```text
실행 시각:
> 09:00

Timezone:
> Asia/Seoul
```

---

# 21. GitHub Profile README 처리

Profile README 전체를 자동 생성하지 않는다.

README 내부의 지정 영역만 교체한다.

```md
<!-- GITHUB_ACTIVITY_REPORTER:START -->

자동 생성 영역

<!-- GITHUB_ACTIVITY_REPORTER:END -->
```

처리 규칙:

* 마커가 없다면 README 마지막에 추가
* 마커 사이의 내용만 교체
* 마커 밖의 사용자 작성 내용은 보존
* 마커가 중복되면 실패
* 시작 마커와 종료 마커 순서가 잘못되면 실패
* README가 없으면 새로 생성
* 변경된 내용이 없으면 커밋하지 않음

---

# 22. GitHub Profile 출력 예시

```md
## Recent Development Activity

### Public Activity

#### NovelAI Codex Bridge

VS Code 확장의 설정 흐름과 연결 안정성을 개선했습니다.

- 5 commits
- 1 pull request merged
- 2 issues closed

[View repository →](https://github.com/example/NovelAICodexBridge)

### Private Activity

최근 보고 기간 동안 3개의 비공개 저장소에서 활동했습니다.

- 12 commits
- 2 pull requests opened
- 1 pull request merged
- 4 issues closed
- 3 reviews submitted

_Last updated: 2026-07-27 09:00 KST_
```

Private 영역에는 프로젝트 이름이나 작업 내용을 표시하지 않는다.

---

# 23. SVG 대시보드

SVG Renderer를 구현한다.

표시 항목:

* 보고 기간
* 마지막 갱신 시각
* Public 활성 저장소 수
* Public 커밋 수
* Public PR 및 Issue 수
* Private 활성 저장소 수
* Private 커밋 수
* Private PR 및 Issue 수
* 최근 활동 상태

예:

```text
Development Activity

Public
2 active repositories
7 commits
2 merged PRs
3 closed issues

Private
3 active repositories
12 commits
1 merged PR
4 closed issues

Updated 2026-07-27 09:00 KST
```

요구사항:

* GitHub README에서 직접 표시 가능
* JavaScript 사용 금지
* 외부 폰트 의존 금지
* Private 식별 정보 포함 금지
* `viewBox` 사용
* 라이트 및 다크 배경에서 가독성 확보
* 기본 테마 제공
* 사용자 지정 테마 설정 지원
* 접근 가능한 텍스트 제공

README 삽입 예:

```md
![Development Activity](./generated/activity-dashboard.svg)
```

---

# 24. 정적 HTML 웹사이트

생성 구조:

```text
generated/site/
├─ index.html
├─ assets/
│  ├─ style.css
│  └─ app.js
└─ data/
   ├─ latest.json
   └─ history.json
```

페이지 구성:

* 사용자명
* 보고 기간
* 마지막 갱신 시각
* Public 활동 요약
* Public 저장소별 세부 내역
* Private 활동 통계
* 최근 7일 또는 30일 활동 추이
* 날짜별 히스토리
* Public 저장소 링크

Private 히스토리에도 숫자 데이터만 저장한다.

Private 저장소명이나 이벤트 제목은 브라우저 개발자 도구로 확인 가능한 HTML, JavaScript, JSON 어디에도 포함하면 안 된다.

GitHub Pages 배포를 지원한다.

---

# 25. 이메일 출력

이메일은 일반 웹사이트와 별도 Renderer를 사용한다.

생성 파일:

```text
generated/email.html
generated/email.txt
```

HTML 이메일 요구사항:

* JavaScript 금지
* 인라인 CSS 사용
* 최대 너비 680px
* 모바일 환경 대응
* Public 저장소 링크 제공 가능
* Private은 집계 건수만 표시
* 외부 이미지 없이도 읽을 수 있어야 함
* Plain Text fallback 생성

---

# 26. Slack 출력

Slack Block Kit 형식의 JSON payload를 생성한다.

생성 파일:

```text
generated/slack.json
```

요구사항:

* Public 활동은 저장소명, 링크, 요약 및 활동 건수를 포함할 수 있음
* Private 활동은 전체 집계 건수만 포함
* 메시지 길이 제한 고려
* 긴 Public 활동은 상위 N개 저장소만 표시
* 게시 전 Privacy Validator 실행
* 초기 버전은 payload 생성과 dry-run을 우선 구현 가능
* 실제 전송은 Webhook 기반 Publisher로 분리

---

# 27. JSON 출력

`ActivityReport`를 직렬화한 JSON을 생성한다.

생성 파일:

```text
generated/report.json
```

JSON에는 다음을 포함할 수 있다.

* 보고 기간
* 생성 시각
* 사용자명
* Public 저장소 활동
* Public 이벤트 및 통계
* Private 집계 통계

JSON에도 Private 원본 데이터는 포함하지 않는다.

Private 이벤트의 저장소 식별자도 포함하면 안 된다.

---

# 28. GitHub Actions 워크플로

다음 파일을 생성한다.

```text
.github/workflows/update-activity-report.yml
```

지원 트리거:

```yaml
on:
  workflow_dispatch:
  schedule:
    - cron: "<generated-cron>"
```

작업 순서:

1. Checkout
2. .NET 설치
3. Restore
4. Build
5. Test
6. Reporter 실행
7. 출력물 Privacy 검증
8. 변경 여부 확인
9. README 및 생성 파일 커밋
10. Profile Repository Push
11. GitHub Pages 배포
12. 선택된 외부 채널 전송
13. 성공 상태 저장

변경이 없으면 빈 커밋을 생성하지 않는다.

기본 커밋 메시지:

```text
chore(profile): update GitHub activity report
```

---

# 29. 인증 및 Secret

MVP에서는 Fine-grained Personal Access Token을 사용한다.

기본 Secret 이름:

```text
GIT_ACCESS_TOKEN
```

필요 권한은 최소 범위로 제한한다.

예:

* Public 저장소 Metadata Read
* 선택된 Private 저장소 Metadata Read
* Pull Requests Read
* Issues Read
* Profile Repository Contents Write

토큰 값은 다음 위치에 저장하면 안 된다.

* YAML 설정 파일
* appsettings.json
* README
* 로그
* 생성된 JSON
* 테스트 Fixture
* 상태 파일
* 예외 메시지

CLI는 토큰 값을 직접 파일에 기록하지 않는다.

Secret 등록은 다음 방식으로 안내하거나 자동화할 수 있다.

```bash
gh secret set GIT_ACCESS_TOKEN
```

이메일과 Slack용 Secret도 설정 파일에는 Secret 이름만 기록한다.

---

# 30. Privacy Validator

`PrivacyValidator`를 구현한다.

모든 출력물은 Publisher 호출 전에 검증을 통과해야 한다.

검사 항목:

* 알려진 Private 저장소명
* Private 저장소 URL
* Private 조직명
* Private PR 제목
* Private Issue 제목
* Private 커밋 메시지
* Branch 이름
* 파일 경로
* 내부 프로젝트 용어
* 고객사명
* 이메일 주소
* 전체 Git SHA 형태
* GitHub Token 형태
* Secret 원문
* 사용자 정의 금지어
* Private 원본 이벤트 객체 직렬화 흔적

검증 실패 예:

```text
Privacy validation failed.

Detected a private repository identifier:
- company-secret-project

Target:
- generated/activity.md

Publishing has been cancelled.
```

검증 실패 시:

* 외부 전송 중단
* Git commit 중단
* Git push 중단
* 상태 파일 갱신 중단
* 안전한 오류 메시지만 출력

---

# 31. 로그 정책

정상 로그 예:

```text
Collected 8 public events.
Collected private activity metrics.
Generated 5 outputs.
Validated 5 outputs.
Published 2 outputs.
```

로그에 출력하면 안 되는 정보:

* Private 저장소명
* Private 저장소 URL
* Private 조직명
* Private PR 및 Issue 제목
* Private 커밋 메시지
* Branch 이름
* 파일 경로
* 토큰
* Secret
* GitHub API 원본 응답
* AI 요청 전문
* AI 응답 전문에 포함된 민감정보

Debug 모드에서도 Private 원문은 출력하지 않는다.

---

# 32. 상태 저장

마지막 성공 실행 상태를 다음 파일에 저장한다.

```text
.activity-reporter/state.json
```

예:

```json
{
  "schemaVersion": 1,
  "reporterVersion": "0.1.0",
  "lastSuccessfulRunAt": "2026-07-27T00:00:00Z",
  "lastReportHash": "sha256-value"
}
```

상태 파일에는 다음만 저장한다.

* 스키마 버전
* Reporter 버전
* 마지막 성공 시각
* 마지막 보고서 해시

Private 원본 이벤트, 저장소명, 저장소 ID는 저장하지 않는다.

---

# 33. 중복 이벤트 처리

GitHub API의 여러 조회 경로에서 같은 활동이 중복 수집될 수 있으므로 중복 제거 로직을 구현한다.

Public 이벤트는 다음 조합을 기반으로 중복을 제거할 수 있다.

* 저장소
* 이벤트 유형
* 이벤트 URL
* 발생 시각

Private 이벤트는 내부 Opaque 식별자와 이벤트 유형, 발생 시각을 기반으로 집계 전에 중복 제거한다.

중복 제거 후 Private 식별자는 폐기한다.

---

# 34. 실행 기간 처리

지원 모드:

```text
last-24-hours
last-7-days
since-last-success
custom
```

기본값:

```text
since-last-success
```

첫 실행에서는 `initial_lookback` 값을 사용한다.

실패한 실행은 `lastSuccessfulRunAt`을 변경하지 않는다.

다음 성공 실행에서 누락된 기간을 포함할 수 있어야 한다.

---

# 35. AI 요약

AI 요약은 Public 활동에만 적용한다.

지원 구조:

```text
IPublicActivitySummarizer
├─ RuleBasedPublicActivitySummarizer
├─ OpenAiPublicActivitySummarizer
└─ CopilotPublicActivitySummarizer
```

요구사항:

* Public 데이터만 입력
* 입력 길이 제한
* 최대 저장소 수 제한
* 저장소별 최대 이벤트 수 제한
* 비용 및 토큰 제한
* 응답 시간 제한
* 실패 시 규칙 기반 요약으로 fallback
* AI 결과도 Privacy Validator 검사
* 사실에 없는 작업을 생성하지 않도록 프롬프트 제한
* 요약은 원본 Public 이벤트 범위를 벗어나면 안 됨

Private 집계에는 AI를 사용하지 않는다.

---

# 36. 테스트 요구사항

## 36.1 Unit Tests

다음 테스트를 작성한다.

* Public 및 Private 활동 분류
* Private 활동 집계
* 활성 Private 저장소 수 계산
* 활동 일수 계산
* 중복 이벤트 제거
* 보고 기간 필터링
* 마지막 성공 실행 이후 기간 계산
* README 마커 교체
* README 사용자 영역 보존
* README 중복 마커 오류
* 설정 파일 검증
* 시간대 변환
* GitHub Actions cron 생성
* 각 Renderer 출력
* 각 Publisher 실패 처리
* Private 정보 노출 탐지
* Secret 마스킹
* AI fallback
* Preview 모드 Publish 방지

## 36.2 Security Tests

다음 테스트는 반드시 작성한다.

1. Private 저장소명이 Markdown에 포함되면 실패
2. Private 저장소명이 SVG에 포함되면 실패
3. Private 저장소명이 HTML에 포함되면 실패
4. Private 저장소명이 JSON에 포함되면 실패
5. Private PR 제목이 이메일에 포함되면 실패
6. Private Issue 제목이 Slack payload에 포함되면 실패
7. Private 파일 경로가 출력에 포함되면 실패
8. GitHub Token이 로그에 포함되면 실패
9. Secret 값이 예외 메시지에 포함되면 실패
10. AI Summarizer가 Private 모델을 입력받지 못하는지 검증
11. Publisher가 Privacy Validator보다 먼저 호출되지 않는지 검증
12. 상태 파일에 Private 식별자가 저장되지 않는지 검증
13. Debug 로그에 Private 원본이 출력되지 않는지 검증

## 36.3 Snapshot Tests

다음 출력에 Snapshot Test를 적용한다.

* Profile Markdown
* SVG Dashboard
* Static HTML
* Email HTML
* Email Plain Text
* Slack JSON
* Normalized Report JSON

---

# 37. 샘플 데이터

Public 테스트 데이터:

```text
Repository: example/public-tool
PR: Improve configuration flow
Issue: Fix connection timeout
Commits: 5
```

Private 테스트 데이터:

```text
Repository: company/secret-project
Organization: company-internal
PR: Internal Feature Alpha
Issue: Client-specific defect
Branch: release/customer-name
File: Assets/Internal/SecretFeature.cs
Commit: Implement confidential workflow
```

Private 샘플의 문자열이 어떠한 출력물, 로그, 상태 파일에도 포함되지 않는 테스트를 작성한다.

---

# 38. Copilot 작업 규칙

다음 파일을 생성한다.

```text
.github/copilot-instructions.md
```

내용:

```md
# Copilot Instructions

This repository generates public-facing reports from GitHub activity.

## Critical privacy rules

Public activity may include repository names, public links, pull request
titles, issue titles, release information, and summaries when allowed by
configuration.

Private activity must be represented only as aggregate numeric metrics.

Never expose private:

- repository names
- repository URLs
- organization names
- commit messages
- commit hashes
- pull request titles
- pull request bodies
- issue titles
- issue bodies
- branch names
- tags
- release names
- file paths
- class names
- method names
- source code
- inferred project topics
- client names
- publisher names
- participant names

Private raw activity must never be sent to an AI summarizer.

AI summarization is allowed only for PublicActivityEvent data.

All rendered outputs must pass PrivacyValidator before publishing.

Do not log private identifiers, secrets, tokens, or raw GitHub API payloads.

Do not store private raw events in generated files or state files.

Preserve manually written README content outside generated markers.

Preview mode must never publish or send external messages.

Do not leave empty implementations or placeholder methods marked as complete.
```

---

# 39. README 필수 내용

프로젝트 README에는 다음 내용을 포함한다.

1. 프로젝트 소개
2. 주요 기능
3. Public 및 Private 처리 차이
4. 개인정보 보호 원칙
5. 설치 방법
6. 초기 설정 방법
7. CLI 명령어
8. 설정 파일 설명
9. GitHub Token 권한
10. GitHub Actions 설치
11. Profile README 적용 방식
12. SVG 대시보드 설명
13. 정적 HTML 사이트 설명
14. 이메일 설정
15. Slack 설정
16. Preview 및 Dry-run
17. 보안 주의사항
18. 로컬 개발 방법
19. 테스트 실행 방법
20. 로드맵
21. 알려진 제한사항

---

# 40. 개발 단계

## Phase 1: Core MVP

구현 범위:

* 솔루션 및 프로젝트 구조
* 핵심 도메인 모델
* 설정 모델
* 설정 파일 로딩 및 검증
* GitHub 인증 확인
* Public 및 Private 활동 수집
* Visibility 분류
* Private 집계
* Public 규칙 기반 요약
* ActivityReport 생성
* Markdown Renderer
* JSON Renderer
* README 마커 갱신
* Local File Publisher
* GitHub Profile Publisher
* Privacy Validator
* 상태 저장
* CLI `init`
* CLI `preview`
* CLI `run`
* CLI `doctor`
* CLI `validate`
* 기본 및 보안 테스트

## Phase 2: Dashboard 및 Web

구현 범위:

* SVG Dashboard Renderer
* Static HTML Renderer
* History JSON
* GitHub Pages Publisher
* 날짜별 활동 추이
* Snapshot Tests

## Phase 3: 외부 배포 채널

구현 범위:

* Email HTML Renderer
* Email Plain Text Renderer
* Email Publisher
* Slack Renderer
* Slack Publisher
* Dry-run
* 채널별 입력 검증
* Mock 기반 전송 테스트

## Phase 4: AI Summary

구현 범위:

* AI Provider 추상화
* Public 전용 OpenAI Provider
* Public 전용 Copilot Provider
* Prompt Template
* 토큰 및 비용 제한
* Timeout
* Retry
* 규칙 기반 fallback
* AI 결과 검증

---

# 41. 구현 우선순위

다음 순서로 구현한다.

```text
1. Privacy 관련 테스트
2. Core 모델
3. 설정 모델 및 검증
4. Public / Private 분류
5. Private 집계
6. Privacy Validator
7. GitHub Collector
8. ActivityReport 생성
9. Markdown Renderer
10. JSON Renderer
11. README Marker Updater
12. CLI init
13. CLI preview
14. CLI run
15. GitHub Profile Publisher
16. GitHub Actions Generator
17. SVG Renderer
18. Static HTML Renderer
19. GitHub Pages Publisher
20. Email 출력
21. Slack 출력
22. AI Public Summary
```

출력 디자인보다 개인정보 보호 기능을 먼저 구현한다.

---

# 42. 완료 조건

다음 조건을 모두 만족해야 Phase 1 완료로 본다.

* `dotnet build` 성공
* `dotnet test` 성공
* CLI에서 `init` 실행 가능
* CLI에서 `preview` 실행 가능
* CLI에서 `run` 실행 가능
* 샘플 설정 생성 가능
* GitHub 인증 상태 검사 가능
* Public 활동 수집 가능
* Private 활동 집계 가능
* Public 활동 상세 출력 가능
* Private 활동은 숫자만 출력
* Private 식별 정보가 모든 출력에서 제거됨
* README 기존 사용자 내용 보존
* README 자동 생성 영역 갱신
* JSON 보고서 생성
* GitHub Profile Repository 갱신
* Preview 모드에서 실제 Publish가 실행되지 않음
* Privacy Validator 실패 시 Publish 중단
* 마지막 성공 실행 상태 저장
* GitHub Actions 워크플로 생성
* README 설치 및 사용법 작성
* 예제 설정 파일 작성
* Copilot Instructions 작성
* 보안 테스트 통과
* 미완성 기능에 명확한 TODO와 사유가 기록됨

전체 프로젝트 완료 조건:

* SVG Dashboard 생성
* Static HTML 생성
* GitHub Pages 배포 가능
* Email HTML 및 Plain Text 생성
* Slack payload 생성
* 선택한 Publisher를 통한 실제 전송 가능
* Public 전용 AI 요약 가능
* AI 실패 시 규칙 기반 fallback 동작
* 모든 출력 Snapshot Test 통과
* 전체 Privacy 테스트 통과

---

# 43. 에이전트 작업 지시

위 명세를 기준으로 전체 프로젝트를 구현한다.

작업 방식:

1. 먼저 저장소 전체 구조를 조사한다.
2. 기존 코드가 있다면 불필요하게 삭제하거나 재작성하지 않는다.
3. `IMPLEMENTATION_PLAN.md`를 작성한다.
4. 구현 단계를 Phase별 체크리스트로 정리한다.
5. Privacy 관련 테스트를 가장 먼저 작성한다.
6. Phase 1 Core MVP를 우선 완성한다.
7. 각 핵심 모듈마다 단위 테스트를 작성한다.
8. 기능 구현 후 `dotnet build`와 `dotnet test`를 실행한다.
9. 실패한 테스트를 수정하고 다시 실행한다.
10. 완료된 항목을 `IMPLEMENTATION_PLAN.md`에 체크한다.
11. 동작하지 않는 빈 메서드나 가짜 구현을 완료된 기능처럼 남기지 않는다.
12. 외부 인증이 필요한 기능은 dry-run과 Mock 테스트를 제공한다.
13. 실제 인증 정보나 Secret을 저장소에 작성하지 않는다.
14. 모든 출력은 Privacy Validator를 통과한 후에만 게시한다.
15. 최종적으로 README에 실행 방법, 설정 방법, 보안 정책, 제한사항을 정리한다.

초기 구현에서 반드시 실제로 동작해야 하는 기능:

* Public 및 Private 활동 수집
* Private 활동 건수 집계
* ActivityReport 생성
* Markdown 출력
* JSON 출력
* Local Preview
* README 마커 갱신
* GitHub Profile Repository 갱신
* Privacy Validator
* 상태 저장
* GitHub Actions 워크플로 생성

외부 서비스 인증이 필요한 기능은 초기 단계에서 다음 수준까지 구현할 수 있다.

* Renderer
* Payload 생성
* 설정 검증
* Dry-run
* Publisher 인터페이스
* Mock 기반 테스트
* 명확한 설정 문서

구현을 완료한 뒤 최종 보고서에 다음을 포함한다.

* 구현된 기능
* 생성 및 수정된 주요 파일
* 빌드 결과
* 테스트 결과
* 실제 실행 방법
* 필요한 GitHub 권한
* 필요한 Secret 목록
* 남아 있는 TODO
* 알려진 제한사항
