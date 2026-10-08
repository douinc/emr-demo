# EmrDemo — Windows EMR 시험 대상 앱

화면 에이전트(창 식별 → 캡처 → UIA/비전 → 입력 → 재캡처 검증)를 실제 벤더 프로그램 없이
시험하기 위한 **가짜 EMR 벤더 프로그램**이다. 화면은 저장소 루트 `index.html`의 처방관리
화면을 축약 재현한다. 설계: [`docs/specs/2026-09-29-windows-emr-target-design.md`](../docs/specs/2026-09-29-windows-emr-target-design.md)

환자·진료기록은 모두 이 앱용으로 새로 쓴 **합성 데이터**다(`src/EmrDemo.Core/Seed/records.json`).

이 앱으로 확인되는 것은 에이전트 쪽 파이프라인이다. 실제 벤더 프로그램의 UIA 노출 여부나
보안 솔루션 간섭은 실제 프로그램이 깔린 PC에서 따로 확인해야 한다.

## 실행

Windows 10/11에서 실행한다. [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)(10.0.401 이상)를 설치한다.

```powershell
git clone https://github.com/douinc/emr-demo.git
cd emr-demo\windows

# 바로 실행 (`--` 뒤가 앱 인자)
dotnet run --project src/EmrDemo.App -- --ui=custom

# exe로 만들어 실행
dotnet publish src/EmrDemo.App -c Release -r win-x64 --self-contained false -o out/app
.\out\app\EmrDemo.exe --ui=custom --data-dir=C:\emr-test\run1
```

`out/app` 폴더는 다른 PC로 복사해 실행할 수 있다. 그 PC에는 SDK 대신
[.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)만 있으면 된다.
시험마다 `--data-dir`을 새 폴더로 주면 시드 상태에서 시작하고, 그 폴더의 `events.jsonl`만 보면 된다.

## 실행 모드

| 인자 | 재현 조건 |
|---|---|
| `--ui=standard` (기본) | 차트 기록=`TextBox`, 바이탈·처방=`DataGridView`. UIA AutomationId·ValuePattern·`WM_SETTEXT`가 통한다 |
| `--ui=custom` | 같은 세 영역을 직접 그린 컨트롤로 바꾼다. UIA에는 이름·값·패턴·자식 없는 Pane으로만 보이고(AutomationId는 실행마다 바뀌는 숫자뿐) `WM_GETTEXT`도 비어 있다 |
| `--data-dir=<경로>` | 저장소·로그 위치. 기본 `%LOCALAPPDATA%\EmrDemo` |

메모 두 칸과 상병 그리드는 두 모드 모두 표준 컨트롤이다(한 화면에 두 종류가 섞인 벤더 재현).
`Ctrl+S`는 `저장` 버튼과 같다.

표준 모드 AutomationId: `chartText` `medicalMemo` `patientMemo` `vitalGrid` `orderGrid`
`diagnosisGrid` `patientList` `saveButton` `refreshButton` `resetButton`

## 구조화 서식지

환자 목록의 `DEMO-09`~`DEMO-14`는 산과 서식 기록이다(Labor record Ⅱ, 수술기록 C/sec, 산전초음파 1st·
2nd&3rd, Fetal echocardiography, Fetal Neurosonography). 고르면 차트 칸 자리에 서식이 뜨고 중간 열이 숨는다.
서식 정의는 루트의 `ob-forms*.js`에서 `node windows/tools/export-ob-forms.mjs`로 만든
`src/EmrDemo.Core/Seed/forms.json`이다. 구조만 가져오고 예시 값은 버려 **모든 서식이 빈 상태로 시작**한다.

| 스키마 | standard | custom | 저장 값 |
|---|---|---|---|
| text(읽기 전용 포함)·textarea | `TextBox` | 직접 그림 | 문자열 |
| date | `DateTimePicker`(체크 해제 = 빈 값, 날짜 글자를 감춤) | 한 줄 입력 `YYYY-MM-DD`(형식이 틀리면 되돌림) | `yyyy-MM-dd` 또는 없음 |
| select | `ComboBox`(첫 항목 빈 값) | 캔버스 안에 그린 목록 | 선택지 |
| radio | `RadioButton` | 직접 그림 | 선택지 |
| check | `CheckBox` | 직접 그림 | 선택지를 스키마 순서로 `|`로 이음 |
| grid | `DataGridView`(첫 열은 행 번호) | 직접 그린 셀 | 셀별 `<키>[행][열]` |

