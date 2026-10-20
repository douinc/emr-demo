/* ============================================================
   정형외과 · 마취 서식지 4종 (종이 서식형, paper-forms.js 로 그림)
     1. 초진기록지
     2. 마취전 환자평가
     3. 마취기록지 (ANESTHESIA RECORD)
     4. 회복실 기록지
   실제 병원 서식을 참고해 칸 구성과 라벨을 옮겼다(라벨의 철자도 원본대로 둠).
   병원 로고는 '예시병원' 자리표시 로고로 바꿨고, 입력값은 모두 지어낸 예시 데이터다
   (우측 무릎 반월상연골 파열 → 관절경 수술 한 건의 흐름).
   ============================================================ */
(function (global) {
  'use strict';
  var D = global.OBFormDSL, Q = global.PaperFormDSL;
  var t = D.t, ta = D.ta, r = D.r, k = D.k, d = D.d, lbl = D.lbl;
  var P = Q.P, L = Q.L, C = Q.C, f = Q.f, lw = Q.lw, pt = Q.pt, ln = Q.ln, vb = Q.vb, hb = Q.hb,
      mallampati = Q.mallampati, logo = Q.logo, sign = Q.sign, vlegend = Q.vlegend, vchart = Q.vchart;

  var PATIENT = { sex: 'M', age: '54' };
  var SURGEON = '예시 의사', ANESTHESIOLOGIST = '예시 마취의';

  /* 칸 하나짜리 입력 : 값 v 를 가진 채우기 입력 */
  function F(v, o) { return C([f({ val: v || '' })], o); }

  /* ========================================================
     1. 초진기록지
     ======================================================== */
  var firstVisit = {
    id: 'osfirst',
    paper: true,
    title: '초진기록지',
    dept: '정형외과',
    summary: '정형외과 초진 기록 (C.C · P.I · 통증 평가 · P/E)',
    patient: PATIENT,
    width: 720,
    blocks: [
      P.title('초진기록지'),
      P.table([84, 96, 52, 104, 76, 86, 60, null], [[
        L('등록번호', { cls: 'b' }), C([pt('id')]), L('이름', { cls: 'b' }), C([pt('name')]),
        L('성별/나이', { cls: 'b' }), C([pt('sexAge')]), L('진료과', { cls: 'b' }), C([pt('dept')])
      ]]),
      P.table([null, 190], [
        [L('Chief Complaint', { cls: 'bh' }), L('Onset', { cls: 'bh' })],
        [C([ta(46, { val: '우측 무릎 통증, 계단 내려갈 때 무릎이 걸리는 느낌' })], { cls: 'bb' }),
         C([ta(46, { val: '2026-08 말 (약 5주 전)' })], { cls: 'bb' })]
      ]),
      P.box('Present Illness', 80,
            '약 5주 전 등산 후 하산 중 우측 무릎을 비튼 뒤 통증 발생함.\n' +
            '이후 계단을 내려갈 때와 쪼그려 앉을 때 무릎 안쪽 통증과 걸림(locking) 증상이 반복됨.\n' +
            '타병원에서 물리치료 2주 받았으나 호전 없어 내원함.'),
      P.table([null], [[C([vb([
        ln([lw('통증', 46), k(['유', '무'], ['유']), lw('', 50), lbl('VAS'), t(56, { val: '6' }), lbl('점'),
            lw('', 20), lbl('or'), lw('', 20), lbl('FPSR'), t(56), lbl('점')]),
        ln([lw('빈도', 46), f({ val: '계단 보행 시마다, 하루 수 회' })]),
        ln([lw('양상', 46), f({ val: '찌르는 듯함, 무릎 안쪽' })])
      ])], { cls: 'bb' })]]),
      P.box('Past History', 64, 'HTN (amlodipine 5mg 복용 중)\n수술력 없음, 약물 알레르기 없음'),
      P.box('Physical Examination', 80,
            'Rt knee : effusion (+), medial joint line tenderness (+)\n' +
            'McMurray test (+) medial, Lachman test (-), anterior drawer test (-)\n' +
            'ROM 0-120°, 하지 신경학적 이상 소견 없음'),
      P.box('특이소견', 46, 'Knee X-ray : 관절 간격 유지됨, 골절 소견 없음'),
      P.box('Impression', 36, 'Medial meniscus tear, Rt knee (r/o)'),
      P.box('Plan', 52, 'Rt knee MRI 촬영\nMRI 결과 확인 후 관절경적 반월상연골 부분절제술 고려\nNSAIDs 1주 처방'),
      P.line([logo(), lw('', 40), lbl('작성일'), d('2026-09-28'), lw('', 10), lbl('주치의'),
              t(90, { val: SURGEON }), sign()])
    ]
  };

  /* ========================================================
     2. 마취전 환자평가
     ======================================================== */
  function lab(name, slots, checked, vals) {
    var items = [k([name], checked ? [name] : []), lbl('(')];
    for (var i = 0; i < slots; i++) {
      if (i) items.push(lbl('/'));
      items.push(t(slots === 1 ? 110 : 40, { val: (vals || [])[i] || '' }));
    }
    items.push(lbl(')'));
    return ln(items);
  }

  var preAnesthesia = {
    id: 'preanes',
    paper: true,
    title: '마취전 환자평가',
    dept: '마취통증의학과',
    summary: '마취 전 환자 상태 평가 (병력 · 검사 · 기도 평가)',
    patient: PATIENT,
    width: 780,
    blocks: [
      P.title('마취전 환자평가'),
      P.line([lbl('등록번호 :'), pt('id'), lbl('이름 :'), pt('name'), lbl('성별/나이 :'), pt('sexAge'),
              lbl('병실 :'), t(110, { val: '5병동 512호' }), lbl('집도의 :'), t(100, { val: SURGEON })]),
      P.table([170, 180, 110, 100, null], [
        [L('수술전 진단명'), F('Medial meniscus tear, Rt knee', { cs: 4 })],
        [L('예정 수술명'), F('Arthroscopic partial meniscectomy, Rt knee', { cs: 4 })],
        [L('Vital sign', { rs: 2 }),
         C([lbl('BP(mmHg) :'), t(50, { val: '132' }), lbl('/'), t(50, { val: '84' })], { cs: 2 }),
         C([lbl('PR(회/분) :'), f({ val: '76' })], { cs: 2 })],
        [C([lbl('RR(회/분) :'), f({ val: '18' })], { cs: 2 }),
         C([lbl('BT(℃) :'), f({ val: '36.6' })], { cs: 2 })],
        [L('키.몸무게'),
         C([lbl('HT :'), t(90, { val: '172' }), lbl('cm')], { cs: 2 }),
         C([lbl('WT :'), t(90, { val: '74' }), lbl('kg')], { cs: 2 })],
        [L('금식.동의서'),
         C([k(['NPO since'], ['NPO since']), t(110, { val: '10-07 24:00' })], { cs: 2 }),
         C([k(['Permision'], ['Permision'])], { cs: 2 })],
        [L('마취관련 과거력', { rs: 3 }), L('Difficult airway', { cls: 'l plain' }),
         C([k(['N', 'Y'], ['N'])], { cs: 3, cls: 'spread' })],
        [L('Complication', { cls: 'l plain' }), C([k(['N', 'Y'], ['N'])], { cs: 3, cls: 'spread' })],
        [L('FHx', { cls: 'l plain' }), C([k(['N', 'Y'], ['N'])], { cs: 3, cls: 'spread' })],
        [L('PHX', { rs: 2 }), L('OP history', { cls: 'l plain' }), F('없음', { cs: 3 })],
        [C([k(['Asthma', 'Hypertension', 'DM', '고지혈증', 'Hepatitis', 'TBC', 'Thyroid Dis', 'Allergy', 'URI'],
              ['Hypertension'])], { cs: 4, cls: 'k5' })],
        [L('Labs study', { rs: 3 }), L('Chest PA', { cls: 'l plain' }), F('No active lung lesion', { cs: 3 })],
        [L('EKG', { cls: 'l plain' }), F('Normal sinus rhythm', { cs: 3 })],
        [C([hb([
          vb([lab('Hb/Hct/Plt', 3, true, ['14.2', '42.1', '245']), lab('PT/APTT', 2, true, ['1.02', '31.5']),
              lab('Na/k/Cl', 3, true, ['140', '4.1', '103']), lab('U/A', 1, true, ['WNL'])]),
          vb([lab('ABO Typing', 2, true, ['A', 'Rh+']), lab('AST/ALT', 2, true, ['24', '28']),
              lab('Bun/Cr', 2, true, ['15', '0.9'])])
        ])], { cs: 4 })],
        [L('치아상태'), C([k(['Normal', 'Abnormal (의치/흔들림)'], ['Normal'])], { cs: 4, cls: 'spread' })],
        [L('Mallampai grade'), C([hb([mallampati(1), mallampati(2, true), mallampati(3), mallampati(4)],
                                     { cls: 'spread' })], { cs: 4 })],
        [L('Premedication'), F('없음', { cs: 4 })],
        // 원본 서식 그대로 : ASA class 칸의 선택지가 치아상태와 같다
        [L('ASA class'), C([k(['Normal', 'Abnormal (의치/흔들림)'], ['Normal'])], { cs: 4, cls: 'spread' })],
        [L('Anesthetic plan'),
         C([k(['General', 'Spinal', 'Epidural', 'BPB', 'Mask', 'Other'], ['General'])], { cs: 4, cls: 'spread' })],
        [C([t(50, { val: '2026' }), lbl('년'), t(30, { val: '10' }), lbl('월'), t(30, { val: '07' }), lbl('일'),
            lbl('('), t(30, { val: '16' }), lbl(':'), t(30, { val: '30' }), lbl(')')], { cs: 3, cls: 'c' }),
         C([lbl('마취의 :'), f({ val: ANESTHESIOLOGIST })], { cs: 2 })]
      ]),
      P.line([lw('', 300), logo()])
    ]
  };

  /* ========================================================
     3. 마취기록지 (ANESTHESIA RECORD)
     ======================================================== */
  var MAJOR = 12;               // 15분 칸 12개 = 3시간
  var CW = 13;                  // 5분 칸 폭(px)
  var TIMES = ['09:00', '09:15', '09:30', '09:45', '10:00', '10:15'];

  /* 투약·감시 줄 : 라벨 칸 + 15분 칸 12개 (값은 15분 칸 순서) */
  function drugRow(labelItems, vals) {
    var row = [C(labelItems, { cls: 'dl' })];
    for (var i = 0; i < MAJOR; i++) {
      row.push(C([t(null, { val: (vals || [])[i] || '' })], { cs: 3, cls: 'tg' }));
    }
    return row;
  }
  function named(name, unit) { return [lw(name, 108), lbl(unit || '')]; }
  function blank(val) { return [f({ val: val || '' })]; }

  var drugRows = [
    drugRow([lbl('O2('), t(44), lbl('ℓ/min)')], ['6', '2', '', '', '', '6']),
    drugRow([lbl('Air('), t(44), lbl('ℓ/min)')], ['', '2']),
    drugRow([lbl('Sevoflurance('), t(30), lbl('%)')], ['', '2.0', '2.0', '1.8', '1.5']),
    drugRow(blank()),
    drugRow(named('H/S', '(ml)'), ['200', '', '', '', '300']),
    drugRow(named('N/S', '(ml)')),
    drugRow(named('Volulyte 500', '(ml)')),
    drugRow(named('Blood')),
    drugRow(blank()),
    drugRow(named('덱스메딘 50', 'ml')),
    drugRow(named('Fentanyl', '(mg)'), ['0.05']),
    drugRow(named('2%리도카인', '(mg)'), ['40']),
    drugRow(named('포폴', '(mg)'), ['120']),
    drugRow(named('로큐니움', '(mg)'), ['40']),
    drugRow(blank('브리디온 (mg)'), ['', '', '', '', '', '200']),
    drugRow(blank()),
    drugRow(blank()),
    drugRow(named('SpO2'), ['100', '99', '99', '99', '99', '100']),
    drugRow(named('ETCO2'), ['', '35', '34', '35', '36']),
    drugRow(named('EKG'), ['NSR', 'NSR', 'NSR', 'NSR', 'NSR', 'NSR']),
    drugRow(named('Temperature'), ['36.5', '', '36.3', '', '36.2'])
  ];

  // 5분 칸 기준 활력징후 (09:00 ~ 10:15)
  var CHART = {
    cols: MAJOR * 3, cw: CW, h: 300, min: 10, max: 210,
    data: {
      sbp:   [134, 128, 112, 108, 118, 122, 120, 118, 121, 119, 117, 120, 122, 125, 130, 132],
      dbp:   [84, 80, 68, 64, 72, 76, 74, 72, 75, 73, 71, 74, 76, 78, 82, 84],
      pulse: [78, 82, 70, 66, 72, 74, 72, 70, 71, 70, 69, 71, 73, 76, 80, 82],
      resp:  ['a', 'c', 'c', 'c', 'c', 'c', 'c', 'c', 'c', 'c', 'c', 'c', 'c', 'c', 's', 's'],
      marks: [{ i: 0, s: 'x' }, { i: 15, s: 'x' }, { i: 4, s: 'o' }, { i: 13, s: 'o' }]
    }
  };

  var timeRow = [L('TIME', { cls: 'l b' })];
  for (var ti = 0; ti < MAJOR; ti++) timeRow.push(C([t(null, { val: TIMES[ti] || '' })], { cs: 3, cls: 'tg' }));
  timeRow.push(L('REMARK', { cls: 'l b plain' }));
  // REMARK 칸은 투약 줄부터 마취 방법 칸까지 이어진다
  drugRows[0].push(C([ta(40, { val:
    '09:00 Induction\n09:03 Intubation (ETT 7.5, oral, 22cm)\n09:15 Tourniquet on (Rt thigh, 300mmHg)\n' +
    '09:20 Op start\n10:00 Tourniquet off\n10:05 Op end\n10:12 Bridion 200mg IV\n10:15 Extubation\n' +
    '10:18 회복실 이동' })], { rs: drugRows.length + 4, cls: 'fill top' }));

  function u(text) { return lbl(text); }
  var method = hb([
    vb([
      ln([k(['Circuit(비급여/ 급여)']), k(['Circle with MASK'])]),
      ln([k(['LMA #']), t(30), k(['I-Gel #']), t(30)]),
      ln([k(['Endotracheal'], ['Endotracheal'])]),
      ln([u('Size'), t(30, { val: '7.5' }), u('Fr./ cuff'), r(['+', '-'], '+'), u('/ depth:'), t(30, { val: '22' }), u('cm')]),
      ln([r(['Oral', 'Naso', 'Trac'], 'Oral')]),
      ln([k(['Video Blade (1회용.C)']), k(['wire/plain'])]),
      ln([k(['pillow'], ['pillow']), k(['Tube holder'], ['Tube holder']), k(['ST Probe'])])
    ], { w: 275 }),
    vb([
      ln([k(['Regional'])]),
      ln([r(['SPI', 'EPI', 'BPB', 'CSE', 'Caudal'])]),
      ln([u('P.Po :'), t(50), u('Needle:'), t(36), u('G')]),
      ln([u('Injection level :'), f()]),
      ln([u('Anesth level :'), f()]),
      ln([u('CSF :'), t(50), u('Blood :'), t(50)]),
      ln([k(['Mask']), k(['N-P']), k(['ST Probe'])])
    ], { w: 205 }),
    vb([
      ln([k(['IV PCA'], ['IV PCA'])]),
      ln([u('Fentanyl'), t(50, { val: '500' }), u('㎍')]),
      ln([u('+네큐팜'), t(50, { val: '1' }), u('@')]),
      ln([u('+람세트'), t(50, { val: '1' }), u('@')]),
      ln([u('+N/S=100㎖')]),
      ln([k(['IV PCA'])]),
      ln([u('Fentanyl'), t(50), u('㎍')]),
      ln([u('+람세트'), t(50), u('@')]),
      ln([u('+N/S=100㎖')]),
      ln([k(['PCA refuse'])])
    ], { w: 150 })
  ], { cls: 'pf-method' });

  function outputRow(label, vals, total) {
    var row = [L(label, { cls: 'l' })];
    for (var i = 0; i < 6; i++) row.push(C([t(null, { grow: true, val: (vals || [])[i] || '' })], { cs: 6 }));
    row.push(C([lbl('TOTAL'), f({ val: total || '' }), lbl('㎖')]));
    return row;
  }

  var anesRecord = {
    id: 'anesrec',
    paper: true,
    title: '마취기록지',
    dept: '마취통증의학과',
    summary: 'ANESTHESIA RECORD (투약 · 활력징후 그래프 · 마취 방법)',
    patient: PATIENT,
    width: 840,
    blocks: [
      P.table([150, null, 80, 80, 80], [
        [L('ROOM'), L('ANESTHESIA RECORD', { rs: 2, cls: 'nb plain pf-big' }), L('YEAR'), L('MONTH'), L('DAY')],
        [F('2'), C([t(null, { grow: true, val: '2026' })], { cls: 'c' }),
         C([t(null, { grow: true, val: '10' })]), C([t(null, { grow: true, val: '08' })])]
      ], { cls: 'pf-head' }),
      P.table([100, 60, 60, 130, null], [
        [C([vb([lw('NAME'), pt('name')])], { rs: 2 }), C([vb([lw('SEX'), pt('sex')])], { rs: 2 }),
         C([vb([lw('AGE'), pt('age')])], { rs: 2 }),
         C([vb([lw('HT/BW'), ln([t(50, { val: '172' }), lbl('/'), t(50, { val: '74' })])])], { rs: 2 }),
         C([lw('HOSPITAL NO.', 110), pt('id')])],
        [C([lw('ASA PHYSICAL STATUS', 140), r(['1', '2', '3', '4', '5', '6'], '2'), k(['E'])])],
        [L('마취유전 구두로 환자상태 평가 확인 하였음', { cs: 4, cls: 'l plain' }), C([f()])],
        [L('PREOP.DIAGNOSIS  /  POSTOP.DIAGNOSIS', { cs: 4, cls: 'l plain' }),
         L('PROPOSED OPERATION / OPERATION PERFORMED', { cls: 'l plain' })],
        [F('Medial meniscus tear, Rt knee', { cs: 4 }), F('Arthroscopic partial meniscectomy, Rt knee')]
      ]),
      P.table([190].concat(Array(MAJOR * 3).fill(CW), [null]), [timeRow].concat(drugRows, [
        [C([vlegend(CHART)], { cls: 'ch' }), C([vchart(CHART)], { cs: MAJOR * 3, cls: 'ch' })],
        [L('Drug No.', { cls: 'l' }), F('', { cs: MAJOR * 3 })],
        [L('Pt. Position', { cls: 'l' }), F('Supine', { cs: MAJOR * 3 })],
        [C([vb([ln([lw('ANESTHESIA METHOD')], { cls: 'c' }), method])], { cs: MAJOR * 3 + 1 })],
        outputRow('URINE OUTPUT'),
        outputRow('BLOODLOSS', ['10'], '10'),
        [C([vb([lw('ANESTHESIOLOGISTS'), f({ val: ANESTHESIOLOGIST })])], { cs: 22, cls: 'top' }),
         C([vb([lw('SURGEONS'), f({ val: SURGEON })])], { cs: 15, cls: 'top' }),
         C([vb([ln([lw('Scru.Nr', 50), f({ val: '예시 간호사 A' })]),
                ln([lw('ANE.Nr', 50), f({ val: '예시 간호사 B' })])])])]
      ]), { cls: 'pf-anes' }),
      P.line([lw('', 320), logo()])
    ]
  };

  /* ========================================================
     4. 회복실 기록지
     ======================================================== */
  function inputGrid(cols, widths, data, rows) {
    var out = [cols.map(function (h) { return L(h); })];
    for (var i = 0; i < rows; i++) {
      out.push(cols.map(function (_, j) { return F((data[i] || [])[j]); }));
    }
    return P.table(widths, out);
  }

  var recovery = {
    id: 'pacu',
    paper: true,
    title: '회복실 기록지',
    dept: '마취통증의학과',
    summary: '회복실 간호 기록 (활력징후 · I/O · PAR score)',
    patient: PATIENT,
    width: 780,
    blocks: [
      P.title('회복실 기록지'),
      P.line([lbl('등록번호 :'), pt('id'), lbl('이름 :'), pt('name'), lbl('성별/나이 :'), pt('sexAge'),
              lbl('진료과 :'), pt('dept')]),
      P.table([96, 150, 80, 150, 80, null], [
        [L('마취종류'), C([k(['General', 'Spninal', 'BPB', 'Epidural', 'Mask', 'Other'], ['General'])], { cs: 5 })],
        [L('Pre op. Mental Status:', { cs: 2, cls: 'l' }),
         C([k(['Alert', 'Drowsy', 'Confused', 'Non-responsive', 'Other'], ['Alert'])], { cs: 4 })],
        [L('Monitor'), C([k(['NIBP', 'EKG', 'SpO2', 'Others'], ['NIBP', 'EKG', 'SpO2'])], { cs: 5 })],
        [L('입실시간'), F('10:18'), L('퇴실시간'), F('11:10'), L('퇴실장소'), C([k(['수술실', '회복실'], ['수술실'])])],
        [L('회복실 간호사'), F('예시 간호사 C'), L('담당 마취의'), F(ANESTHESIOLOGIST),
         L('낙상예방\n간호'), C([k(['Knee band', 'Arm band'], ['Knee band', 'Arm band'])])]
      ]),
      P.line([lbl('[Vital Sign]')]),
      inputGrid(['Time', 'BP', 'PR', 'RR', 'SpO2', 'Temp', '오심', '구토', 'NRS'],
                [null, 90, 70, 70, 70, 70, 60, 60, 60],
                [['10:18', '138/86', '84', '16', '98', '36.2', '-', '-', '3'],
                 ['10:35', '130/80', '78', '14', '99', '36.4', '-', '-', '3'],
                 ['10:50', '126/78', '74', '14', '99', '36.5', '-', '-', '2'],
                 ['11:05', '124/78', '72', '14', '99', '36.6', '-', '-', '2']], 10),
      P.gap(6),
      P.table([null, 230], [
        [L('간호기록 및 Remark 인계사항'), L('피부상태 확인')],
        [C([ta(40, { val:
          '10:18 회복실 입실, drowsy 하나 구두 지시에 반응함\nO2 5L/min via facial mask 적용\n' +
          'IV PCA 작동 확인\n10:50 의식 alert, NRS 2\n11:10 PAR score 10점 확인 후 병동 이실, 인계함' })],
           { rs: 3, cls: 'fill' }),
         C([k(['유', '무'], ['무'])], { cls: 'spread', h: 34 })],
        [L('치아상태 확인')],
        [C([k(['유', '무'], ['무'])], { cls: 'spread', h: 34 })]
      ]),
      P.table([96, 130, 140, 70, 130, null], [
        [L('Foley cath.'), C([k(['무', '유'], ['무'])], { cls: 'spread' }), F(),
         L('Drain'), C([k(['무', '유'], ['무'])], { cls: 'spread' }), F()],
        [L('IV PCA'), C([k(['무', '유'], ['유'])], { cls: 'spread' }), F('Fentanyl based'),
         L('기타'), F('', { cs: 2 })]
      ]),
      P.line([lbl('[Intake/Output]')]),
      P.table([110, null, null, null, null], [
        [L('Type', { rs: 2 }), L('Intake', { cs: 2 }), L('Output', { cs: 2 })],
        [L('Fluid'), L('Blood'), L('Uluid'), L('Blood')],
        [L('수술실'), F('500'), F(), F(), F('10')],
        [L('회복실'), F('100'), F(), F(), F()],
        [L('Total'), F('600'), F(), F(), F('10')]
      ]),
      P.line([lbl('[PAR Score]')]),
      P.table([110, null, null, null, null, null, null], [
        [L(''), L('활동성'), L('호흡'), L('순환'), L('의식'), L('피부색'), L('합계')],
        [L('입실'), F('2'), F('2'), F('2'), F('1'), F('2'), F('9')],
        [L('퇴실'), F('2'), F('2'), F('2'), F('2'), F('2'), F('10')]
      ]),
      P.line([lw('', 480), lbl('작성 간호사'), t(100, { val: '예시 간호사 C' }), sign()])
    ]
  };

  global.SURGERY_FORMS = [firstVisit, preAnesthesia, anesRecord, recovery];
})(window);
