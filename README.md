# GitHub Activity Reporter

공개 GitHub 활동에서 **어떤 작업을 했는지** 수집하고 정리해 프로필 README, JSON, SVG 대시보드, 정적 웹사이트로 만드는 .NET CLI입니다. 커밋·PR·Issue 건수는 작업 설명을 보조하는 참고 지표로만 제공합니다. 비공개 저장소 이름, 조직명, 제목, 링크, 커밋 메시지 같은 식별 정보는 출력 전에 차단됩니다.

## 주요 기능

- 공개 저장소별 변경 파일, 커밋 주제, Pull Request, Issue, Review, Release를 바탕으로 한 작업 내용 요약
- 기간별 핵심 작업과 저장소별 변경 목적을 우선 표시하고 활동 건수는 보조 지표로 제공
- 비공개 활동은 저장소 수와 이벤트 수 등 집계값만 출력
- Markdown, JSON, SVG, 정적 HTML 렌더링
- GitHub 프로필 README의 마커 구간만 안전하게 갱신
- GitHub Pages용 정적 사이트 staging 및 Actions 배포
- 이메일 HTML/plain-text 및 Slack Block Kit 전송
- 공개 활동만 사용하는 OpenAI·GitHub Models AI 요약과 규칙 기반 fallback
- 로컬 preview, dry-run, 설정 진단, 출력 개인정보 검사
- 마지막 성공 시점과 결과 해시를 이용한 증분 실행

## 요구 사항