- 값은 저장 스냅샷의 `patient.formValues`(키 → 문자열)에 들어가고, `field_commit`의 field는 `form.<키>`다.
  체크·라디오·드롭다운은 바뀌는 즉시, 텍스트·날짜·셀은 기존 규칙대로 기록한다.
- 키는 `<서식id>.b<블록 번호>.<항목 번호>`(파생 입력은 `.other`·`.tail`·`.<선택지>.<번호>`, 작성일은
  `<서식id>.writtenOn`). standard 모드 AutomationId는 키이고, 라디오·체크 선택지는 `<키>#<선택지 번호>`다.
- standard `DateTimePicker`는 UIA에서 ComboBox(Value·Toggle·ExpandCollapse)로 보인다. 빈 날짜(체크 해제)는
  화면에 날짜를 감추고 UIA Value가 공백 한 칸(`" "`)이다. 날짜를 넣으려면 Toggle로 체크한 뒤 값을 바꾼다.
- custom 모드는 서식 전체가 컨트롤 하나다. 클릭으로 요소를 고르고 `Tab`/`Shift+Tab`으로 이동, 체크·라디오는
  클릭 또는 `Space`(그룹 안은 화살표), 드롭다운은 클릭·`Space`로 열어 항목 클릭 또는 `↑↓`+`Enter`, `Esc`로
  닫는다. 휠로 세로, `Shift`+휠로 가로 스크롤(스크롤 막대는 표시만 하고 끌 수 없다).
- Tab 정지점은 두 모드가 다르다: standard는 라디오·체크 선택지마다, custom은 선택지 그룹마다 하나다.
  "Tab을 N번" 같은 절차는 모드마다 다른 칸에 닿는다.
- 두 모드는 같은 배치 좌표를 쓴다(Core `FormLayout`, 배치 폭 860px 고정). 서식을 띄울 때마다 데이터
  디렉터리에 **정답 좌표** `layout-<서식id>.json`(입력 요소별 키·종류·선택지·사각형, 서식 콘텐츠 좌표·스크롤 0
  기준)을 쓴다. 비전 에이전트 채점과 프로브 클릭에 쓰며, 에이전트 입력으로 주면 안 된다. 이 파일과
  `events.jsonl`은 정답이므로, 시험하는 에이전트가 데이터 디렉터리를 읽을 수 없게 둔다.

## 동작 규칙

- `저장`: 현재 환자를 `store.json`에 쓰고 `events.jsonl`에 `save` 스냅샷을 남긴다.
- 환자 전환·`새로고침`: 저장소에서 다시 읽는다. **저장하지 않은 편집은 버려진다.**
- `초기화`: 현재 환자를 처음 시드 값으로 화면에만 되돌린다(저장 전까지 저장소는 그대로).
- custom 모드 편집: 클릭한 위치에 커서, 문자 입력(IME 조합 완료 문자), `Backspace`·`Delete`,
  화살표·`Home`·`End`(`Shift`로 선택, `Home`·`End`는 표시 줄이 아니라 문단 단위), `Ctrl+A/C/X/V`.
  그리드는 셀을 고르면 내용 전체가 선택된 편집 상태가 되어 입력하면 교체되고, `Enter`·`Tab`·
  `↑↓`·다른 셀 클릭·포커스 이탈 때 확정, `Esc`로 취소한다. 첫·마지막 셀에서 `Tab`은 그리드를 벗어난다.
- 저장 실패(파일 잠김·권한)는 상태 표시줄에 `저장 실패: …`로 보이고, 저장소를 읽을 수 없으면 시작 시
  오류 창을 띄우고 종료한다(종료 코드 3).

## 정답 로그 (`events.jsonl`)

에이전트가 넣으려던 값과 비교해 정확도를 채점하고, 시각으로 레이턴시를 잰다.

