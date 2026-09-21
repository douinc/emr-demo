/* ============================================================
   산과(OB) 서식지 스키마 6종
     1. Labor record Ⅱ-OB
     2. 수술기록-OB C/sec
     3. 산전초음파 1st trimester (single)-OB
     4. 산전초음파 2nd & 3rd trimester (single)-OB
     5. Fetal echocardiography (single)-OB
     6. Fetal Neurosonography (single)-OB
   입력값은 모두 개인정보가 제거된 예시 데이터입니다.
   ============================================================ */
(function (global) {
  'use strict';
  var D = global.OBFormDSL;
  var S = D.S, t = D.t, d = D.d, ta = D.ta, r = D.r, k = D.k, sel = D.sel,
      btn = D.btn, lbl = D.lbl, br = D.br, nna = D.nna, staff = D.staff,
      signBlock = D.signBlock, sonoHeader = D.sonoHeader, imageQuality = D.imageQuality,
      biometry = D.biometry, impression = D.impression,
      placentaBlock = D.placentaBlock, maternalAnatomy = D.maternalAnatomy;

  var SIGNER = '예시 의사';

  /* ========================================================
     1. Labor record Ⅱ-OB
     ======================================================== */
  var laborRecord = {
    id: 'labor2',
    title: 'Labor record Ⅱ-OB',
    dept: '산부인과',
    summary: '분만 진행 경과 기록 (내진·태아심음·자궁수축)',
    writtenOn: '2026-09-15',
    labelW: 110,
    rows: [
      S.sec('임신력'),
      S.row('', [lbl('G'), t(40, { val: '2' }), lbl('P'), t(40, { val: '1' }), t(40, { val: '0' }),
                 lbl('D'), t(40, { val: '0' }), lbl('A'), t(40, { val: '0' })]),
      S.row('임신주수', [t(50, { val: '39', hl: true }), lbl('(weeks)'),
                      t(50, { val: '2', hl: true }), lbl('(day)')]),
      S.grid({
        title: 'Labor record',
        cols: [{ h: 'Date/Time', w: 110 }, { h: 'P', w: 44 }, { h: 'Dil', w: 50 }, { h: 'Eff', w: 50 },
               { h: 'St', w: 44 }, { h: 'ROM', w: 60 }, { h: 'FHB', w: 54 },
               { h: 'Ut. Cont.', w: 190 }, { h: 'Mx', w: 150 }],
        rows: 7,
        data: [
          ['09-15 08:20', 'Vx', '2', '50', '-3', 'intact', '142', 'q 5min / 30sec / mild', '입원, 관찰 시작'],
          ['09-15 10:00', 'Vx', '3', '60', '-3', 'intact', '145', 'q 4min / 35sec / moderate', 'Oxytocin 시작'],
          ['09-15 12:30', 'Vx', '5', '80', '-2', 'AROM (clear)', '140', 'q 3min / 40sec / moderate', 'Epidural PCA'],
          ['09-15 14:10', 'Vx', '7', '90', '-1', '', '138', 'q 3min / 45sec / strong', ''],
          ['09-15 15:40', 'Vx', '10', '100', '0', '', '144', 'q 2min / 50sec / strong', 'Full dilatation'],
          ['09-15 16:05', 'Vx', '10', '100', '+2', '', '136', 'q 2min / 55sec / strong', 'Push 시작'],
          ['', '', '', '', '', '', '', '', '']
        ]
      }),
      S.grid({
        title: 'labor record-기타',
        cols: [{ h: 'Date/Time', w: 110 }, { h: 'Remark', w: 190 }, { h: 'Other', w: 260 },
               { h: '기록자명', w: 90 }],
        rows: 5,
        data: [
          ['09-15 08:25', 'NST reactive', 'Baseline 140 bpm, acceleration (+), deceleration (-)', '예시 간호사'],
          ['09-15 12:30', '양막 파수', '양수 clear, 악취 없음', '예시 간호사'],
          ['09-15 14:15', '무통분만', 'Epidural PCA 적용, VAS 8 → 3', '예시 간호사']
        ]
      })
    ]
  };

  /* ========================================================
     2. 수술기록-OB C/sec
     ======================================================== */
  var cSection = {
    id: 'csec',
    title: '수술기록-OB C/sec',
    dept: '산부인과',
    summary: '제왕절개 수술기록 (마취·절개·봉합·출혈량)',
    writtenOn: '2026-09-15',
    labelW: 200,
    rows: [
      S.row('수술일', [d('2026-09-15')]),
      S.note('* 같은 수술에 집도의가 2명인 경우에만 체크해 주세요.'),
      S.row('집도의', [t(160, { val: '예시 의사 A', hl: true }), btn('직원선택'), lbl('/'),
                     t(130), btn('직원선택'), k(['집도의 추가'])]),
      S.row('', [sel(['주집도의', '공동집도의'], '주집도의', 100)]),
      S.row('보조의', [t(330, { val: '예시 의사 B' })]),
      S.row('수술진료지원간호사', [t(330, { val: '예시 간호사' })]),

      S.grid({
        title: '수술전 진단명', peach: true,
        cols: [{ h: '진단명' }], rows: 2,
        data: [['Late postpartum preeclampsia'], ['Hypertension in the puerperium']]
      }),
      S.row('진단 부가 설명', [ta(56, { w: 450 })]),
      S.grid({
        title: '수술후 진단명', peach: true, btns: ['OCS진단', '추가', '삭제'],
        cols: [{ h: '진단명' }], rows: 2,
        data: [['Late postpartum preeclampsia'], ['Hypertension in the puerperium']]
      }),
      S.row('진단 부가 설명', [ta(56, { w: 450 })]),
      S.grid({
        title: '수술명', peach: true,
        cols: [{ h: '수술명' }], rows: 3,
        data: [['Cesarean section, low transverse'], ['Adhesiolysis'], ['']]
      }),
      S.row('수술 부가 설명', [ta(72, { w: 470 })]),

      S.row('Level', [r(['elective', 'level A', 'level B', 'level C'], 'elective')]),
      S.row('Cesarean section indication', [t(470, { hl: true, val: 'Previous cesarean section' })]),
      S.row('Anesthesia', [k(['General', 'Spinal', 'Epidural', 'Failed regional'], ['Spinal']),
                           br(), k(['Other']), t(230, { fill: true })]),
      S.row('PCA', [r(['IV', 'Epidural', 'Not done'], 'IV')]),
      S.row('Skin Incision', [r(['Pfannenstiel', 'Low midline'], 'Pfannenstiel', { other: 250 })]),
      S.row('Old Scar Revision', [r(['Not done', 'Done'], 'Not done')]),
      S.row('Type of uterine incision', [r(['Low flap transverse', 'Vertical', 'Inverted-T', 'Classical'],
                                          'Low flap transverse', { other: 250 })]),
      S.row('Amniotic fluid', [r(['clear', 'meconium stained', 'bloody'], 'clear')]),
      S.note('* Meconium stained 가 있을 경우'),
      S.row('Meconium stained', [r(['mild', 'moderate', 'severe'])], 1),
      S.row('Uterine low flap thickness', [r(['>3mm', '≤3mm'], '>3mm', { other: 200 })]),
      S.row('', [lbl('Endomyometrial layer and myofascial layer were repaired in'),
                 sel(['one-layered', 'two-layered'], 'two-layered', 110), lbl('fashion')]),
      S.row('', [lbl('by'), sel(['continuous running and locking suture', 'continuous running suture',
                                 'interrupted suture'], 'continuous running and locking suture', 240),
                 lbl('using'), sel(['Vicryl', 'Monosyn', 'PDS'], 'Vicryl', 90), t(60, { val: '#1-0' })]),
      S.row('Serosal repair', [r(['not done', 'done in a continuous running suture using'], 'not done'),
                               lbl('Vicryl #2-0')]),
      S.row('Tubal sterilization', [r(['Not done', 'Done'], 'Not done')]),
      S.row('Tubal sterilization method', [t(250)]),

      S.sec('Operative findings'),
      S.row('Other organs', [lbl('both ovaries'), r(['normal', 'abnormal'], 'normal'),
                             t(230, { fill: true })]),
      S.row('', [lbl('Others　　　　　'), r(['normal', 'abnormal'], 'normal'), t(230, { fill: true })]),
      S.row('Uterine Contraction', [r(['firm', 'atonic'], 'firm')]),
      S.row('Antiadhesive used', [k(['Seprafilm', 'Medicurtain', 'Protad', 'Hyalobarrier', 'Guardix'], []),
                                  k(['Other']), t(110, { fill: true })]),

      S.sec('Closure'),
      S.stat('The abdominal wall was closed in layers ;'),
      S.row('', [lbl('Peritoneal repair : continuous sutures of'),
                 sel(['Vicryl #1-0', 'Vicryl #2-0'], 'Vicryl #1-0', 110),
                 lbl('for the peritoneum and the rectus muscle')]),
      S.row('', [lbl('Fascia layer repair :'), sel(['continous running', 'interrupted'],
                 'continous running', 130), lbl('sutures of'),
                 sel(['Vicryl #1-0', 'PDS #1-0'], 'Vicryl #1-0', 110), lbl('for the fascia')]),
      S.stat("Subcutaneous layer repair including Scarpa's fascia :"),
      S.row('', [r(['Not done', 'Done'], 'Done'), lbl('( by'),
                 sel(['inverted knot buried', 'simple interrupted'], 'inverted knot buried', 160),
                 lbl('sutures using'),
                 sel(['Vicryl #1-0', 'Vicryl #2-0', 'Vicryl #3-0'], 'Vicryl #2-0', 110), lbl(')')], 1),
      S.row('', [lbl('The skin was closed with'),
                 sel(['continuous subcuticular', 'interrupted', 'staple'], 'continuous subcuticular', 180),
                 sel(['Nylon', 'Monosyn', 'Skin stapler'], 'Monosyn', 110),
                 sel(['#3-0', '#4-0', '#5-0'], '#4-0', 70), t(150)]),
      S.stat('Hemostasis was assured in all steps.'),

      S.sec('Operative summary'),
      S.row('Estimated blood loss (cc)', [t(140, { hl: true, val: '700' }), btn('재조회')]),
      S.row('Remarkable Operation Findings', [ta(56, { w: 470 })]),
      S.row('수술과정 중 특이사항', [r(['No', 'Yes'], 'No', { tail: t(160, { fill: true }) })]),
      S.row('Placenta', [t(70, { val: '600.0' }), lbl('(gram)')]),
      S.row('Placenta gross finding', [r(['normal', 'other'], 'normal', { tail: t(260, { fill: true }) })]),
      S.row('Use of medication during the surgery',
            [k(['duratocin', 'pitocin', 'methylergonovine'], ['pitocin']),
             k(['Other']), t(110, { fill: true })]),

      S.sec('NEONATE RECORD'),
      S.note('Refer to Delivery-Discharge Note'),
      S.row('Tissue to Pathology', [r(['No', 'Yes'], 'No'), lbl('('),
                                    k(['Placenta', 'Other']), t(110, { fill: true }), lbl(')')]),
      S.row('Drains', [r(['No', 'Yes'], 'No', { tail: t(130, { fill: true }) })]),
      S.row('Sponge count correct', [r(['Yes', 'No'], 'Yes', { tail: t(180, { fill: true }) })]),

      S.sec('그림'),
      S.draw('Text'),
      staff('기록자명', '예시 기록자')
    ]
  };

  /* ========================================================
     3. 1st trimester (single)-OB
     ======================================================== */
  var sono1st = {
    id: 'sono1st',
    title: '산전초음파 1st trimester (single)-OB',
    dept: '산부인과',
    summary: '임신 초기 초음파 판독 (CRL·NT·NB)',
    writtenOn: '2026-09-15',
    labelW: 150,
    rows: [].concat(
      sonoHeader('2026-04-10', '2026-12-25', ''),
      [imageQuality('Good Quality')],
      impression('11', 'Single intrauterine pregnancy, 11+3 weeks by CRL.\nFetal heart beat (+), NT within normal range.'),
      [
        S.sec('Early fetal measurement'),
        S.row('G-sac', [k(['+'], ['+']), t(80, { val: '5.4' }), lbl('cm'), t(60, { val: '11' }), lbl('w'),
                        t(60, { val: '2' }), lbl('d'), k(['-'])], 1),
        S.row('CRL', [k(['+'], ['+']), t(80, { val: '4.6' }), lbl('cm'), t(60, { val: '11' }), lbl('w'),
                      t(60, { val: '3' }), lbl('d'), k(['-'])], 1),
        S.row('Y-sac', [k(['+'], ['+']), t(80, { val: '0.4' }), lbl('cm'), k(['-'])], 1),
        S.row('FHB', [k(['+'], ['+']), t(60, { val: '162', fill: true }), lbl('bpm'), k(['-'])], 1),
        S.row('NT', [r(['measured', 'failed'], 'measured',
                       { inlineFields: { measured: [t(60, { val: '0.12', fill: true }), lbl('cm')] } })], 1),
        S.row('NB', [r(['present', 'not checked', 'absent'], 'present',
                       { inlineFields: { present: [t(60, { fill: true }), lbl('cm')] } })], 1),
        S.sec('Fetal biometry'),
        biometry('BPD', '', '', ''),
        biometry('HC', '', '', ''),
        biometry('AC', '', '', ''),
        biometry('FL', '', '', ''),
        biometry('HL', '', '', ''),
        S.row('Other Significant Findings', [ta(48, { w: 420 })])
      ],
      placentaBlock([], 'No'),
      maternalAnatomy('Other findings'),
      signBlock(SIGNER)
    )
  };

  /* ========================================================
     4. 2nd & 3rd trimester (single)-OB
     ======================================================== */
  var sono23 = {
    id: 'sono23',
    title: '산전초음파 2nd & 3rd trimester (single)-OB',
    dept: '산부인과',
    summary: '정밀 초음파 판독 (태아 계측·기관별 소견·도플러)',
    writtenOn: '2026-09-15',
    labelW: 190,
    rows: [].concat(
      sonoHeader('2026-07-18', '2026-12-25', ''),
      [imageQuality('Good Quality')],
      impression('30', 'Single intrauterine pregnancy, 30+2 weeks.\nAppropriate for gestational age. No gross anomaly detected.'),
      [
        S.sec('Fetal biometry'),
        biometry('BPD', '7.9', '31', '2'),
        biometry('HC', '28.4', '30', '5'),
        biometry('AC', '26.1', '30', '0'),
        biometry('FL', '5.8', '30', '3'),
        biometry('HL', '5.2', '30', '1'),
        S.row('EBW', [lbl('Min'), t(66, { val: '1520' }), lbl('(gm)'), lbl('~'), lbl('Max'),
                      t(66, { val: '1680' }), lbl('(gm)')], 1),
        S.row('Percentile', [lbl('Min'), t(66, { val: '42' }), lbl('(%ile)'), lbl('~'), lbl('Max'),
                             t(66, { val: '55' }), lbl('(%ile)')], 1),
        S.row('Heart activity', [k(['+'], ['+']), t(60, { val: '148', fill: true }), lbl('bpm'), k(['-'])]),
        S.row('Presentation', [r(['Vertex', 'Breech', 'T-lie', 'Oblique', 'Not checked'], 'Vertex')]),

        S.sec('Head & Neck'),
        nna('Ventricle', 'Normal', 1, [br(), lbl('Rt'), t(66, { val: '0.62' }), lbl('cm'),
                                       lbl('Lt'), t(66, { val: '0.58' }), lbl('cm')]),
        nna('Cerebellum', 'Normal', 1, [br(), t(60, { val: '3.4' }), lbl('cm'),
                                        t(60, { val: '30' }), lbl('wk'), t(60, { val: '1' }), lbl('d')]),
        nna('Cisterna magna', 'Normal', 1, [br(), t(60, { val: '0.55' }), lbl('cm')]),
        nna('Nasal bone', 'Not checked', 1, [br(), t(60), lbl('cm')]),
        nna('Nuchal fold thickness', 'Normal', 1, [br(), t(60), lbl('cm')]),
        nna('Cavum septum pellucidum', 'Normal', 1),
        nna('Nose/Lips', 'Normal', 1),
        nna('Orbits', 'Normal', 1),
        S.row('Others', [ta(44, { w: 420 })], 1),

        S.sec('Thorax'),
        nna('Lungs', 'Normal', 1),
        nna('Diaphragm', 'Normal', 1),
        S.row('Others', [ta(44, { w: 420 })], 1),

        S.sec('Heart'),
        nna('Cardiac axis', 'Normal', 1),
        nna('Cardiac position', 'Normal', 1),
        nna('4CV', 'Normal', 1),
        nna('3VV', 'Normal', 1),
        nna('Long axis', 'Normal', 1),
        nna('Short axis', 'Normal', 1),
        nna('Aortic arch', 'Normal', 1),
        nna('Ductal arch', 'Normal', 1),
        S.row('Others', [ta(44, { w: 420 })], 1),

        S.sec('Abdomen'),
        nna('Abdominal wall', 'Normal', 1),
        nna('Stomach', 'Normal', 1),
        nna('Umbilical cord', 'Normal', 1),
        S.row('Others', [ta(44, { w: 420 })], 1),

        S.sec('Genitourinary'),
        nna('Kidney', 'Normal', 1, [br(), lbl('Renal pelvis　Rt'), t(60, { val: '0.32' }), lbl('cm'),
                                    lbl('Lt'), t(60, { val: '0.30' }), lbl('cm')]),
        nna('Bladder', 'Normal', 1),
        nna('Genitalia', 'Normal', 1),
        S.row('Others', [ta(44, { w: 420 })], 1),

        S.sec('Skeletal system'),
        nna('Spine', 'Normal', 1),
        S.sub('Extremities and presence of hand'),
        nna('Rt', 'Normal', 2),
        nna('Lt', 'Normal', 2),
        S.sub('Extremities and presence of foot'),
        nna('Rt', 'Normal', 2),
        nna('Lt', 'Normal', 2),
        S.row('Long bone', [r(['Not checked', 'Checked'], 'Checked')], 1),
        S.set('Long bone', [
          longBone('Humerus', '5.2', '30', '2', '48'),
          longBone('Radius', '4.5', '30', '0', '45'),
          longBone('Ulna', '5.0', '30', '1', '46'),
          longBone('Femur', '5.8', '30', '3', '52'),
          longBone('Tibia', '5.1', '30', '2', '50'),
          longBone('Fibula', '4.9', '30', '1', '49'),
          S.row('', [ta(40, { w: 330 })], 1)
        ]),
        S.row('Others', [ta(44, { w: 420 })], 1),

        S.sec('Amniotic fluid'),
        S.row('', [k(['Amniotic fluid index', 'Deep pocket'], ['Deep pocket'])], 1),
        S.set('Deep pocket', [S.row('Deep pocket', [t(60, { val: '4.6' }), lbl('(cm)')], 1)]),
        S.row('Amount', [k(['Adequate', 'Oligohydramnios (AFI<5cm or deep pocket<2cm)',
                            'Decreased (5~10cm)', 'Increased (20~24cm)',
                            'Hydramnios (AFI>24cm or deep pocket>8cm)'], ['Adequate'])], 1)
      ],
      placentaBlock(['Posterior'], 'No'),
      [
        S.sec('Doppler ultrasound'),
        S.sub('Umbilical artery'),
        S.row('Systolic velocity', [t(66, { val: '42.1' })], 2),
        S.row('Diastolic velocity', [t(66, { val: '15.8' }), lbl('PI'), t(60, { val: '0.92' }),
                                     lbl('S/D'), t(80, { val: '2.66', ro: true })], 2),
        S.row('NEDV', [r(['No', 'Yes'], 'No', { tail: t(280, { fill: true }) })], 2),
        S.sub('Uterine artery'),
        S.row('Right　Systolic velocity', [t(66, { val: '58.2' })], 2),
        S.row('　　　 Diastolic velocity', [t(66, { val: '22.4' }), lbl('PI'), t(80, { val: '0.78', ro: true }),
                                          lbl('S/D'), t(80, { val: '2.60', ro: true })], 2),
        S.row('　　　 Early-diastolic notch', [r(['No', 'Yes'], 'No', { tail: t(260, { fill: true }) })], 2),
        S.row('Left 　Systolic velocity', [t(66, { val: '55.7' })], 2),
        S.row('　　　 Diastolic velocity', [t(66, { val: '21.6' }), lbl('PI'), t(80, { val: '0.80', ro: true }),
                                          lbl('S/D'), t(80, { val: '2.58', ro: true })], 2),
        S.row('　　　 Early-diastolic notch', [r(['No', 'Yes'], 'No', { tail: t(260, { fill: true }) })], 2),
        S.sub('Middle cerebral artery'),
        S.row('Systolic velocity', [t(66, { val: '45.3' })], 2),
        S.row('Diastolic velocity', [t(66, { val: '9.7' }), lbl('PI'), t(60, { val: '1.64' }),
                                     lbl('S/D'), t(80, { val: '4.67', ro: true })], 2),
        S.row('Cerebroplacental ratio', [t(66, { val: '1.78', ro: true })], 2)
      ],
      maternalAnatomy('Other Findings'),
      signBlock(SIGNER)
    )
  };

  /* 장골 계측 1행 (Rt / Lt) */
  function longBone(name, cm, wk, day, pct) {
    return S.row(name, [lbl('Rt'), t(56, { val: cm }), lbl('cm'), t(56, { val: wk }), lbl('wk'),
                        t(56, { val: day }), lbl('d'), t(56, { val: pct }), lbl('%ile'),
                        br(), lbl('Lt'), t(56, { val: cm }), lbl('cm'), t(56, { val: wk }), lbl('wk'),
                        t(56, { val: day }), lbl('d'), t(56, { val: pct }), lbl('%ile')], 1);
  }

  /* ========================================================
     5. Fetal echocardiography (single)-OB
     ======================================================== */
  var fetalEcho = {
    id: 'fetalecho',
    title: 'Fetal echocardiography (single)-OB',
    dept: '산부인과',
    summary: '태아 심장초음파 판독 (구조·리듬·도플러)',
    writtenOn: '2026-09-15',
    labelW: 190,
    rows: [].concat(
      sonoHeader('2026-07-18', '2026-12-25', ''),
      [S.row('Indication', [ta(56, { w: 440, val: 'Routine screening at 24 weeks' })])],
      impression('24', 'Structurally normal fetal heart at 24+1 weeks.\nNo evidence of arrhythmia or pericardial effusion.'),
      [
        S.sec('Cardiac position & size'),
        S.row('Heart axis', [r(['Levocardia(normal)', 'Extreme levocardia', 'Dextrocardia', 'Mesocardia'],
                               'Levocardia(normal)')]),
        S.row('Heart position', [r(['normal', 'dextroposition', 'levoposition'], 'normal')]),
        S.row('Heart size', [r(['Normal', 'Enlarged'], 'Normal')]),
        S.row('Heart/chest circumference', [t(66, { val: '8.4' }), lbl('(cm)'), lbl('/'),
                                            t(66, { val: '17.2' }), lbl('(cm)'), lbl('='),
                                            t(80, { val: '0.49', ro: true })], 1),
        S.row('Heart /chest area', [t(66, { val: '5.1' }), lbl('(cm²)'), lbl('/'),
                                    t(66, { val: '17.6' }), lbl('(cm²)'), lbl('='),
                                    t(80, { val: '0.29', ro: true })], 1),
        S.row('Chamber size', []),
        S.row('RA / LA', [t(60, { val: '1.12' }), lbl('(cm)'), lbl('/'), t(60, { val: '1.08' }),
                          lbl('(cm)'), lbl('='), t(80, { val: '1.04', ro: true })], 1),
        S.row('RV / LV', [t(60, { val: '1.21' }), lbl('(cm)'), lbl('/'), t(60, { val: '1.15' }),
                          lbl('(cm)'), lbl('='), t(80, { val: '1.05', ro: true })], 1),
        S.row('Vessel size', []),
        S.row('PA / Ao', [t(60, { val: '0.64' }), lbl('(cm)'), lbl('/'), t(60, { val: '0.58' }),
                          lbl('(cm)'), lbl('='), t(80, { val: '1.10', ro: true })], 1),

        S.sec('Structure'),
        na2('Four chamber view'), na2('Three vessels view'), na2('Long axis view'),
        na2('Short axis view'), na2('Aortic arch view'), na2('Ductal arch view'),
        S.row('Abnormal finding', [ta(48, { w: 420 })], 1),

        S.sec('Rhythm'),
        S.row('Rhythm', [r(['Regular', 'Irregular'], 'Regular')], 1),
        S.row('Baseline heart rate', [r(['Normal', 'Bradycardia', 'Tachycardia'], 'Normal'),
                                      lbl('(Bradycardia ≤100 bpm / Tachycardia ≥180 bpm)')], 1),
        S.row('M mode', [r(['Normal', 'Not checked', 'Abnormal'], 'Not checked')], 1),
        S.row('Other M mode finding', [ta(48, { w: 420 })], 1),
        S.row('Pericardial effusion', [r(['absent', 'present'], 'absent',
                                         { tail: t(120, { fill: true }) })], 1),

        S.sec('Doppler'),
        S.row('Aorta', [t(90, { val: '78' }), lbl('cm/sec')], 1),
        S.row('Pulmonary artery', [t(90, { val: '72' }), lbl('cm/sec')], 1),
        S.sub('Atrioventricular valves'),
        S.row('TV', [lbl('E'), t(60, { val: '32' }), lbl('/'), lbl('A'), t(60, { val: '52' }),
                     lbl('='), t(80, { val: '0.62', ro: true })], 2),
        S.row('MV', [lbl('E'), t(60, { val: '30' }), lbl('/'), lbl('A'), t(60, { val: '50' }),
                     lbl('='), t(80, { val: '0.60', ro: true })], 2),
        S.row('Ductus venosus', [lbl('S'), t(90, { val: '58' }), lbl('D'), t(90, { val: '42' }), br(),
                                 lbl('A'), r(['normal', 'not checked', 'abnormal'], 'normal',
                                             { tail: t(150, { fill: true }) })], 2),
        S.sub('Other'),
        checked2('Inferior vena cava'), checked2('Superior vena cava'),
        checked2('Pulmonary veins'), checked2('Hepatic veins'),
        S.row('Other Doppler findings', [ta(48, { w: 420 })], 1)
      ],
      signBlock(SIGNER)
    )
  };

  function na2(label) {
    return S.row(label, [r(['Normal', 'Abnormal'], 'Normal')], 1);
  }
  function checked2(label) {
    return S.row(label, [r(['Not checked', 'Checked'], 'Not checked', { tail: t(120, { fill: true }) })], 2);
  }

  /* ========================================================
     6. Fetal Neurosonography (single)-OB
     ======================================================== */
  var neuroSono = {
    id: 'neurosono',
    title: 'Fetal Neurosonography (single)-OB',
    dept: '산부인과',
    summary: '태아 신경초음파 판독 (뇌실·중심구조·후두와·척추)',
    writtenOn: '2026-09-15',
    labelW: 190,
    rows: [].concat(
      sonoHeader('2026-07-18', '2026-12-25', ''),
      [imageQuality('Good Quality'),
       S.row('Indication', [ta(56, { w: 440, val: 'Detailed neurosonography at 24 weeks' })])],
      impression('24', 'Normal fetal neurosonographic findings at 24+1 weeks.'),
      [
        S.row('Presentation', [r(['Vertex', 'Breech', 'T-lie', 'Oblique', 'Not checked'], 'Vertex')]),

        S.sec('Cranium'),
        nna('Size', 'Normal', 1), nna('BPD', 'Normal', 1), nna('HC', 'Normal', 1),
        nna('Shape', 'Normal', 1), nna('Integrity', 'Normal', 1),
        S.row('Other', [ta(44, { w: 420 })], 1),

        S.sec('Ventricular system'),
        S.sub('Anterior horn'),
        nna('Rt', 'Normal', 2), nna('Lt', 'Normal', 2),
        S.sub('Lateral ventricle'),
        nna('Rt', 'Normal', 2), nna('Lt', 'Normal', 2),
        S.sub('Atrial diameter'),
        S.row('Rt', [t(66, { val: '6.2' }), lbl('(mm)')], 2),
        S.row('Lt', [t(66, { val: '5.8' }), lbl('(mm)')], 2),
        S.sub('Choroid plexus'),
        nna('Rt', 'Normal', 2), nna('Lt', 'Normal', 2),
        nna('3rd ventricle', 'Normal', 1), nna('4th ventricle', 'Normal', 1),
        S.row('Other', [ta(44, { w: 420 })], 1),

        S.sec('Midline structure'),
        nna('Falx', 'Normal', 1), nna('CSP', 'Normal', 1),
        S.sub('Corpus callosum'),
        nna('Shape', 'Normal', 2),
        S.row('Length', [t(66, { val: '3.6' }), lbl('(cm)')], 2),
        nna('Pericallosal flow', 'Normal', 2),
        nna('Thalami', 'Normal', 1),
        S.row('Other', [ta(44, { w: 420 })], 1),

        S.sec('Cerebrum'),
        nna('Symmetry', 'Normal', 1), nna('Echogenicity', 'Normal', 1),
        nna('Periventricular', 'Normal', 1),
        S.sub('Sulcus and fissure (for GA)'),
        nna('Sylvian', 'Normal', 2), nna('Parieto-occipital', 'Normal', 2),
        nna('Calcarine', 'Normal', 2), nna('Cingulate', 'Normal', 2),
        nna('Frontal', 'Normal', 2), nna('Central', 'Normal', 2), nna('Temporal', 'Normal', 2),
        S.row('Other', [ta(44, { w: 420 })], 1),

        S.sec('Posterior fossa'),
        nna('Cerebellum', 'Normal', 1), nna('Cisterna magna', 'Normal', 1),
        nna('Vermis', 'Normal', 1), nna('Fastigium', 'Normal', 1),
        nna('Brainstem-to-vermis angle', 'Normal', 1, [br(), t(66, { val: '12' }), lbl('degrees')]),
        nna('Brainstem-to-tentorium angle', 'Normal', 1, [br(), t(66, { val: '32' }), lbl('degrees')]),
        S.row('Other', [ta(44, { w: 420 })], 1),

        S.sec('Vasculature'),
        nna('Sinus', 'Normal', 1), nna('Circle of Willis', 'Normal', 1),
        nna('Vein of Galen', 'Normal', 1),
        S.row('Other', [ta(44, { w: 420 })], 1),

        S.sec('Spine'),
        nna('Canal integrity', 'Normal', 1), nna('Skin integrity', 'Normal', 1),
        S.row('Conus medullaris position', [lbl('Level'), t(44, { val: 'L2' }), lbl(':'),
                                            t(66, { val: '0.4' }), lbl('(cm)'),
                                            k(['Not checked'])], 1),
        S.row('Other', [ta(44, { w: 420 })], 1)
      ],
      signBlock(SIGNER)
    )
  };

  global.OB_FORMS = [laborRecord, cSection, sono1st, sono23, fetalEcho, neuroSono];
})(window);
