# Windows EMR 시험 대상 앱 — 구조화 서식지 plan

spec: `docs/specs/2026-09-29-windows-ob-forms-design.md`

실행 방식: 부모 에이전트 인라인 순차. 스키마 모델 → 배치 → 두 모드 렌더러 → 프로브가 한 사슬로
묶이고(판정 ① 불충족), 배치 좌표가 두 렌더러의 공유 계약이다.

## 태스크

- [x] 1. 내보내기 스크립트와 `Seed/forms.json` (6종, 빈 값, 키 523개 고유)
- [x] 2. Core `FormDefinition` 모델·로더 (TDD)
- [x] 3. Core `FormLayout` 배치 (TDD, 가짜 측정 함수)
- [x] 4. Core `Patient.FormId`·`FormValues`, 시드 `DEMO-09`~`DEMO-14`, `EmrFields.Form` (TDD)
- [ ] 5. App standard 서식 뷰(네이티브 컨트롤, AutomationId, 확정 규칙)
- [ ] 6. App custom 서식 캔버스(그리기·입력·드롭다운·스크롤·opaque)
- [ ] 7. MainForm 서식 모드 전환(가운데 칸 확장), `layout-<formId>.json` 기록
- [ ] 8. 프로브 서식 검사, README
- [ ] 9. CI 확인, 종료 전 `origin/main`·PR #2 델타 재감사

## 판단 기록

## 남은 문제