```json
{"ts":"2026-09-29T03:12:45.123Z","uiMode":"custom","elevated":false,"type":"save","patientId":"DEMO-01","patient":{...}}
{"ts":"2026-09-29T03:12:44.870Z","uiMode":"standard","elevated":false,"type":"field_commit","patientId":"DEMO-01","field":"orders[0].name","value":"..."}
```

- `field`: `chartText` `medicalMemo` `patientMemo` `vitals[i].<date|time|sbp|dbp|pulse|temp>`
  `orders[i].<name|dose|frequency|days|route>`
- `field_commit`: 포커스를 잃을 때 값이 바뀌었으면 기록한다. `TextBox`가 포커스 없이 바뀌면
  (`WM_SETTEXT`·UIA SetValue) 즉시, 그리드는 셀 값이 확정될 때 기록한다. 저장 직전에도
  미기록 변경을 먼저 기록한다.

## 빌드

.NET SDK 10.0.401 이상(`global.json`). 코드는 macOS에서도 빌드·테스트할 수 있지만 실행은
Windows에서만 된다.

```powershell
cd windows
dotnet test --solution EmrDemo.slnx
dotnet publish src/EmrDemo.App -c Release -r win-x64 --self-contained false -o out/app
dotnet publish src/EmrDemo.App -c Release -r win-x64 --self-contained false -p:RequireAdmin=true -o out/app-admin
```

`out/app-admin/EmrDemo.exe`는 `requireAdministrator` manifest가 들어간 빌드다. 실행하면 UAC
확인 창이 뜨고 창 제목 끝에 `(관리자)`가 붙는다. 일반 권한 에이전트에서 이 창으로 보낸
`SendInput`·`WM_SETTEXT`·UIA 쓰기가 UIPI로 막히는지 확인하는 용도다. 관리자 계정으로
로그인했더라도 UAC가 켜져 있으면 일반 실행 프로세스는 일반 권한이다.

`tests/EmrDemo.Wpf.Tests`는 Windows에서만 돈다(net10.0-windows).

## WPF판 (EmrDemoWpf)

`src/EmrDemo.Wpf`는 같은 화면·데이터를 표준 WPF 컨트롤로 만든 판이다. BESTCare 2.0 같은 WPF EMR의
UIA 구조에서 에이전트를 시험하려는 용도다. 설계: [`docs/specs/2026-10-08-windows-wpf-target-design.md`](../docs/specs/2026-10-08-windows-wpf-target-design.md)

```powershell
cd windows
dotnet run --project src/EmrDemo.Wpf -- --data-dir=C:\emr-test\wpf1
dotnet publish src/EmrDemo.Wpf -c Release -r win-x64 --self-contained false -o out/app-wpf
dotnet publish src/EmrDemo.Wpf -c Release -r win-x64 --self-contained false -p:RequireAdmin=true -o out/app-wpf-admin
.\out\app-wpf\EmrDemoWpf.exe --data-dir=C:\emr-test\wpf1
```

- 실행 파일·프로세스 이름은 `EmrDemoWpf`다. 기본 데이터 디렉터리는 `%LOCALAPPDATA%\EmrDemoWpf`다.
- standard 모드만 있다. `--ui=custom`은 오류 창을 띄우고 종료 코드 2로 끝난다.
- 창 제목, 환자·서식, 저장·새로고침·초기화, `store.json`·`events.jsonl`·`layout-<서식id>.json`은 WinForms판과 같다.
  AutomationId도 같다(서식 키, `<키>#<선택지 번호>`, `patientList`·`chartText` 등).
- 날짜는 OS 언어와 상관없이 `yyyy-MM-dd`로 보이고 읽는다. 앱이 ko-KR 문화권을 복제해 짧은 날짜 형식만 바꿔 쓴다.
  .NET 10의 ICU ko-KR 기본값은 `yyyy. M. d.`다.
- 배치 좌표는 같은 규칙(`FormLayout`, 폭 860px)이지만 글자 폭을 WPF로 재므로 WinForms판과 몇 px 다를 수 있다.
  정답 좌표는 각 앱이 쓴 `layout-<서식id>.json`을 본다.

WinForms판과 UIA가 다른 점:

