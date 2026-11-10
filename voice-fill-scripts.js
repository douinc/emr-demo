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
      'Impression 은 IUP 24주, normal fetal neurosonographic findings 입니다.'
  };
})(window);
