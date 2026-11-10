/* ============================================================
   음성 서식 입력 — 서식별 말하기 예시
   시연자가 그대로 읽으면 해당 칸이 채워지도록 쓴 짧은 구술 대본이다.
   모두 가상의 내용이며 실제 환자 정보가 아니다.
   ============================================================ */
(function (global) {
  'use strict';
  global.VOICE_FILL_SCRIPTS = {
    labor2:
      '임신력은 G 2, P 1, L 1, D 0, A 0 이고, 임신 39주 2일입니다. ' +
      '오전 8시 20분 내진, presentation 은 vertex, 자궁경부 개대 2센티, 소실 50퍼센트, station 마이너스 3, ' +
      '양막은 intact, 태아 심박수 142, 자궁수축은 5분 간격 30초 mild 입니다. 입원해서 관찰 시작합니다. ' +
      '오전 10시 내진, vertex, 개대 3센티, 소실 60퍼센트, station 마이너스 3, 양막 intact, ' +
      '태아 심박수 145, 수축은 4분 간격 35초 moderate, 옥시토신 시작했습니다. ' +
      '기타 기록으로 8시 25분 NST reactive, baseline 140, acceleration 있고 deceleration 없습니다.',
    csec:
      '수술일은 오늘입니다. 수술 전 진단은 previous cesarean section, 수술 후 진단도 같습니다. ' +
      '수술명은 low transverse cesarean section. Level 은 elective, 적응증은 previous cesarean section 입니다. ' +
      '마취는 spinal 로 했고 PCA 는 IV 입니다. 피부 절개는 Pfannenstiel, 자궁 절개는 low flap transverse, ' +
      '양수는 clear 했습니다. 자궁 수축은 firm 했고 양쪽 난소는 normal 입니다. ' +
      '추정 출혈량은 700 cc, 태반 무게 600그램, 태반 육안 소견은 normal 입니다. ' +
      '수술 중 pitocin 을 썼고, drain 은 넣지 않았으며 sponge count 는 맞았습니다.',
    sono1st:
      '검사일은 오늘이고, LMP 기준 분만예정일은 2027년 5월 29일입니다. 영상은 good quality 입니다. ' +
      'G-sac 보이고 5.4센티, 11주 2일 크기입니다. CRL 은 4.6센티, 11주 3일. ' +
      'Yolk sac 보이고 0.4센티, 태아 심박동 있고 162 bpm 입니다. ' +
      'NT 는 측정했고 0.12센티, nasal bone 은 present 입니다. ' +
      '태반은 posterior 이고 previa 는 없습니다. 자궁과 자궁경부는 특이 소견 없고, 양쪽 난소는 not checked 입니다. ' +
      'Impression 은 IUP 11주, single intrauterine pregnancy 11주 3일이고 NT 는 정상 범위입니다.',
    sono23:
      '검사일은 오늘이고, LMP 기준 분만예정일은 2027년 1월 17일입니다. 영상은 good quality 입니다. ' +
      'BPD 7.9센티 31주 2일, HC 28.4센티 30주 5일, AC 26.1센티 30주 0일, FL 5.8센티 30주 3일입니다. ' +
      '예상 체중은 1520에서 1680그램, 심박동 있고 148 bpm, 태위는 vertex 입니다. ' +
      '뇌실, 소뇌, cisterna magna 정상이고, 폐와 횡격막 정상, four chamber view 와 three vessel view 정상, ' +
      '위와 탯줄 정상, 신장과 방광 정상, 척추 정상입니다. ' +
      '양수는 deep pocket 4.6센티로 adequate 합니다. 태반은 posterior 이고 previa 는 없습니다. ' +
      'Impression 은 IUP 30주, 주수에 맞는 발육이고 gross anomaly 는 보이지 않습니다.',
    fetalecho:
      '검사일은 오늘이고, 적응증은 임신 24주 routine screening 입니다. ' +
      'Heart axis 는 levocardia 로 정상, heart position 은 normal, heart size 도 normal 입니다. ' +
      '심장 둘레 8.4센티, 흉곽 둘레 17.2센티입니다. ' +
      'Four chamber view, three vessels view, long axis view, short axis view, aortic arch view, ductal arch view 모두 normal 입니다. ' +
      'Rhythm 은 regular, baseline heart rate 는 normal, pericardial effusion 은 absent 입니다. ' +
      '도플러에서 aorta 78, pulmonary artery 72 cm/sec 이고, TV 는 E 32, A 52, MV 는 E 30, A 50 입니다. ' +
      'Impression 은 IUP 24주, structurally normal fetal heart 이고 부정맥 소견은 없습니다.',
    neurosono:
      '검사일은 오늘이고, 영상은 good quality, 적응증은 임신 24주 정밀 신경초음파입니다. 태위는 vertex 입니다. ' +
      '두개골은 size, shape, integrity 모두 정상입니다. ' +
      'Lateral ventricle 은 양쪽 정상이고, atrial diameter 는 오른쪽 6.2, 왼쪽 5.8밀리미터입니다. ' +
      'Falx 와 CSP 정상, corpus callosum 은 모양 정상에 길이 3.6센티입니다. ' +
      '소뇌, cisterna magna, vermis 정상입니다. ' +
      '척추는 canal integrity 와 skin integrity 정상이고, conus medullaris 는 L2 레벨입니다. ' +
      'Impression 은 IUP 24주, normal fetal neurosonographic findings 입니다.',
    /* ---- 정형외과·마취 종이 서식 ---- */
    osfirst:
      '주호소는 우측 무릎 통증이고, 계단 내려갈 때 무릎이 걸리는 느낌이 있습니다. Onset 은 약 5주 전입니다. ' +
      '현병력입니다. 5주 전 등산 후 하산하다가 우측 무릎을 비튼 뒤 통증이 생겼고, 계단을 내려가거나 쪼그려 앉을 때 ' +
      '무릎 안쪽 통증과 locking 증상이 반복됩니다. 타 병원에서 물리치료를 2주 받았지만 호전이 없어 내원했습니다. ' +
      '통증은 있고 VAS 6점, 빈도는 계단 보행 시마다 하루 수 회, 양상은 무릎 안쪽이 찌르는 듯합니다. ' +
      '과거력은 고혈압으로 amlodipine 5mg 복용 중이고, 수술력과 약물 알레르기는 없습니다. ' +
      '진찰 소견입니다. 우측 무릎 effusion 있고 medial joint line tenderness 양성, McMurray test 양성, Lachman test 음성, ' +
      'ROM 은 0도에서 120도입니다. 특이소견으로 knee X-ray 에서 골절 소견 없습니다. ' +
      'Impression 은 우측 무릎 medial meniscus tear 의심이고, Plan 은 우측 무릎 MRI 촬영하고 NSAIDs 1주 처방합니다.',
    preanes:
      '병실은 5병동 512호입니다. 수술 전 진단명은 medial meniscus tear, right knee 이고, ' +
      '예정 수술명은 arthroscopic partial meniscectomy 입니다. ' +
      '혈압 132 에 84, 맥박 76, 호흡수 18, 체온 36.6도입니다. 키 172센티, 몸무게 74킬로입니다. ' +
      '어젯밤 자정부터 금식했고 동의서 받았습니다. ' +
      '마취 관련 과거력으로 difficult airway 없고, complication 없고, 가족력도 없습니다. ' +
      '수술력은 없고 고혈압이 있습니다. Chest PA 는 no active lung lesion, EKG 는 normal sinus rhythm 입니다. ' +
      '혈색소 14.2, 헤마토크릿 42.1, 혈소판 245 이고, 혈액형은 A형 Rh 양성입니다. ' +
      '치아 상태는 normal, Mallampati 는 class 2 입니다. Premedication 은 없고, 마취 계획은 general 입니다.',
    pacu:
      '마취 종류는 general 이고, 수술 전 의식 상태는 alert 였습니다. 모니터는 NIBP, EKG, SpO2 적용했습니다. ' +
      '입실 시간 10시 18분, 퇴실 시간 11시 10분입니다. 낙상 예방으로 knee band 와 arm band 적용했습니다. ' +
      '활력징후입니다. 10시 18분 혈압 138 에 86, 맥박 84, 호흡 16, 산소포화도 98, 체온 36.2, 오심 구토 없고 NRS 3점. ' +
      '10시 35분 혈압 130 에 80, 맥박 78, 호흡 14, 산소포화도 99, 체온 36.4, 오심 구토 없고 NRS 3점입니다. ' +
      '간호기록입니다. 10시 18분 회복실 입실, drowsy 하나 구두 지시에 반응함. 산소 5리터 facial mask 로 적용함. ' +
      '피부 상태 이상 없고 치아 상태도 이상 없습니다. Foley 는 없고 drain 도 없습니다. IV PCA 는 있고 fentanyl based 입니다. ' +
      '수술실 수액 500, 수술실 출혈 10, 회복실 수액 100 입니다. ' +
      'PAR score 는 입실 시 활동성 2, 호흡 2, 순환 2, 의식 1, 피부색 2, 합계 9점이고, ' +
      '퇴실 시 활동성 2, 호흡 2, 순환 2, 의식 2, 피부색 2, 합계 10점입니다.'
  };
})(window);
