# Windows EMR 시험 대상 앱 — 구조화 서식지 plan

spec: `docs/specs/2026-09-29-windows-ob-forms-design.md`

실행 방식: 부모 에이전트 인라인 순차. 스키마 모델 → 배치 → 두 모드 렌더러 → 프로브가 한 사슬로
묶이고(판정 ① 불충족), 배치 좌표가 두 렌더러의 공유 계약이다.

## 태스크

- [x] 1. 내보내기 스크립트와 `Seed/forms.json` (6종, 빈 값, 키 523개 고유)
- [x] 2. Core `FormDefinition` 모델·로더 (TDD)
- [x] 3. Core `FormLayout` 배치 (TDD, 가짜 측정 함수)
- [x] 4. Core `Patient.FormId`·`FormValues`, 시드 `DEMO-09`~`DEMO-14`, `EmrFields.Form` (TDD)
- [x] 5. App standard 서식 뷰(네이티브 컨트롤, AutomationId, 확정 규칙)
- [x] 6. App custom 서식 캔버스(그리기·입력·드롭다운·스크롤·opaque)
- [x] 7. MainForm 서식 모드 전환(가운데 칸 확장), `layout-<formId>.json` 기록
- [x] 8. 프로브 서식 검사, README
- [x] 9. CI 확인, 종료 전 `origin/main`·PR #2 델타 재감사 (둘 다 변화 없음, 2026-09-29)

## 판단 기록

- 배치 폭은 860px 고정(웹 form-mode 가운데 칸 폭). 창 크기와 무관해야 정답 좌표가 실행마다 같다.
- 네이티브 Label·RadioButton·CheckBox는 글자 폭 외 여백이 필요해 `FormLayout.TextSlack`(8px)을 둔다.
  CI 캡처에서 여백이 없을 때 standard만 글자가 잘리는 것을 확인했다.
- 차트와 서식은 같은 표 칸에 바꿔 끼운다. 감싸는 Panel을 두면 그 Panel이 앞 라벨 글자를 UIA Name으로 얻어
  (CI 덤프로 확인) UIA 구조가 PR #2와 달라진다.
- standard 라디오는 `AutoCheck=false`로 그룹을 직접 관리한다(모든 컨트롤이 한 Panel에 있어 WinForms 기본
  그룹이 서식 전체가 된다). UIA SelectionItem.Select가 Click으로 이어지는 것을 CI 프로브로 확인했다.
- `DateTimePicker`는 UIA에서 ComboBox(Value·Toggle·ExpandCollapse)다. 체크 해제 상태에서도 회색 날짜가 보이고
  UIA Value가 날짜 문자열이라 입력된 값으로 오인됐다(CI 캡처·덤프). 빈 값일 때 `CustomFormat=" "`로 날짜를
  감추게 했고, 이제 UIA Value는 `" "`다(CI 덤프로 확인).
- 확정 시점: `DateTimePicker`는 칸 편집마다 ValueChanged가 나서, 포커스가 없을 때(UIA 쓰기)만 즉시 기록하고
  나머지는 Leave·CloseUp에서 기록한다. custom 캔버스는 편집 중인 칸을 다시 클릭하면 커서만 옮긴다.
- 너비 없는 표 열은 남은 폭을 나눠 갖는다(웹 `width:100%`). standard DataGridView는 스키마가 아니라 배치의
  셀 폭을 쓴다.
- 저장소의 서식 id가 카탈로그에 없으면 시작 시 거부한다(`InvalidDataException`). 서식 기록 이전 버전이 만든
  저장소에는 빠진 시드 기록을 뒤에 보충한다.
- gitleaks generic-api-key 규칙이 `forms.json`의 `"key"` 값 69건을 잡는다. 전부 서식 필드 키(`csec.b2.0` 형식)임을
  확인했다(오탐). 저장소 gitleaks 설정 추가는 범위 밖.

## 남은 문제

- custom 텍스트 영역(textarea)은 자동 줄바꿈하지 않는다(줄바꿈 문자로만 줄이 나뉜다). 칸 밖 글자는 잘린다.
  standard는 스크롤 막대를 없앴고 자동 줄바꿈은 한다 — 긴 글에서 두 모드 모양이 다르다.
- standard 읽기 전용 TextBox에 UIA SetValue가 막히는지는 확인하지 않았다.
- custom 키보드 조작(Tab·화살표·Space·드롭다운 키)과 날짜 입력, 서식 환자 초기화는 자동 검사가 없다.
- custom 날짜는 형식이 틀리면 조용히 이전 값으로 되돌린다(오류 표시 없음).
- standard 서식 화면은 서식별로 한 번 만들고 캐시한다. 첫 표시 시간은 측정하지 않았다.
