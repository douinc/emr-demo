# Windows EMR 시험 대상 앱 — 설계

- 작성일: 2026-09-29
- 근거: 내부 기술 검토 "EMR 윈도우 뷰어에 프론트 접근만으로 데이터 공급" (2026-09-29).
  아래 ①~⑥은 그 문서의 구성 요소 번호다.

## 목적

검토 문서의 에이전트 파이프라인(창 식별 → 캡처 → UIA/비전 분기 → 입력 합성 → 재캡처
검증)을 실제 벤더 프로그램 없이 시험할 **가짜 EMR 벤더 프로그램**을 만든다.
검토 문서가 "남은 미지수"로 둔 실제 벤더 속성(UIA 노출 여부·보안 솔루션 간섭)은 이
앱으로 확정되지 않는다. 이 앱은 그 프로브 전에 에이전트 쪽을 완성·계측하는 용도다.

## 기술 선택

C# WinForms, .NET 10 LTS(SDK 10.0.401). Electron·WPF는 UIA 트리를 자동으로 풍부하게
노출해 "잡히지 않는 벤더"를 재현할 수 없고, WinForms는 표준 Win32 컨트롤(`Edit` 클래스 →
`WM_SETTEXT` 가능)과 직접 그린 컨트롤을 한 앱에서 둘 다 만들 수 있다.
Windows 전용이며 macOS 빌드는 만들지 않는다.

## 확정 범위 (1차)

| 항목 | 재현 조건 | 검토 문서 대응 |
|---|---|---|
| `--ui=standard` (기본) | 차트 기록=`TextBox`, 바이탈·처방 그리드=`DataGridView` | ③ UIA 경로, ④ ValuePattern·`WM_SETTEXT` |
| `--ui=custom` | 같은 세 영역을 `OnPaint`로 직접 그린 컨트롤로 교체. UIA에는 이름·패턴·자식 없는 Pane 하나로 보인다(AutomationId는 실행마다 바뀌는 숫자 창 ID뿐이라 앵커로 쓸 수 없다) | ③ 비전 폴백 |
| 한글 입력 | 차트 기록·메모·처방명은 한글 자유 입력. custom 컨트롤도 `WM_CHAR`(IME 조합 완료 문자)·`Ctrl+V`를 받는다 | ④ IME 우회 |
| 저장·다시 불러오기 | `저장` 버튼이 JSON 저장소에 쓰고, 환자 전환·`새로고침` 시 저장소에서 다시 읽는다. 저장하지 않은 편집은 환자 전환 시 버린다 | ⑤ 검증 루프 |
| 정답 로그 | 데이터 디렉터리의 `events.jsonl`에 `save`(전체 스냅샷)와 `field_commit`(필드 단위 확정) 이벤트를 UTC ms 시각과 함께 한 줄씩 기록 | 정확도 채점·레이턴시 실측 |
| 관리자 빌드 | `-p:RequireAdmin=true`로 `requireAdministrator` manifest를 넣은 빌드. 코드는 같다 | ④ UIPI |

2차(이번 범위 밖): `--layout=v2`(위치·AutomationId를 바꾼 "벤더 업데이트" 화면) —
에이전트에 self-healing 앵커 캐시가 생긴 뒤에 붙인다.

## 화면

`index.html`의 처방관리 화면(Ver. 2.0.0.122, XP풍 색)을 시각 원본으로 삼아 축약 재현한다.
창 제목과 테두리는 OS 네이티브 창을 쓴다(HTML의 가짜 타이틀바는 재현하지 않는다).

- 창 제목: `[접속정보 : Tester1] 처방관리 Ver. 2.0.0.122 : [ 예시 환자 NN ]`
  (관리자 권한으로 실행되면 끝에 ` (관리자)`)
- 메뉴 바, 환자 헤더(환자 번호·성명·진료과·진료일자 등 읽기 전용 필드)
- 좌: 환자 목록 8명 (`DEMO-01`~`DEMO-08`, 이름 `예시 환자 NN`)
- 중앙: 차트 기록(여러 줄 자유 기록)
- 중간: VitalSign 그리드(측정일·시간·SBP·DBP·맥박·체온, 편집 가능), 상병 그리드(읽기 전용),
  메디칼메모·환자메모(`TextBox`, 두 모드 공통)
