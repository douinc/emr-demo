# Windows EMR 시험 대상 앱 — plan

spec: `docs/specs/2026-09-29-windows-emr-target-design.md`

실행 방식: 부모 에이전트 인라인 순차. 모든 태스크가 Core 모델·저장 형식을 공유하는 한
사슬이라(판정 ① 불충족) 병렬·위임 이득보다 인수인계 비용이 크다.

## 태스크

- [x] 1. 솔루션 골격: `windows/EmrDemo.slnx`, Core·App·Tests 프로젝트, `.gitignore`
- [x] 2. Core `AppOptions` 인자 파싱 (TDD)
- [x] 3. Core 모델·`EmrStore` (시드 생성·저장·다시 읽기) (TDD)
- [x] 4. Core `EventLog` (`save`·`field_commit` jsonl) (TDD)
- [x] 5. Core `TextBuffer` (custom 텍스트 편집 규칙) (TDD)
- [x] 6. App: 테마·MainForm 레이아웃·standard 모드 바인딩·저장/새로고침/초기화
- [x] 7. App: custom 텍스트·그리드 컨트롤과 접근성 차단
- [x] 8. App: 관리자 manifest 빌드 옵션, 창 제목·상태 표시줄
- [x] 9. `probe/Probe-Uia.ps1`, README
- [x] 10. CI 워크플로, 러너에서 UIA 프로브·스크린샷 확인
- [ ] 11. 종료 전 `origin/main` 델타 재감사

## 판단 기록

- 시드 환자는 8명. 차트 본문은 이 앱용으로 새로 쓴 합성 기록이다. `index.html`의 예시 기록은
  출처(실제 기록의 비식별본인지)를 확인할 수 없어 복사하지 않고, 진료과·형식만 따랐다. 테스트·프로브가
  기대는 표지 문구(1번 `C.C: 가려움`·`manual squeezing`, 3번 첫 줄 `S`)는 유지한다.
- 테스트 러너는 Microsoft.Testing.Platform(`global.json`의 `test.runner`). .NET 10 SDK는
  VSTest 경로의 `dotnet test`를 거부한다.
- custom 컨트롤은 `Control.Text`와 `Control.Name`을 비워 둔다. 각각 `WM_GETTEXT`·UIA Name과
  UIA AutomationId로 노출되기 때문이다.
- `TextBox`가 포커스 없이 바뀌면 즉시 `field_commit`을 남긴다. `WM_SETTEXT`·UIA SetValue는
  포커스 이탈 이벤트가 없어 이 규칙이 없으면 외부 입력이 로그에서 빠진다.
- 관리자 빌드의 manifest는 빌드 순서와 무관하게 맞게 들어가는지 win-x64 publish 두 순서로 확인했다.
- 이름 없는 WinForms 컨트롤도 UIA AutomationId로 실행마다 바뀌는 숫자 창 ID를 내보낸다(CI 덤프로
  확인). custom 모드 검사는 "AutomationId가 없거나 숫자뿐"을 기준으로 한다.
- 두 모드의 그리드 열 폭은 `GridMetrics.FitWidths` 하나로 계산해, 좁은 창에서도 열 위치가 같다.
- custom 그리드는 셀 선택 시 내용 전체를 선택해 입력하면 교체된다(DataGridView와 같은 동작).
  첫·마지막 셀의 `Tab`은 그리드를 벗어나고, 확정은 `Leave`에서 한다(앱 비활성화로는 확정하지 않는다).
- 프로브 스크립트는 ASCII만 쓴다. Windows PowerShell 5.1은 BOM 없는 스크립트를 ANSI 코드
  페이지로 읽는다. 한글 입력값은 `[char]` 코드로 만든다.

## 남은 문제

- custom 컨트롤은 IME 조합 창 위치를 지정하지 않는다. 조합 중 글자는 IME 기본 창에 보이고
  조합 완료 문자만 컨트롤에 들어갈 것으로 예상한다(미확인 — 한글 IME로 수동 확인 필요).
- UIPI 차단 확인은 일반 계정 Windows PC에서 수동으로 해야 한다(CI 러너는 관리자 세션).
- 고 DPI(125%·150%)에서 고정 픽셀 행 높이·여백이 글꼴과 맞는지 미확인(CI는 96 DPI).
- custom 편집기는 키 입력마다 전체 레이아웃을 다시 잰다. 긴 텍스트에서 레이턴시 측정에 영향을 줄 수 있다(미측정).
- 차트 줄 간격·줄바꿈 위치는 두 모드가 조금 다르다(표준 TextBox와 직접 그리기의 차이).
- 런타임 중 저장소 파일이 손상되면 환자 전환·새로고침에서 예외가 난다(시작 시에만 처리).
