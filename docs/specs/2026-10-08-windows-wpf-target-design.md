# Windows EMR 시험 대상 앱 WPF판 — 설계

- 작성일: 2026-10-08
- 근거: 대상 EMR에 WPF로 만든 제품(이지케어텍 BESTCare 2.0)이 들어왔다. 지금까지 에이전트(emr-form-pilot)는
  WinForms 화면(EmrDemo, 모의 EMR)에서만 시험했다.

## 목적

같은 처방관리 화면과 산과 서식을 표준 WPF 컨트롤로 만든 판(`EmrDemoWpf`)을 둔다. WPF EMR에서 에이전트가 칸을
찾고 읽고 쓰는지, 서식 전체 읽기가 얼마나 걸리는지를 실제 병원 방문 전에 확인하려는 용도다.
WinForms판을 흉내 내지 않는다. WPF가 기본으로 만드는 UIA 구조를 그대로 둔다(자체 AutomationPeer·창 핸들 꼼수 없음).

## 범위

| 항목 | 내용 |
|---|---|
| 프로젝트 | `windows/src/EmrDemo.Wpf`(net10.0-windows, WPF), 실행 파일 `EmrDemoWpf.exe`. `EmrDemo.Core`를 그대로 쓴다 |
| 실행 인자 | WinForms판과 같은 `AppOptions`. 기본 데이터 디렉터리는 `%LOCALAPPDATA%\EmrDemoWpf`다. `--ui=custom`은 지원하지 않는다(종료 코드 2) |
| 화면 | 창 제목·메뉴·환자 머리글·탭 줄·네 칸 배치·상태 표시줄을 WinForms판과 같게 둔다. 서식 환자를 고르면 가운데 칸이 숨고 폭이 70/30이 된다 |
| AutomationId | WinForms판과 같다. 서식 칸은 서식 키, 라디오·체크 선택지는 `<키>#<선택지 번호>`, 서식 표는 표 키다 |
| 서식 컨트롤 | text·textarea=`TextBox`, date=`DatePicker`, select=편집 불가 `ComboBox`(첫 항목 빈 값), radio=`RadioButton`, check=`CheckBox`, grid=`DataGrid`(첫 열은 읽기 전용 행 번호) |
| 배치 | `FormLayout`(폭 860px)을 WPF 글자 폭으로 계산해 `ScrollViewer` 안 `Canvas`에 놓는다. 선·띠 장식은 요소 하나가 직접 그린다 |
| 기록 규칙 | WinForms판과 같다. 텍스트는 포커스를 잃을 때(포커스 없이 바뀌면 바로), 선택형은 바뀌는 즉시, 표 셀은 값이 바뀔 때 기록한다. 불러오는 중이거나 마지막 기록 값과 같으면 기록하지 않는다 |
| 날짜 형식 | ko-KR 문화권을 복제해 짧은 날짜를 `yyyy-MM-dd`로 바꿔 쓴다. 한국어 Windows의 .NET Framework(NLS) EMR과 같은 모양이다. 시험 VM이 en-US여도 같다 |
| 저장 파일 | `store.json`·`events.jsonl`·`layout-<서식id>.json` 형식은 WinForms판과 같다(`uiMode`는 `standard`) |
| 관리자 빌드 | `-p:RequireAdmin=true`로 WinForms판과 같은 manifest를 넣는다 |

## 범위 밖

- custom 모드(직접 그린 컨트롤). 비전 경로는 화면 기술과 상관없이 좌표로 다루므로 WinForms판으로 시험한다.
- 상용 컨트롤 묶음(DevExpress·Telerik·Infragistics 등). 실제 WPF EMR이 이런 컨트롤을 쓰면 UIA 노출이 이 앱과
  다를 수 있다. 설치 방문 때 실제 화면으로 확인한다.
- UIA 프로브(`probe/Probe-Uia.ps1`)의 WPF 검사. 프로브는 WinForms판 계약만 본다.
