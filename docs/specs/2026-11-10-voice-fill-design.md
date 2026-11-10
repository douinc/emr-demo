# 산과 서식 음성 입력 (voice fill) — 2026-11-10

웹 데모의 산과(OB) 구조화 서식 6종에 녹음 버튼을 달아, 말한 내용으로 서식 칸을 채운다.
EMR 화면 안에서 Dou Open API(실시간 전사 + 서식 결과 생성)를 바로 쓰는 모습을 보여 주는 시연용이다.

## 구성

```
화면(GitHub Pages, 정적) ──WS: 오디오·서식 정의·출력 구조──▶ 중계 서버 ──▶ Dou Open API(Test 환경)
        ◀──────────── 전사 · 서식 결과 전체(JSON) ────────────
```

- 화면은 API 키를 갖지 않는다. 키는 중계 서버에만 있고, 화면은 시연용 접속 코드로 중계 서버에 붙는다.
- 화면과 중계 서버 사이 프레임은 Dou Open API 의 "실시간 서식 결과 세션"(`WS /v1/documents/stream`)과 같은 모양이다
  (`config` → `ready` → 오디오 → `transcript`·`document` 반복 → `finish` → 최종 `document` → `session_end`).

| 파일 | 내용 |
|---|---|
| `voice-fill.js` | 서식 스키마 → 칸 목록·`output_schema`·서식 설명, 녹음 막대, 세션, 결과를 칸에 넣기 |
| `voice-fill-worklet.js` | 마이크 → 16 kHz mono 16-bit PCM, 100ms 조각 (AudioWorklet) |
| `voice-fill-scripts.js` | 서식별 말하기 예시(가상의 내용) |
| `voice-fill.css` | 녹음 막대·전사·채운 칸 표시 |
| `ob-forms.js` | 렌더러가 스키마 조각 → DOM 대응(`OBForms.bound`)을 남긴다. 서식 스키마 출력은 그대로다 |

## 서식 → 요청

`buildModel(form)` 이 `form.rows` 를 훑어 입력 칸마다 항목을 만든다.

- 키는 짧은 순번(`f1`, `f2` …)이고 뜻은 `description`(구역 > 라벨 > 칸, 단위)에 싣는다. 뜻이 드러나는 긴 키를 쓰면
  칸이 100개를 넘는 서식(2nd & 3rd trimester 193칸, Neurosonography 103칸)에서 모델의 구조화 출력 한도를 넘겨
  생성이 실패했다(2026-11-10 Test 환경 실측). 짧은 키로는 6종 모두 생성된다.
- 타입: 글·서술·날짜 = `string|null`, 라디오·드롭다운 = `enum|null`, 체크 묶음 = 선택지 배열, 선택지 하나짜리 체크 = `boolean|null`,
  표 = 열 키 객체의 배열(`maxItems` = 행 수). 선택지에 딸린 입력칸(Other 내용·부가 내용·선택지 옆 값)도 각각 칸이다.
- 뺀 것: 버튼, 그림 영역, 서명(기록자명·판독의·전문의), 읽기 전용 계산 칸. 같은 행의 `a / b = (계산)` 칸은 화면이 계산한다.
- 서식 설명(`form.source`)은 작성 규칙(말하지 않은 칸은 비움·숫자만·선택지 그대로 등)과 화면 순서대로의 항목 줄이다.
  내용이 같으면 중계 서버가 같은 서식(template)을 다시 쓴다.

## 결과 → 화면

- `document.output.value` 에서 `changed` 키만 갱신한다. 녹음 중 손으로 고친 칸은 덮어쓰지 않는다.
- `null` 은 건드리지 않는다. 다만 음성이 채웠던 칸이 다음 결과에서 `null` 이 되면 비운다.
- 채운 칸은 아래 초록 줄로 표시하고, 채워지는 순간 잠깐 노랗게 강조한다.
- 녹음을 시작하면 예시 값을 비우고(작성일은 오늘) 시작한다. "예시 값으로 되돌리기"로 처음 상태로 돌아간다.

## 설정

- 중계 서버 주소: 기본 `https://playground.dou.so/emr-demo-api`. 주소창 `?voiceApi=`(localhost 주소만 받는다 — 링크로 접속 코드·소리가 남의 서버로 가지 않게), 또는 설정 창(localStorage `emrDemo.voice.api`)으로 바꾼다.
- 접속 코드: 처음 녹음할 때 한 번 묻고 localStorage `emrDemo.voice.accessCode` 에 둔다. 설정 창에서 바꾼다.
- 마이크는 https 또는 localhost 에서만 된다.

## 로컬에서 보기

```bash
python3 -m http.server 8000 --bind 127.0.0.1          # 저장소 루트에서
# http://localhost:8000/index.html?voiceApi=http://localhost:8787  (중계 서버를 로컬에 띄웠을 때)
```

## 종이 서식형(정형외과·마취) — 2026-11-10 추가

초진기록지(`osfirst`)·마취전 환자평가(`preanes`)·회복실 기록지(`pacu`)에도 녹음 막대를 붙인다(`VoiceFill.supports`).
마취기록지(`anesrec`)는 그래프·시간 눈금 서식이라 붙이지 않는다.

- 종이 서식은 칸을 병합한 표라서, 표를 격자로 펴 입력 칸마다 라벨을 찾는다: 같은 행 왼쪽의 라벨 칸(이어진 라벨은 경로로),
  없거나 머리줄이 따로 있으면 같은 열 위쪽의 머리줄(라벨만 있는 행). 칸 안의 글자 라벨·단위(`cm`·`kg`·`점`)도 이름에 쓴다.
- 머리줄 + 한 칸에 입력 하나씩인 행이 이어지는 표(회복실 Vital Sign)는 행 목록(배열)으로 받는다.
- `유/무`·`N/Y` 같은 둘 중 하나 체크는 `enum|null`, Mallampati 그림 4개는 `Class 1~4` 중 하나, `검사명 ( 값 / 값 )` 은 체크 + 값 칸.
- 서명 줄(작성일·주치의·작성 간호사)은 채우지 않고, 작성일만 녹음 시작 때 오늘로 둔다. 환자 정보(등록번호·이름)는 읽기 전용이다.
- 서식 설명의 작성 규칙은 종이 서식용을 따로 쓴다. 산과 서식의 설명·출력 구조는 그대로다(서식 캐시 유지).
- "예시 값으로 되돌리기"는 `attach(host, form, redraw)` 의 `redraw` 로 다시 그린다.

