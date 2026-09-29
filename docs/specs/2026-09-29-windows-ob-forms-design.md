# Windows EMR 시험 대상 앱 — 구조화 서식지

- 작성일: 2026-09-29
- 선행: `docs/specs/2026-09-29-windows-emr-target-design.md` (standard·custom 모드, 저장·정답 로그)

## 목적

화면 에이전트의 주 과제는 **서식 입력**이다. 텍스트뿐 아니라 체크박스·라디오·드롭다운·날짜·표를
채워야 하고, 이들은 UIA 패턴(Toggle·SelectionItem·ExpandCollapse)도, 비전 조작 방식(상태 읽기 →
클릭·토글, 드롭다운 목록 재캡처)도 텍스트와 다르다. 웹 데모(`ob-forms.js`·`ob-forms-data.js`)의
산과 서식 6종을 두 UI 모드로 재현한다.

## 확정 범위

- 서식 6종 전부: Labor record Ⅱ-OB, 수술기록-OB C/sec, 산전초음파 1st trimester,
  산전초음파 2nd & 3rd trimester, Fetal echocardiography, Fetal Neurosonography.
- 서식 정의는 웹 스키마에서 `windows/tools/export-ob-forms.mjs`로 `Seed/forms.json`을 만든다.
  라벨·선택지·배치 같은 **구조만** 가져오고 예시 값은 모두 버린다 — 모든 서식은 빈 상태로 시작한다
  (예시 값의 출처를 확인할 수 없고, 에이전트가 채울 대상이기 때문).
- 환자 목록에 서식 기록 6건을 더한다: `DEMO-09`~`DEMO-14`(`예시 환자 NN`, 진료과 산부인과).
  서식 기록을 고르면 차트 칸 자리에 서식이 뜨고, 웹처럼 가운데 칸을 넓힌다(중간 열을 숨김).

## 컨트롤과 값

| 스키마 | standard | custom | 값(`form.<key>`) |
|---|---|---|---|
| text / 읽기 전용 text | `TextBox`(`ReadOnly`) | 직접 그림(읽기 전용은 입력 무시) | 문자열 |
| textarea | 여러 줄 `TextBox` | 직접 그림 | 문자열(`\n`) |
| date | `DateTimePicker`(체크박스 포함, 해제=빈 값) | 한 줄 입력(`YYYY-MM-DD`) | `yyyy-MM-dd` 또는 빈 값 |
| select | `ComboBox`(DropDownList, 첫 항목 빈 값) | 직접 그린 드롭다운(캔버스 안에 목록을 그림) | 선택지 문자열 |
| radio | `RadioButton`(그룹 단위로 하나만) | 직접 그림 | 선택지 문자열 |
| check | `CheckBox` | 직접 그림 | 선택된 선택지를 `|`로 이은 문자열(스키마 순서) |
| grid | `DataGridView` | 직접 그린 셀 | 셀별 `form.<key>[r][c]` |
| button·label·section 등 | `Button`(동작 없음)·`Label` | 직접 그림 | 없음 |

- 키는 내보내기 스크립트가 `<formId>.b<블록 번호>.<항목 번호>`로 붙이고, 파생 입력은 접미사를 쓴다
  (`.other`, `.tail`, 인라인 필드 `.<선택지 번호>.<번호>`, 작성일 `<formId>.writtenOn`).
- standard 모드 AutomationId = 키. 라디오·체크 선택지는 `<key>#<선택지 번호>`, Name은 선택지 문자열.
- custom 모드는 서식 전체가 **컨트롤 하나**다(PowerBuilder DataWindow와 같은 모양). UIA에는 이름·패턴·
  자식 없는 Pane 하나로 보인다.
- `Patient`에 `FormId`와 `FormValues`(키 → 문자열)를 더한다. 저장 스냅샷에 들어간다.
- `field_commit`: 텍스트·날짜·셀은 기존 규칙(포커스 이탈·포커스 없는 변경·셀 확정), 체크·라디오·
  드롭다운은 값이 바뀌는 즉시. field는 `form.<key>`.

## 배치와 정답 좌표

- 두 모드가 같은 화면이 되도록, 배치는 Core의 `FormLayout`이 스키마와 글자 폭 측정 함수로 계산한다.
  standard는 그 좌표에 네이티브 컨트롤을 놓고, custom은 같은 좌표에 그린다.
- 서식을 띄울 때 데이터 디렉터리에 `layout-<formId>.json`(요소별 키·선택지·종류·사각형, 서식 콘텐츠
  좌표)을 쓴다. 비전 에이전트 채점과 프로브의 클릭 좌표에 쓴다. 에이전트 입력으로 쓰면 안 된다.
- 규칙(웹 CSS를 단순화): 제목줄(작성일·제목·진료과), 섹션 머리, 행 = 라벨 열(`labelWidth`, 들여쓰기
  14px 단위) + 컨트롤 흐름 배치(간격 4px, 폭 초과·`break`에서 줄바꿈), stack 선택지는 세로, 표는 머리줄
  + 헤더 + 행, set·서명 블록은 테두리 상자.

## custom 모드 조작

- 클릭으로 요소 선택. `Tab`/`Shift+Tab`으로 입력 요소 이동. 텍스트는 기존 `TextBuffer` 규칙.
- 체크·라디오: 클릭 또는 포커스 후 `Space`. 드롭다운: 클릭하면 목록이 열리고 항목 클릭·`↑↓`+`Enter`로
  고르며 `Esc`로 닫는다. 마우스 휠로 세로 스크롤, `Shift`+휠로 가로 스크롤.

## 검증

- Core: 스키마 읽기(6종, 키 고유), 배치(겹침 없음·줄바꿈·표 크기, 가짜 측정 함수로), 서식 값 저장.
- 프로브 standard: 서식 기록에서 text 쓰기, 라디오 SelectionItem, 체크 Toggle, 콤보 선택, 표 셀 쓰기 →
  저장 스냅샷·`field_commit` 확인.
- 프로브 custom: 서식 Pane이 opaque하고 값이 UIA·`WM_GETTEXT`로 안 읽힘, `layout-<formId>.json`
  좌표로 클릭·키 입력한 값이 저장·기록됨.

## 가정·미확정

- 가정: `DateTimePicker`의 UIA 노출 형태는 CI 덤프로 확인한 뒤 프로브 검사를 정한다.
- 가정: 서식의 `draw`(그림 작성 영역)는 웹처럼 비활성 영역으로 둔다.