- [.NET SDK 10](https://dotnet.microsoft.com/)
- GitHub API에 접근할 토큰 또는 인증된 GitHub CLI
- 자동 프로필 배포 시 대상 프로필 저장소와 Actions secret

토큰에는 조회하려는 활동에 필요한 최소 권한만 부여하세요. 비공개 활동을 포함하려면 해당 저장소를 읽을 권한이 필요합니다. 토큰 값은 설정 파일에 저장하지 않고 환경 변수나 GitHub Actions secret으로 전달합니다.

## 빠른 시작

```bash
dotnet restore GitHubActivityReporter.sln
dotnet build GitHubActivityReporter.sln
dotnet run --project src/GitHubActivityReporter.Cli -- init
```

생성된 `activity-reporter.yml`을 확인한 다음 로컬 미리보기를 실행합니다.

```bash
dotnet run --project src/GitHubActivityReporter.Cli -- preview \
  --config activity-reporter.yml
```

실제 파일을 생성하되 외부 변경을 막으려면 다음 명령을 사용합니다.

```bash
dotnet run --project src/GitHubActivityReporter.Cli -- run \
  --config activity-reporter.yml \
  --dry-run
```

## 인증

기본 설정의 `github.token_secret_name` 값은 `GIT_ACCESS_TOKEN`입니다.

PowerShell:

```powershell
$env:GIT_ACCESS_TOKEN = "github-token"
dotnet run --project src/GitHubActivityReporter.Cli -- doctor --config activity-reporter.yml
```

Bash:

```bash
export GIT_ACCESS_TOKEN="github-token"
dotnet run --project src/GitHubActivityReporter.Cli -- doctor --config activity-reporter.yml
```

환경 변수가 없으면 인증된 `gh` CLI를 사용할 수 있습니다. 토큰이나 webhook 값을 설정 파일, 로그, 생성 결과에 넣지 마세요.

## CLI 명령

| 명령 | 용도 |
|---|---|
| `init` | 설정과 Actions workflow를 대화형으로 생성 |
| `configure` | 기존 설정을 대화형으로 수정 |
| `preview` | Publisher를 호출하지 않고 모든 활성 출력을 로컬 생성 |
| `run` | 수집 → 요약 → 렌더링 → 개인정보 검사 → 배포 실행 |
| `doctor` | 설정, 인증, 로컬 환경 진단 |
| `validate` | 생성 파일에서 비공개 식별자와 secret 검사 |
| `install-workflow` | `.github/workflows/update-activity-report.yml` 재생성 |

공통 옵션은 `--config`, `--working-directory`, `--verbose`입니다. `run`은 `--dry-run`, `--profile-path`, `--commit`, `--push`를 추가로 지원합니다.

## 설정

대표적인 출력과 Publisher 설정은 다음과 같습니다.

```yaml
outputs:
  github_profile:
    enabled: true
    renderer: compact-markdown
    target: generated/activity.md
  json:
    enabled: true
    renderer: normalized-json
    target: generated/report.json
  dashboard:
    enabled: true
    renderer: svg-dashboard
    target: generated/activity-dashboard.svg
  website:
    enabled: true
    renderer: static-html
    output_directory: generated/site
    history_days: 30

publishers:
  github_profile:
    enabled: true
  github_pages:
    enabled: true
    output_directory: artifacts/pages
  local:
    enabled: true
    output_directory: artifacts
```

GitHub Pages Publisher를 활성화하려면 `outputs.website.enabled`도 `true`여야 합니다. 실행 시 사이트 번들은 `publishers.github_pages.output_directory`에 staging되고, 생성된 workflow가 이를 GitHub Pages artifact로 업로드합니다. 저장소의 **Settings → Pages → Source**는 GitHub Actions로 설정해야 합니다.

설정 변경 후 검증과 workflow 재생성을 권장합니다.

```bash
dotnet run --project src/GitHubActivityReporter.Cli -- doctor --config activity-reporter.yml
dotnet run --project src/GitHubActivityReporter.Cli -- install-workflow --config activity-reporter.yml
```

## GitHub 프로필 자동 배포

1. `github.profile_repository`에 프로필 저장소 owner, name, branch를 설정합니다.
2. `github.token_secret_name`과 같은 이름으로 reporter 저장소의 Actions secret을 만듭니다.
3. `install-workflow`를 실행하고 생성된 workflow를 커밋합니다.
4. Actions의 `Update GitHub activity report` workflow를 수동 실행해 확인합니다.

Publisher는 프로필 저장소의 `README.md`에서 아래 구간만 교체하며 나머지 사용자 작성 내용은 보존합니다.

```html
<!-- GITHUB_ACTIVITY_REPORTER:START -->
<!-- generated content -->
<!-- GITHUB_ACTIVITY_REPORTER:END -->
```

## 이메일과 Slack

이메일 출력과 전송을 활성화하려면 다음 설정을 사용합니다.

```yaml
outputs:
  email:
    enabled: true
    renderer: email-html
    html_target: generated/email.html
    text_target: generated/email.txt
publishers:
  email:
    enabled: true
    secret_name: EMAIL_CREDENTIALS
```

`EMAIL_CREDENTIALS` 환경 변수 또는 Actions secret 값은 다음 JSON 형식입니다.

```json
{"host":"smtp.example.com","port":587,"username":"user","password":"password","from":"from@example.com","to":"to@example.com","useSsl":true,"subject":"GitHub activity report"}
```

Slack은 `outputs.slack.enabled`와 `publishers.slack.enabled`를 활성화하고, `publishers.slack.secret_name`이 가리키는 환경 변수에 HTTPS incoming webhook URL을 저장합니다. `--dry-run`에서는 자격 증명과 webhook을 읽지 않으며 외부 전송도 하지 않습니다.

## 공개 활동 AI 요약

AI 요약은 기본적으로 꺼져 있습니다. 활성화할 때도 `PublicActivityEvent`만 Provider에 전달되며 비공개 이벤트를 받는 API 자체가 없습니다.

```yaml
privacy:
  public:
    ai_summary: true
  private:
    ai_summary: false
summary:
  use_public_change_details: true
  public_change_detail_level: standard
  ai:
    provider: openai
    model: gpt-5.6-sol
    api_key_secret_name: OPENAI_API_KEY
    include_public_commit_messages: false
    max_input_events: 100
    max_input_characters: 20000
    max_output_tokens: 800
    timeout_seconds: 30
    max_retries: 2
```

OpenAI Provider는 [Responses API](https://developers.openai.com/api/reference/resources/responses/methods/create)를 사용합니다. GitHub Copilot으로 실행하려면 `provider: github-copilot`, `model: auto`, `api_key_secret_name: ACTIVITY_REPORTER_GITHUB_TOKEN`을 설정합니다. Copilot Provider는 Fine-grained PAT와 `Copilot Requests` 권한이 필요하며, Classic PAT(`ghp_` prefix)는 지원하지 않습니다. 생성 workflow는 활동 수집용 `GIT_ACCESS_TOKEN`과 요약용 `ACTIVITY_REPORTER_GITHUB_TOKEN`을 함께 사용합니다.

### GitHub Copilot 설정 예시

```yaml
summary:
  ai:
    provider: github-copilot
    model: auto
    api_key_secret_name: ACTIVITY_REPORTER_GITHUB_TOKEN
```

### GitHub Copilot 토큰 안내

- Fine-grained personal access token 필요
- `Copilot Requests` 권한 필요
- Classic PAT(`ghp_` prefix) 미지원
- 기존 활동 수집 및 프로필 저장소 권한도 동일 토큰에 포함
- 토큰 값은 설정 파일에 직접 기록하지 않고 Secret으로 관리

```bash
gh secret set ACTIVITY_REPORTER_GITHUB_TOKEN
```

### GitHub Models에서 마이그레이션

```yaml
# 이전
summary:
  ai:
    provider: github-models
    model: openai/gpt-4.1

# 변경
summary:
  ai:
    provider: github-copilot
    model: auto
    api_key_secret_name: ACTIVITY_REPORTER_GITHUB_TOKEN
```

AI에는 설정에서 노출을 허용한 공개 제목과 공개 변경 메타데이터만 전달됩니다. 요청은 실행당 한 번으로 제한되고 입력 이벤트·문자·출력 토큰, timeout, retry 상한을 적용합니다. 응답이 실패하거나 형식 검증을 통과하지 못하면 규칙 기반 요약으로 자동 복구됩니다. 규칙 기반 요약도 공개 제목, 저장소 설명, 변경 파일 경로, diff 통계를 바탕으로 실제 작업 주제를 먼저 설명하며 활동 건수는 별도의 참고 지표로만 표시합니다.

더 구체적인 변경 주제 요약이 필요하면 `summary.ai.include_public_commit_messages: true`를 명시적으로 설정할 수 있습니다. 이 옵션은 공개 저장소의 커밋 제목을 AI 입력 근거로만 사용하며, `privacy.public.expose_commit_messages: false`인 동안 Markdown과 JSON에는 원문 커밋 이벤트를 출력하지 않습니다. AI 결과가 커밋 제목을 그대로 복제하면 검증에서 거부하고 규칙 기반 요약으로 fallback합니다. 정상 응답은 전체 기간 headline, 핵심 highlight 최대 5개, 저장소별 요약으로 구성됩니다.

## 개인정보 보호

- 비공개 이벤트는 수집 직후 집계되며 출력 모델에 식별 정보를 전달하지 않습니다.
- `privacy.private.mode`는 `aggregate-only`만 허용합니다.
- 비공개 저장소명, 조직명, 제목, 링크, 브랜치, 파일 경로, 토픽 노출 옵션은 활성화할 수 없습니다.
- 모든 렌더링 결과는 Publisher 실행 전에 개인정보 검사를 통과해야 합니다.
- `privacy.custom_forbidden_terms`에 추가 금지 문자열을 등록할 수 있습니다.
- `preview`는 Publisher를 호출하지 않으며, `run --dry-run`은 Publisher의 파일 쓰기·commit·push·전송을 차단합니다.

생성 결과를 별도로 검사할 수도 있습니다.

```bash
dotnet run --project src/GitHubActivityReporter.Cli -- validate \
  --config activity-reporter.yml \
  --path artifacts
```

## 개발 및 테스트

```bash
dotnet test GitHubActivityReporter.sln
```

렌더러 테스트에는 SVG와 정적 사이트 번들의 승인된 SHA-256 스냅샷이 포함됩니다. 의도적으로 출력 형식을 변경했다면 변경 내용을 검토한 뒤 스냅샷 매니페스트를 갱신하세요.

구현 단계와 후속 작업은 [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md)와 [UNRESOLVED_ISSUES.md](UNRESOLVED_ISSUES.md)에서 관리합니다.