- 창 핸들(HWND)은 주 창과 팝업(메뉴·드롭다운·달력)에만 있다. 칸마다 창이 아니어서 `WM_SETTEXT`·`CB_*`
  메시지·창 스타일 읽기가 통하지 않는다. UIA 패턴만 쓸 수 있다.
- `Grid`·`Canvas`·`StackPanel`·`Border` 같은 배치 요소는 UIA 트리에 없다. 서식 칸의 UIA 부모는 서식 전체를 감싼
  `ScrollViewer`(Pane)다. 메뉴 막대는 `MenuBar`가 아니라 `Menu`다.
- 서식 컨트롤:

  | 스키마 | WPF 컨트롤 | 비고 |
  |---|---|---|
  | text·textarea | `TextBox` | ValuePattern. 포커스 없이 값이 바뀌면 바로, 아니면 포커스를 잃을 때 기록 |
  | date | `DatePicker` | 빈 값 = 날짜 없음. 안에 `DatePickerTextBox`(Edit)와 달력 단추가 있다. 친 글자는 포커스를 잃거나 Enter를 칠 때 날짜가 되고 그때 기록 |
  | select | `ComboBox`(편집 불가, 첫 항목 빈 값) | 선택이 바뀌는 즉시 기록 |
  | radio | `RadioButton`(GroupName = 키) | SelectionItem. 고르는 즉시 기록 |
  | check | `CheckBox` | Toggle. 바뀌는 즉시 기록 |
  | grid | `DataGrid`(첫 열은 읽기 전용 행 번호) | Grid·Table. 셀 값이 바뀌면(편집 확정·UIA 셀 SetValue) 기록 |

## UIA 프로브

`probe/Probe-Uia.ps1`은 창의 UIA 트리 덤프(`uia-tree.txt`, 전체 순회 시간 포함)와 두 가지 캡처
(`capture-screen.png`=CopyFromScreen, `capture-printwindow.png`=PrintWindow)를 `-OutDir`(기본
`.\probe-out`)에 남긴다. **실제 EMR을 프로브하면 이 파일들에 환자 화면 내용이 그대로 들어간다.**
`windows/probe-out/`·`windows/out/`은 git에서 무시되지만, 가능하면 저장소 밖 경로를 `-OutDir`로 준다.

```powershell
# EmrDemo를 새 데이터 디렉터리로 띄워 모드별 계약까지 검사
powershell -File probe\Probe-Uia.ps1 -ExePath out\app\EmrDemo.exe -Mode standard
powershell -File probe\Probe-Uia.ps1 -ExePath out\app\EmrDemo.exe -Mode custom

# 이미 떠 있는 아무 프로그램(실제 EMR 포함)의 트리 덤프·캡처만
powershell -File probe\Probe-Uia.ps1 -ProcessName <프로세스 이름>
```

검사 내용 — standard: AutomationId 도달, `chartText` ValuePattern 읽기, UIA로 쓴 차트 텍스트와
처방 셀이 저장 스냅샷·`field_commit`에 한 번씩 들어가는지, 환자 전환 시 저장 안 한 편집이 버려지고
불러오기가 `field_commit`을 남기지 않는지, `초기화`가 화면만 되돌리는지, 서식(`DEMO-10`)에서 UIA
ValuePattern·SelectionItem·Toggle·ExpandCollapse·GridPattern으로 넣은 값이 저장·기록되는지.
custom: 세 AutomationId가 없고 세 영역이 이름·패턴·자식 없는 Pane(AutomationId는 숫자뿐)인지, UIA 트리와 자식 창
`WM_GETTEXT` 어디에서도 차트 텍스트가 읽히지 않는지, 키보드로 입력한 차트 텍스트·처방 셀이
저장·기록되고 그 뒤에도 읽히지 않는지, 서식 캔버스가 opaque Pane이고 `layout-csec.json` 좌표로
클릭·입력한 텍스트·라디오·체크·드롭다운·표 셀 값이 저장·기록되며 UIA·`WM_GETTEXT`로 읽히지 않는지. EmrDemo 모드에서는 `<OutDir>\data-<시각>`을 새로 만들어 쓰고
아무것도 지우지 않는다. CI(`.github/workflows/windows-demo.yml`)가 Windows 러너에서 두 모드를
돌리고 결과를 `probe-results` 아티팩트로 올린다.
