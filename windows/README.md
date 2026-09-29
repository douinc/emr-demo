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
불러오기가 `field_commit`을 남기지 않는지, `초기화`가 화면만 되돌리는지.
custom: 세 AutomationId가 없고 세 영역이 이름·패턴·자식 없는 Pane(AutomationId는 숫자뿐)인지, UIA 트리와 자식 창
`WM_GETTEXT` 어디에서도 차트 텍스트가 읽히지 않는지, 키보드로 입력한 차트 텍스트·처방 셀이
저장·기록되고 그 뒤에도 읽히지 않는지. EmrDemo 모드에서는 `<OutDir>\data-<시각>`을 새로 만들어 쓰고
아무것도 지우지 않는다. CI(`.github/workflows/windows-demo.yml`)가 Windows 러너에서 두 모드를
돌리고 결과를 `probe-results` 아티팩트로 올린다.