- 우: 처방 그리드(처방명·용량·횟수·일수·용법, 편집 가능), 하단 버튼 `초기화`·`새로고침`·`저장`
- 하단 상태 표시줄: UI 모드, 권한 레벨, 데이터 디렉터리, 마지막 저장 시각

표준 모드의 편집 컨트롤에는 AutomationId로 쓰일 고정 `Name`을 준다:
`chartText`, `medicalMemo`, `patientMemo`, `vitalGrid`, `orderGrid`, `patientList`,
`saveButton`, `refreshButton`, `resetButton`.

## 데이터

- 데이터 디렉터리: `--data-dir=<경로>`, 없으면 `%LOCALAPPDATA%\EmrDemo`.
- `store.json`: 환자 배열. 파일이 없으면 시드(이 앱용으로 새로 쓴 **합성** 진료기록 8건 — 진료과와
  기록 형식만 `index.html` 예시를 따른다)로 만든다. `초기화` 버튼은 현재 환자를 시드 값으로 되돌린다(저장 전까지는 화면만).
- `events.jsonl` 한 줄 형식:
  ```json
  {"ts":"2026-09-29T03:12:45.123Z","type":"save","patientId":"DEMO-01","uiMode":"custom","elevated":false,"patient":{...}}
  {"ts":"...","type":"field_commit","patientId":"DEMO-01","uiMode":"standard","elevated":false,"field":"orders[0].name","value":"..."}
  ```
  `field` 경로: `chartText`, `medicalMemo`, `patientMemo`, `vitals[i].<col>`, `orders[i].<col>`.
  `field_commit` 시점: `TextBox`·custom 텍스트는 포커스를 잃을 때 값이 바뀌었으면,
  `TextBox`가 포커스 없이 바뀌면(`WM_SETTEXT`·UIA SetValue) 즉시, 그리드는 셀 값이
  확정될 때(편집 종료·UIA SetValue) 값이 바뀌었으면. 저장 직전에도 포커스 중인 필드의
  미기록 변경을 먼저 기록한다. 환자 화면을 불러오는 동안의 값 변경은 기록하지 않는다.

## 구조

```
windows/
  EmrDemo.slnx
  src/EmrDemo.Core/      net10.0 — 모델, 저장소, 이벤트 로그, 인자 파싱, 텍스트 편집 버퍼
  src/EmrDemo.App/       net10.0-windows WinForms — 화면, custom 컨트롤
  tests/EmrDemo.Core.Tests/  xUnit — Core만 (macOS에서도 실행)
  probe/Probe-Uia.ps1    UIA 트리 덤프·모드별 기대 검사 (검토 문서 프로브 1번의 스크립트판)
  README.md
.github/workflows/windows-demo.yml  windows 러너: 빌드·테스트·두 모드 실행·UIA 프로브·스크린샷
```

custom 컨트롤의 편집 규칙(삽입·삭제·커서 이동·전체 선택·붙여넣기)은 WinForms와 무관한
`TextBuffer`로 Core에 두어 macOS에서 단위 테스트한다. 그리기·입력 이벤트 연결만 App에 둔다.

## 검증

- Core 단위 테스트 (macOS·Windows 러너 둘 다)
- Windows 러너: 두 모드로 앱을 띄워 `Probe-Uia.ps1`이 standard에서는 `chartText` 등의
  Value 패턴을, custom에서는 해당 영역이 자식 없는 Pane이고 Value 패턴이 없음을 확인.
  `chartText`에 ValuePattern으로 값을 쓰고 저장 버튼을 Invoke한 뒤 `events.jsonl`의
  `save` 스냅샷에 그 값이 있는지 확인. 창 스크린샷을 아티팩트로 올린다.
- 관리자 빌드는 러너에서 빌드만 확인한다(러너 계정은 이미 관리자라 UIPI 재현 불가).
  UIPI 차단 확인은 일반 계정 Windows PC에서 수동으로 한다.

## 가정·미확정

- 가정: GitHub 호스티드 Windows 러너가 대화형 데스크탑 세션을 제공해 WinForms 창 실행·UIA
  조회·스크린샷이 된다. 안 되면 CI는 빌드·테스트만 하고 UI 확인은 수동으로 옮긴다.
- 미확정: 에이전트 쪽 연동 방식. 이 앱은 에이전트 코드에 의존하지 않는다.
