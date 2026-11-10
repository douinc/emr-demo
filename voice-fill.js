/* ============================================================
   음성 서식 입력 (voice fill)
   산과(OB) 구조화 서식지 위에 녹음 버튼을 달고, 말한 내용으로 서식 칸을 채운다.

     화면 ──(WS: 오디오 · 서식 정의 · 출력 구조)──▶ 중계 서버 ──▶ Dou Open API
          ◀──(전사 · 서식 결과 전체 JSON)──────────

   - buildModel(form)  : 서식 스키마(ob-forms-data.js) → 칸 목록 · output_schema · 서식 설명(source)
   - attach(host, form): 그려진 서식 위에 녹음 막대를 붙인다
   프레임 계약은 Dou Open API 의 "실시간 서식 결과 세션"(WS /v1/documents/stream)과 같은 모양이다.
   ============================================================ */
(function (global) {
  'use strict';

  var DEFAULT_API = 'https://playground.dou.so/emr-demo-api';
  var LS_CODE = 'emrDemo.voice.accessCode';
  var LS_API = 'emrDemo.voice.api';
  var LS_FOLLOW = 'emrDemo.voice.follow';
  var REFRESH_SECONDS = 8;

  /* ============================================================
     설정
     ============================================================ */
  function store(key, value) {
    try {
      if (value === undefined) return global.localStorage.getItem(key);
      if (value === null || value === '') global.localStorage.removeItem(key);
      else global.localStorage.setItem(key, value);
    } catch (e) { /* 저장소를 못 쓰는 환경(사생활 보호 모드 등)에서는 이번 화면에서만 유지 */ }
    return value;
  }
  var memory = {};
  function setting(key, value) {
    if (value !== undefined) { memory[key] = value || ''; store(key, value); return value; }
    var saved = store(key);
    return saved != null ? saved : (memory[key] || '');
  }
  function apiBase() {
    var fromQuery = '';
    try { fromQuery = new URLSearchParams(global.location.search).get('voiceApi') || ''; } catch (e) { /* 무시 */ }
    // 주소창 값은 이 PC(로컬 개발)일 때만 받는다 — 링크 하나로 접속 코드·마이크 소리가 남의 서버로 가지 않게.
    if (fromQuery && !/^http:\/\/(localhost|127\.0\.0\.1)(:\d+)?(\/|$)/.test(fromQuery)) fromQuery = '';
    return (fromQuery || setting(LS_API) || DEFAULT_API).replace(/\/+$/, '');
  }
  function wsUrl(path) { return apiBase().replace(/^http/, 'ws') + path; }

  /* ============================================================
     서식 → 칸 목록(모델)
     ============================================================ */
  /* 표 열 이름에 쓰는 한글 머리글 → 영문 키 */
  var KO_SLUG = [['수술명', 'operation_name'], ['진단명', 'diagnosis'], ['기록자명', 'recorder']];
  var UNIT_RE = /^\(?\s*(cm|mm|w|d|wk|weeks|day|bpm|gm|gram|%ile|cm²|degrees|cm\/sec)\s*\)?$/;   // 대소문자 구분: 'D'(Death) 는 단위가 아니다
  var SEP_RE = /^[\/=~:()\-\s]*$/;
  /* 표 열 머리글 약어 풀이(분만 경과표) */
  var COLUMN_HINT = {
    'Date/Time': '날짜·시각(예: 11-10 08:20)', 'P': 'Presentation 선진부(예: Vx)', 'Dil': 'Cervical dilatation 개대(cm, 숫자만)',
    'Eff': 'Effacement 소실(%, 숫자만)', 'St': 'Station(예: -3, 0, +2)', 'ROM': 'Rupture of membranes 양막 상태(예: intact, AROM, SROM)',
    'FHB': 'Fetal heart beat 태아 심박수(bpm, 숫자만)', 'Ut. Cont.': 'Uterine contraction 자궁수축(간격 / 지속 / 강도, 예: q 5min / 30sec / mild)',
    'Mx': 'Management 처치·경과', 'Remark': '기록 제목(예: NST reactive, 양막 파수)', 'Other': '상세 내용'
  };
  var CHECK_HINT = { '+': "'+'(있음·보임) 체크 여부", '-': "'-'(없음·안 보임) 체크 여부" };

  function clean(text) {
    return String(text == null ? '' : text).replace(/[　\s]+/g, ' ').trim();
  }
  function slug(text) {
    var s = clean(text);
    KO_SLUG.forEach(function (pair) { s = s.split(pair[0]).join(' ' + pair[1] + ' '); });
    return s.toLowerCase().replace(/[^a-z0-9]+/g, '_').replace(/^_+|_+$/g, '');
  }
  function today() {
    var d = new Date();
    return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
  }

  function buildModel(form) {
    var fields = [];          // 음성으로 채우는 칸
    var clearables = [];      // 빈 서식으로 만들 때 비울 스키마 조각(읽기 전용 포함)
    var ratios = [];          // 같은 행의 a / b = (자동 계산) 칸
    var outline = [];         // 서식 설명(source)의 항목 줄
    var terms = [];
    var usedDesc = {};
    var sec = '', sub = '', setLabel = '', prevNamed = '', prevBlockName = '', sidePrefix = '';
    var skipping = false;

    function term(text) {
      var t = clean(text).replace(/\s*\(.*$/, '');
      if (/[A-Za-z]{2}/.test(t) && t.length <= 40 && terms.indexOf(t) === -1) terms.push(t);
    }
    function uniqueDesc(desc) {
      if (!usedDesc[desc]) { usedDesc[desc] = 1; return desc; }
      usedDesc[desc] += 1;
      var extra = prevBlockName ? " (앞 항목 '" + prevBlockName + "' 다음, " + usedDesc[desc] + '번째)' : ' (' + usedDesc[desc] + '번째)';
      return desc + extra;
    }
    function path() {
      return Array.prototype.slice.call(arguments).filter(Boolean).join(' > ');
    }
    /* 키는 짧은 순번(f1, f2 …)이다. 뜻은 description 이 싣는다 — 긴 키는 칸이 100개를 넘는 서식에서
       모델의 구조화 출력 한도를 넘겨 생성이 실패한다(2026-11-10 스테이징 실측) */
    function add(field) {
      field.key = 'f' + (fields.length + 1);
      field.desc = uniqueDesc(field.desc);
      fields.push(field);
      return field;
    }

    /* 행 하나를 사람이 읽는 한 줄로 */
    function sentence(items) {
      return items.map(function (it) {
        switch (it.c) {
          case 'lbl': return clean(it.text);
          case 't': return it.ro ? '(자동 계산)' : '___';
          case 'd': return '[날짜]';
          case 'ta': return '[서술]';
          case 's': return '[선택: ' + it.opts.join(' / ') + ']';
          case 'r': return '(택1: ' + it.opts.map(function (o) {
            var inline = it.inlineFields && it.inlineFields[o];
            return inline ? o + ' ' + sentence(inline) : o;
          }).concat(it.other ? [(it.otherLabel || 'Other') + ' ___'] : []).join(' / ') + ')' + (it.tail ? ' ___' : '');
          case 'k': return '(체크: ' + it.opts.join(' / ') + ')';
          case 'br': return ';';
          default: return '';
        }
      }).filter(Boolean).join(' ');
    }

    function controlField(spec, base, local, unit) {
      var where = path(base, local);
      var u = unit ? ' (' + unit + ')' : '';
      switch (spec.c) {
        case 't':
          return add({ kind: 'text', spec: spec,
            desc: where + u + (unit ? ' — 숫자만' : '') });
        case 'ta':
          return add({ kind: 'textarea', spec: spec, desc: where + ' — 서술' });
        case 'd':
          return add({ kind: 'date', spec: spec, desc: where + ' — 날짜 YYYY-MM-DD' });
        case 's':
          spec.opts.forEach(term);
          return add({ kind: 'select', spec: spec, options: spec.opts.slice(), desc: where });
        case 'r': {
          spec.opts.forEach(term);
          var options = spec.opts.concat(spec.other ? [spec.otherLabel || 'Other'] : []);
          var radio = add({ kind: 'radio', spec: spec, options: options, desc: where });
          subFields(spec, where);
          return radio;
        }
        case 'k': {
          spec.opts.forEach(term);
          if (spec.opts.length === 1) {
            var opt = spec.opts[0];
            var boolField = add({ kind: 'bool', spec: spec,
              desc: where + ' — ' + (CHECK_HINT[opt] || "'" + opt + "' 체크 여부") });
            subFields(spec, where);
            return boolField;
          }
          var checks = add({ kind: 'checks', spec: spec, options: spec.opts.slice(),
            desc: where + ' — 해당하는 것 모두' });
          subFields(spec, where);
          return checks;
        }
        default: return null;
      }
    }
    /* 선택지에 딸린 입력칸: 선택지 옆 값(inlineFields) · Other 내용 · 뒤따르는 부가 내용(tail) */
    function subFields(spec, where) {
      clearables.push({ spec: spec, other: !!spec.other });
      Object.keys(spec.inlineFields || {}).forEach(function (option) {
        var items = spec.inlineFields[option];
        items.forEach(function (item, i) {
          if (item.c !== 't') return;
          var next = items[i + 1];
          var unit = next && next.c === 'lbl' && UNIT_RE.test(clean(next.text)) ? clean(next.text).replace(/[()]/g, '') : '';
          clearables.push({ spec: item });
          add({ kind: 'text', spec: item,
            desc: where + " — '" + option + "' 일 때 값" + (unit ? ' (' + unit + ') — 숫자만' : '') });
        });
      });
      if (spec.other) {
        add({ kind: 'text', spec: spec, part: 'other',
          desc: where + " — '" + (spec.otherLabel || 'Other') + "' 를 골랐을 때 그 내용" });
      }
      if (spec.tail && spec.tail.c === 't') {
        clearables.push({ spec: spec.tail });
        add({ kind: 'text', spec: spec.tail,
          desc: where + ' — 부가 내용(해당할 때만)' });
      }
    }

    function rowFields(block) {
      var raw = block.label || '';
      var label = clean(raw);
      var side = raw.match(/^(Right|Left)[\s　]+(.+)$/);
      if (side) { sidePrefix = side[1]; label = side[1] + ' ' + clean(side[2]); }
      else if (/^　/.test(raw) && sidePrefix) label = sidePrefix + ' ' + label;
      else sidePrefix = '';
      var items = block.items || [];
      var inputs = items.filter(function (it) { return /^(t|d|ta|s|r|k)$/.test(it.c); });
      if (!inputs.length) { if (label) { prevNamed = label; term(label); } return; }
      term(label);

      var prose = !label && items.some(function (it) { return it.c === 'lbl' && clean(it.text).length > 14; });
      var base = path(sec, sub, setLabel, label || (prose ? '' : (prevNamed ? prevNamed + ' (세부)' : '')));
      outline.push('- ' + (label ? label + ': ' : '') + sentence(items));

      // "A / B = 비율" 행: 라벨의 A, B 를 두 칸의 이름으로 쓴다
      var plain = items.filter(function (it) { return it.c === 't' && !it.ro; });
      var hasSlash = items.some(function (it) { return it.c === 'lbl' && clean(it.text) === '/'; });
      var hasBefore = items.some(function (it, i) {
        return it.c === 'lbl' && !UNIT_RE.test(clean(it.text)) && !SEP_RE.test(clean(it.text)) &&
          items[i + 1] && items[i + 1].c === 't';
      });
      var ratioNames = (hasSlash && plain.length === 2 && label.indexOf('/') !== -1 && !hasBefore)
        ? label.split('/').map(clean) : null;
      var ratioInputs = [];

      var pending = [], carry = '', lastBefore = '', prevCtrlIndex = -2, localSeen = {};
      items.forEach(function (it, i) {
        if (it.c === 'br') { pending = []; carry = ''; prevCtrlIndex = -2; return; }
        if (it.c === 'b') return;
        if (it.c === 'lbl') {
          var text = clean(it.text);
          if (prevCtrlIndex === i - 1 && UNIT_RE.test(text)) return;   // 앞 칸의 단위
          if (SEP_RE.test(text)) return;
          term(text);
          pending.push(text);
          return;
        }
        if (!/^(t|d|ta|s|r|k)$/.test(it.c)) return;
        var next = items[i + 1];
        var unit = next && next.c === 'lbl' && UNIT_RE.test(clean(next.text)) ? clean(next.text).replace(/[()]/g, '') : '';
        var before = pending.join(' ');
        pending = [];
        if (before === 'Lt' && /\sRt$/.test(lastBefore)) before = lastBefore.replace(/Rt$/, 'Lt');
        if (before) { lastBefore = before; carry = before; }
        var prev = items[i - 1];
        var local = before;
        if (!local) {
          if (ratioNames && it.c === 't' && !it.ro) local = ratioNames[ratioInputs.length] || '';
          else if (prev && prevCtrlIndex === i - 1 && prev.c === 'k' && prev.opts.length === 1) {
            local = /^[+-]$/.test(prev.opts[0]) ? carry : prev.opts[0] + ' 내용';
          }
          else if (prev && prevCtrlIndex === i - 1 && (prev.c === 'r' || prev.c === 'k')) local = (carry ? carry + ' ' : '') + '부가 내용';
          else local = carry;
        }
        if (prose) local = sentence(items.map(function (other, j) {
          return j === i ? { c: 'lbl', text: '[이 칸]' } : other;
        }));
        prevCtrlIndex = i;
        if (it.c === 't' && it.ro) {
          clearables.push({ spec: it });
          if (hasSlash && ratioInputs.length === 2) ratios.push({ target: it, a: ratioInputs[0], b: ratioInputs[1] });
          return;
        }
        if (it.c !== 'r' && it.c !== 'k') clearables.push({ spec: it });
        // 같은 행에서 이름이 겹치면 순번을 붙인다
        var tag = local + '|' + unit + '|' + (it.c === 'k' && it.opts.length === 1 ? it.opts[0] : it.c);
        localSeen[tag] = (localSeen[tag] || 0) + 1;
        var shown = localSeen[tag] > 1 ? (local ? local + ' ' : '') + localSeen[tag] + '번째 칸' : local;
        var field = controlField(it, base, shown, unit);
        if (field && field.kind === 'text' && plain.indexOf(it) !== -1) ratioInputs.push(field);
      });
      if (label) prevNamed = label;
    }

    function gridField(block) {
      var title = clean(block.title);
      term(title);
      var columns = block.cols.map(function (col) {
        var name = clean(col.h);
        term(name);
        return { key: slug(name) || 'value', name: name, hint: COLUMN_HINT[name] || '' };
      });
      var seen = {};
      columns.forEach(function (col) {
        if (seen[col.key]) col.key += '_' + (seen[col.key] + 1);
        seen[col.key] = (seen[col.key] || 0) + 1;
      });
      var rows = Math.max(block.rows || 0, (block.data || []).length);
      outline.push('- [표] ' + title + ' — 열: ' + columns.map(function (c) { return c.name; }).join(' | ') + ' (최대 ' + rows + '행)');
      clearables.push({ grid: block });
      add({ kind: 'grid', spec: block, columns: columns, rows: rows,
        desc: path(sec, '표 ' + title) + ' — 말한 순서대로 한 행에 한 건, 최대 ' + rows + '행' });
    }

    function walk(rows) {
      rows.forEach(function (block) {
        if (block.type === 'signstart') { skipping = true; return; }
        if (block.type === 'signend') { skipping = false; return; }
        if (skipping) return;
        switch (block.type) {
          case 'sec':
            sec = clean(block.label); sub = ''; prevNamed = ''; sidePrefix = '';
            term(sec); outline.push('', '## ' + sec); prevBlockName = sec; break;
          case 'sub':
            sub = clean(block.label); prevNamed = ''; sidePrefix = '';
            term(sub); outline.push('### ' + sub); prevBlockName = sub; break;
          case 'static':
            prevNamed = clean(block.text).replace(/\s*[:;]$/, '');
            outline.push('  ' + clean(block.text)); break;
          case 'note': outline.push('  (안내) ' + clean(block.text)); break;
          case 'grid': gridField(block); prevBlockName = clean(block.title); break;
          case 'set': {
            var before = setLabel;
            setLabel = clean(block.label);
            outline.push('  [' + setLabel + ' 묶음]');
            walk(block.rows);
            setLabel = before;
            break;
          }
          case 'row':
            // 직원 선택 칸(기록자명 등 서명)은 음성으로 채우지 않는다
            if ((block.items || []).some(function (it) { return it.c === 'b' && it.label === '직원선택'; }) &&
                /기록자명|판독의|전문의/.test(block.label || '')) return;
            rowFields(block);
            if (clean(block.label)) prevBlockName = clean(block.label);
            break;
          default: break;
        }
      });
    }
    walk(form.rows);

    return { form: form, fields: fields, clearables: clearables, ratios: ratios, outline: outline, terms: terms };
  }

  /* ---------------- output_schema ---------------- */
  function buildSchema(model) {
    var day = today();
    var properties = {};
    model.fields.forEach(function (f) {
      var p;
      switch (f.kind) {
        case 'select':
        case 'radio':
          p = { type: ['string', 'null'], enum: f.options.concat([null]), description: f.desc };
          break;
        case 'bool': p = { type: ['boolean', 'null'], description: f.desc }; break;
        case 'checks':
          p = { type: ['array', 'null'], items: { type: 'string', enum: f.options }, description: f.desc };
          break;
        case 'grid': {
          var cols = {};
          f.columns.forEach(function (c) {
            cols[c.key] = { type: ['string', 'null'], description: c.name + (c.hint ? ' — ' + c.hint : '') };
          });
          p = { type: ['array', 'null'], maxItems: f.rows, description: f.desc,
            items: { type: 'object', properties: cols,
              required: f.columns.map(function (c) { return c.key; }), additionalProperties: false } };
          break;
        }
        case 'date':
          p = { type: ['string', 'null'], description: f.desc + ' (오늘은 ' + day + ')' };
          break;
        default: p = { type: ['string', 'null'], description: f.desc };
      }
      properties[f.key] = p;
    });
    return { type: 'object', properties: properties,
      required: model.fields.map(function (f) { return f.key; }), additionalProperties: false };
  }

  /* ---------------- 서식 설명(source) ---------------- */
  function buildSource(model) {
    var form = model.form;
    return [
      '서식: ' + form.title + ' (' + (form.dept || '') + ')',
      '용도: ' + (form.summary || ''),
      '',
      '의사가 검사·시술·수술을 하면서(또는 마친 뒤) 말로 불러 준 구술을 듣고, 아래 구조화 서식의 칸을 채운다.',
      '',
      '[작성 규칙]',
      '- 구술에서 말하지 않은 칸은 비워 둔다(JSON 결과에서는 null). 추측하지 않고, 서식의 흔한 기본값(Normal 등)도 넣지 않는다.',
      '- 다만 "나머지는 모두 정상", "A, B, C 는 정상" 처럼 여러 항목을 묶어 말하면 해당하는 판정 칸을 각각 그 값으로 채운다.',
      '- 숫자 칸은 단위를 빼고 숫자만 쓴다(예: "4.6 센티" → 4.6). 칸의 단위와 다른 단위로 말하면 칸의 단위로 바꿔 쓴다.',
      '- 임신 주수 "11주 3일" 은 w(주) 칸에 11, d(일) 칸에 3 으로 나눠 쓴다.',
      '- 선택 칸은 서식에 적힌 선택지 글자 그대로 고른다. 선택지에 없는 내용이면 Other 를 고르고 그 내용을 옆 칸에 쓴다.',
      '- 날짜는 YYYY-MM-DD 로 쓴다. "오늘" 은 작성일이다.',
      '- 서술 칸은 구술 내용을 의무기록 문체로 간결하게 쓰고, 영어 의학 용어는 영어 그대로 둔다.',
      '- 표는 말한 순서대로 한 행에 한 건씩 쓴다.',
      '- 같은 항목을 고쳐 말하면 마지막에 말한 값을 쓴다.',
      '- 기록자·판독의·전문의 서명 칸은 채우지 않는다.',
      '',
      '[서식 항목 — 화면에 놓인 순서. ___ 는 입력 칸]'
    ].concat(model.outline).join('\n').replace(/\n{3,}/g, '\n\n');
  }

  function describe(form) {
    if (typeof form === 'string') {
      form = (global.OB_FORMS || []).filter(function (f) { return f.id === form; })[0];
      if (!form) throw new Error('unknown form');
    }
    var model = buildModel(form);
    return {
      form: { id: form.id, title: form.title, source: buildSource(model) },
      output_schema: buildSchema(model),
      context: { terms: model.terms.slice(0, 120) },
      fields: model.fields.map(function (f) { return { key: f.key, kind: f.kind, desc: f.desc }; })
    };
  }

  /* ============================================================
     칸 ↔ DOM
     ============================================================ */
  function nodeOf(spec) { return global.OBForms.bound(spec); }
  function choiceInputs(spec) {
    var node = nodeOf(spec);
    return node ? Array.prototype.slice.call(node.querySelectorAll('input[type=radio],input[type=checkbox]')) : [];
  }
  function setChoice(input, on) {
    input.checked = !!on;
    var wrap = input.closest('.opt');
    if (wrap) wrap.classList.toggle('on', !!on);
  }
  function gridInputs(block) {
    var table = nodeOf(block);
    if (!table) return [];
    return Array.prototype.slice.call(table.querySelectorAll('tr')).slice(1).map(function (tr) {
      return Array.prototype.slice.call(tr.querySelectorAll('td input'));
    });
  }
  /* 칸의 입력 요소들(강조 · 손으로 고친 칸 추적용) */
  function elementsOf(field) {
    switch (field.kind) {
      case 'radio': case 'checks': case 'bool':
        return choiceInputs(field.spec);
      case 'grid':
        return [].concat.apply([], gridInputs(field.spec));
      default: {
        var node = nodeOf(field.spec);
        if (field.part === 'other') node = node && node._other;
        return node ? [node] : [];
      }
    }
  }

  function clearForm(model, host) {
    model.clearables.forEach(function (item) {
      if (item.grid) {
        gridInputs(item.grid).forEach(function (row) { row.forEach(function (cell) { cell.value = ''; }); });
        return;
      }
      var node = nodeOf(item.spec);
      if (!node) return;
      if (item.spec.c === 'r' || item.spec.c === 'k') {
        choiceInputs(item.spec).forEach(function (input) { setChoice(input, false); });
        if (node._other) node._other.value = '';
      } else if (item.spec.c === 's') {
        node.selectedIndex = -1;
      } else {
        node.value = '';
      }
    });
    var written = host.querySelector('.fm-titlebar input[type=date]');
    if (written) written.value = today();
  }

  /* ============================================================
     녹음 막대(UI)
     ============================================================ */
  var current = null;   // 지금 붙어 있는 서식의 상태

  function el(tag, cls, text) {
    var n = document.createElement(tag);
    if (cls) n.className = cls;
    if (text != null) n.textContent = text;
    return n;
  }
  function button(cls, text, title) {
    var b = el('button', 'vf-btn ' + cls, text);
    b.type = 'button';
    if (title) b.title = title;
    return b;
  }

  function attach(host, form) {
    detach();
    var fm = host.querySelector('.fm');
    if (!fm) return;
    var model = buildModel(form);
    var state = current = {
      host: host, form: form, model: model, fm: fm, phase: 'idle',
      byKey: {}, dirty: {}, voiceSet: {}, elementKey: new WeakMap(),
      segments: [], partial: [], filled: 0, revision: 0,
      ws: null, audio: null, stream: null, timer: null, keepalive: null, queue: [],
      seconds: 0, startedAt: 0, lastScroll: 0, ended: false
    };
    model.fields.forEach(function (f) { state.byKey[f.key] = f; });
    indexElements(state);

    var bar = state.bar = el('div', 'vf-bar');
    var row = el('div', 'vf-row');
    state.btnRec = button('vf-rec', '● 녹음', '말한 내용으로 이 서식을 채웁니다');
    state.btnPause = button('vf-pause', 'Ⅱ 일시정지');
    state.btnStop = button('vf-stop', '■ 정지');
    state.clock = el('span', 'vf-clock', '00:00');
    state.meter = el('span', 'vf-meter');
    state.meter.appendChild(el('i'));
    state.status = el('span', 'vf-status', '녹음을 누르고 말하면 서식이 채워집니다');
    state.status.setAttribute('role', 'status');
    var spacer = el('span', 'vf-spacer');
    var followWrap = el('label', 'vf-follow');
    state.follow = document.createElement('input');
    state.follow.type = 'checkbox';
    state.follow.checked = setting(LS_FOLLOW) !== 'off';
    state.follow.addEventListener('change', function () { setting(LS_FOLLOW, state.follow.checked ? 'on' : 'off'); });
    followWrap.appendChild(state.follow);
    followWrap.appendChild(el('span', null, '채운 칸 따라가기'));
    state.btnScript = button('vf-light', '말하기 예시');
    state.btnReset = button('vf-light', '예시 값으로 되돌리기');
    state.btnSetup = button('vf-light', '설정');
    [state.btnRec, state.btnPause, state.btnStop, state.clock, state.meter, state.status, spacer,
      followWrap, state.btnScript, state.btnReset, state.btnSetup].forEach(function (n) { row.appendChild(n); });
    bar.appendChild(row);

    state.scriptBox = el('div', 'vf-script');
    var script = (global.VOICE_FILL_SCRIPTS || {})[form.id];
    state.scriptBox.appendChild(el('div', 'vf-script-head', '말하기 예시 — 그대로 읽으면 해당 칸이 채워집니다 (가상의 내용)'));
    state.scriptBox.appendChild(el('div', 'vf-script-body', script || '이 서식의 예시 대본이 없습니다.'));
    bar.appendChild(state.scriptBox);

    state.transcript = el('div', 'vf-transcript');
    state.transcript.setAttribute('aria-label', '실시간 전사');
    bar.appendChild(state.transcript);

    fm.insertBefore(bar, fm.firstChild);
    fm.classList.add('vf-on');
    if (global.ResizeObserver) {
      state.resize = new ResizeObserver(function () { fm.style.setProperty('--vf-h', bar.offsetHeight + 'px'); });
      state.resize.observe(bar);
    }
    fm.style.setProperty('--vf-h', bar.offsetHeight + 'px');

    state.btnRec.addEventListener('click', function () { start(state); });
    state.btnStop.addEventListener('click', function () { finish(state); });
    state.btnPause.addEventListener('click', function () { togglePause(state); });
    state.btnScript.addEventListener('click', function () {
      state.scriptBox.classList.toggle('on');
      state.btnScript.classList.toggle('pressed', state.scriptBox.classList.contains('on'));
    });
    state.btnReset.addEventListener('click', function () {
      if (state.phase !== 'idle' && state.phase !== 'done' && state.phase !== 'error') return;
      var scriptOpen = state.scriptBox.classList.contains('on');
      global.OBForms.render(host, form);
      attach(host, form);
      if (scriptOpen) current.btnScript.click();
      setStatus(current, '예시 값으로 되돌렸습니다');
    });
    state.btnSetup.addEventListener('click', function () { openSetup(state); });

    state.onEdit = function (event) {
      if (!event.isTrusted) return;
      if (state.phase === 'idle' || state.phase === 'done' || state.phase === 'error') return;
      var id = state.elementKey.get(event.target);
      if (id) { state.dirty[id] = true; event.target.classList.remove('vf-filled'); }
    };
    host.addEventListener('input', state.onEdit, true);
    host.addEventListener('change', state.onEdit, true);
    state.onScroll = function () { if (!state.autoScrolling) state.lastScroll = Date.now(); };
    host.addEventListener('wheel', state.onScroll, { passive: true });
    host.addEventListener('touchmove', state.onScroll, { passive: true });
    render(state);
  }

  /* 입력 요소 → 칸 식별자(표는 칸마다). 손으로 고친 칸을 알아보는 데 쓴다 */
  function indexElements(state) {
    state.model.fields.forEach(function (f) {
      if (f.kind === 'grid') {
        gridInputs(f.spec).forEach(function (cells, r) {
          cells.forEach(function (cell, c) { state.elementKey.set(cell, f.key + ':' + r + ':' + c); });
        });
      } else {
        elementsOf(f).forEach(function (node) { state.elementKey.set(node, f.key); });
      }
    });
  }

  function detach() {
    var state = current;
    if (!state) return;
    current = null;
    abort(state);
    state.host.removeEventListener('input', state.onEdit, true);
    state.host.removeEventListener('change', state.onEdit, true);
    state.host.removeEventListener('wheel', state.onScroll);
    state.host.removeEventListener('touchmove', state.onScroll);
    if (state.resize) state.resize.disconnect();
    if (state.bar && state.bar.parentNode) state.bar.parentNode.removeChild(state.bar);
    state.fm.classList.remove('vf-on');
    closeDialog();
  }

  var PHASE_TEXT = { connecting: '연결 중…', listening: '듣는 중', paused: '일시정지', finishing: '마무리 중…' };
  function render(state) {
    var p = state.phase;
    var live = p === 'listening' || p === 'paused';
    state.bar.dataset.phase = p;
    state.btnRec.disabled = !(p === 'idle' || p === 'done' || p === 'error');
    state.btnRec.textContent = (p === 'done' || p === 'error') ? '● 다시 녹음' : '● 녹음';
    state.btnPause.disabled = !live;
    state.btnPause.textContent = p === 'paused' ? '▶ 이어서' : 'Ⅱ 일시정지';
    state.btnStop.disabled = !(live || p === 'connecting');
    state.btnReset.disabled = !(p === 'idle' || p === 'done' || p === 'error');
    state.btnSetup.disabled = !(p === 'idle' || p === 'done' || p === 'error');
    if (PHASE_TEXT[p]) {
      var extra = '';
      if (p === 'listening' || p === 'finishing') {
        if (state.templatePending) extra = ' · 서식 준비 중';
        else if (state.filled) extra = ' · 채운 칸 ' + state.filled + '개';
        else if (state.segments.length) extra = ' · 서식 채우는 중';
      }
      if (p === 'finishing') extra = ' 서식을 마저 채우고 있습니다' + (state.filled ? ' (채운 칸 ' + state.filled + '개)' : '');
      state.status.textContent = PHASE_TEXT[p] + extra;
      state.status.classList.remove('err');
    }
  }
  function setStatus(state, text, isError) {
    state.status.textContent = text;
    state.status.classList.toggle('err', !!isError);
  }
  function fail(state, text) {
    abort(state);
    state.phase = 'error';
    render(state);
    setStatus(state, text, true);
  }

  function renderTranscript(state) {
    var box = state.transcript;
    box.textContent = '';
    if (!state.segments.length && !state.partial.length) { box.classList.remove('on'); return; }
    box.classList.add('on');
    var many = {};
    state.segments.concat(state.partial).forEach(function (s) { if (s.speaker) many[s.speaker] = 1; });
    var showSpeaker = Object.keys(many).length > 1;
    function piece(seg, cls) {
      var span = el('span', cls);
      if (showSpeaker && seg.speaker) span.appendChild(el('b', 'vf-spk', String(seg.speaker).replace('speaker_', '화자 ') + ' '));
      span.appendChild(document.createTextNode(seg.text + ' '));
      box.appendChild(span);
    }
    state.segments.forEach(function (s) { piece(s, 'vf-final'); });
    state.partial.forEach(function (s) { piece(s, 'vf-partial'); });
    box.scrollTop = box.scrollHeight;
  }

  function tick(state) {
    var s = Math.floor(state.seconds + (state.phase === 'listening' ? (Date.now() - state.startedAt) / 1000 : 0));
    state.clock.textContent = String(Math.floor(s / 60)).padStart(2, '0') + ':' + String(s % 60).padStart(2, '0');
  }

  /* ============================================================
     대화상자(접속 코드 · 설정)
     ============================================================ */
  var dialog = null;
  function closeDialog() {
    if (dialog && dialog.parentNode) dialog.parentNode.removeChild(dialog);
    dialog = null;
  }
  function openDialog(title, build, onOk) {
    closeDialog();
    dialog = el('div', 'vf-modal');
    var box = el('form', 'vf-dialog');
    box.appendChild(el('div', 'vf-dialog-title', title));
    var body = el('div', 'vf-dialog-body');
    var message = el('div', 'vf-dialog-msg');
    var inputs = build(body);
    body.appendChild(message);
    box.appendChild(body);
    var foot = el('div', 'vf-dialog-foot');
    var ok = button('vf-ok', '확인');
    ok.type = 'submit';
    var cancel = button('vf-light', '취소');
    foot.appendChild(ok);
    foot.appendChild(cancel);
    box.appendChild(foot);
    dialog.appendChild(box);
    document.body.appendChild(dialog);
    cancel.addEventListener('click', closeDialog);
    box.addEventListener('submit', function (event) {
      event.preventDefault();
      ok.disabled = true;
      message.textContent = '확인 중…';
      message.classList.remove('err');
      Promise.resolve(onOk(inputs)).then(function (error) {
        ok.disabled = false;
        if (error) { message.textContent = error; message.classList.add('err'); }
        else closeDialog();
      });
    });
    dialog.addEventListener('keydown', function (event) { if (event.key === 'Escape') closeDialog(); });
    return { message: message, inputs: inputs };
  }
  function field(body, label, type, value, placeholder) {
    var wrap = el('label', 'vf-field');
    wrap.appendChild(el('span', null, label));
    var input = document.createElement('input');
    input.type = type;
    input.value = value || '';
    if (placeholder) input.placeholder = placeholder;
    input.autocomplete = 'off';
    wrap.appendChild(input);
    body.appendChild(wrap);
    return input;
  }

  /* 접속 코드를 서버에 확인한다. 반환: null(통과) 또는 한국어 오류 한 줄 */
  function checkAccess(code) {
    var abort = global.AbortController ? new AbortController() : null;
    var timer = setTimeout(function () { if (abort) abort.abort(); }, 8000);   // 막힌 망에서는 응답 없이 매달린다
    return fetch(apiBase() + '/v1/access', {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ access_code: code }), signal: abort ? abort.signal : undefined
    }).then(function (res) {
      clearTimeout(timer);
      if (res.ok) return null;
      if (res.status === 401) return '접속 코드가 맞지 않습니다.';
      if (res.status === 429) return '접속 코드를 너무 여러 번 틀렸습니다. 1분 뒤 다시 시도해 주세요.';
      return '중계 서버가 요청을 받지 못했습니다(HTTP ' + res.status + ').';
    }, function () {
      clearTimeout(timer);
      return '중계 서버에 연결하지 못했습니다. 네트워크와 설정의 서버 주소를 확인해 주세요.';
    });
  }
  function askCode(note) {
    return new Promise(function (resolve) {
      var done = false;
      var opened = openDialog('음성 입력 접속 코드', function (body) {
        body.appendChild(el('div', 'vf-dialog-note', '시연용 접속 코드를 입력하세요. 이 브라우저에 저장되어 다음부터는 묻지 않습니다.'));
        return { code: field(body, '접속 코드', 'password', '') };
      }, function (inputs) {
        var code = inputs.code.value.trim();
        if (!code) return '접속 코드를 입력해 주세요.';
        return checkAccess(code).then(function (error) {
          if (!error) { setting(LS_CODE, code); done = true; resolve(code); }
          return error;
        });
      });
      if (note) { opened.message.textContent = note; opened.message.classList.add('err'); }
      opened.inputs.code.focus();
      var watcher = setInterval(function () {
        if (!dialog) { clearInterval(watcher); if (!done) resolve(null); }
      }, 200);
    });
  }
  function openSetup(state) {
    var opened = openDialog('음성 입력 설정', function (body) {
      var api = field(body, '중계 서버 주소', 'text', setting(LS_API), DEFAULT_API);
      var code = field(body, '접속 코드', 'password', setting(LS_CODE));
      body.appendChild(el('div', 'vf-dialog-note', '서버 주소를 비우면 기본 주소를 씁니다. 주소창의 ?voiceApi= 는 localhost 주소일 때만 쓰고, 있으면 그 값이 먼저입니다.'));
      return { api: api, code: code };
    }, function (inputs) {
      var api = inputs.api.value.trim().replace(/\/+$/, '');
      if (api && !/^https?:\/\//.test(api)) return '서버 주소는 http:// 또는 https:// 로 시작해야 합니다.';
      setting(LS_API, api || null);
      var code = inputs.code.value.trim();
      if (!code) { setting(LS_CODE, null); setStatus(state, '설정을 저장했습니다'); return null; }
      return checkAccess(code).then(function (error) {
        if (!error) { setting(LS_CODE, code); setStatus(state, '설정을 저장했습니다'); }
        return error;
      });
    });
    opened.inputs.api.focus();
  }

  /* ============================================================
     녹음 세션
     ============================================================ */
  var CLOSE_TEXT = {
    4001: '접속 코드가 맞지 않거나 허용되지 않은 화면입니다.',
    4002: '한동안 소리가 없어 세션이 끝났습니다.',
    4003: '세션 시간 상한에 닿아 녹음이 끝났습니다.',
    4004: '요청 형식이 맞지 않아 연결이 닫혔습니다.',
    4006: '지금 다른 시연이 녹음 중입니다(동시 세션 상한). 잠시 뒤 다시 시도해 주세요.',
    4007: '이 서식 정의를 서버가 받아들이지 않았습니다.',
    1011: '서버 오류로 연결이 끊겼습니다. 다시 시도해 주세요.'
  };

  function start(state) {
    if (!(state.phase === 'idle' || state.phase === 'done' || state.phase === 'error')) return;
    if (!global.isSecureContext || !navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
      setStatus(state, '마이크는 https 또는 localhost 에서만 쓸 수 있습니다.', true);
      return;
    }
    state.phase = 'connecting';
    state.ended = false;
    render(state);
    var code = setting(LS_CODE);
    var gate = code
      ? checkAccess(code).then(function (error) {
        if (!error) return code;
        if (error.indexOf('접속 코드가 맞지') === 0) return askCode(error);
        throw new Error(error);
      })
      : askCode();
    gate.then(function (okCode) {
      if (state !== current || state.phase !== 'connecting') return null;
      if (!okCode) { state.phase = 'idle'; render(state); setStatus(state, '접속 코드를 입력해야 녹음할 수 있습니다.'); return null; }
      return openMic(state).then(function () { return okCode; });
    }).then(function (okCode) {
      if (!okCode || state !== current || state.phase !== 'connecting') { if (okCode) stopMic(state); return; }
      beginSession(state, okCode);
    }).catch(function (error) {
      if (state !== current) return;
      var name = error && error.name;
      if (name === 'NotAllowedError' || name === 'SecurityError') {
        fail(state, '마이크 권한이 거부되었습니다. 주소창의 마이크 설정에서 허용해 주세요.');
      } else if (name === 'NotFoundError' || name === 'OverconstrainedError') {
        fail(state, '쓸 수 있는 마이크를 찾지 못했습니다.');
      } else {
        fail(state, (error && error.message) || '녹음을 시작하지 못했습니다.');
      }
    });
  }

  function openMic(state) {
    return navigator.mediaDevices.getUserMedia({
      audio: { channelCount: 1, echoCancellation: true, noiseSuppression: true, autoGainControl: true }
    }).then(function (stream) {
      state.stream = stream;
      var Ctx = global.AudioContext || global.webkitAudioContext;
      var ctx;
      try { ctx = new Ctx({ sampleRate: 16000, latencyHint: 'interactive' }); }
      catch (e) { ctx = new Ctx(); }
      state.audio = ctx;
      return ctx.audioWorklet.addModule(new URL('voice-fill-worklet.js', scriptBase).href).then(function () {
        var source = ctx.createMediaStreamSource(stream);
        var node = new AudioWorkletNode(ctx, 'voice-fill-capture', { numberOfInputs: 1, numberOfOutputs: 0 });
        node.port.onmessage = function (event) { onAudio(state, event.data); };
        source.connect(node);
        state.capture = node;
        return ctx.resume();
      });
    });
  }
  var scriptBase = (document.currentScript && document.currentScript.src) || global.location.href;

  function stopMic(state) {
    if (state.capture) { try { state.capture.port.postMessage('flush'); state.capture.disconnect(); } catch (e) { /* 무시 */ } }
    if (state.stream) state.stream.getTracks().forEach(function (track) { track.stop(); });
    if (state.audio) { try { state.audio.close(); } catch (e) { /* 무시 */ } }
    state.capture = state.stream = state.audio = null;
    state.meter.firstChild.style.width = '0';
  }

  function onAudio(state, data) {
    if (state.phase === 'listening' || state.phase === 'connecting') {
      state.meter.firstChild.style.width = Math.min(100, Math.round(Math.sqrt(data.peak) * 100)) + '%';
    }
    if (state.phase === 'paused' || state.micClosed) return;
    var ws = state.ws;
    if (ws && ws.readyState === 1 && state.ready) {
      ws.send(data.pcm);
      state.sentBytes = (state.sentBytes || 0) + data.pcm.byteLength;
    } else if (state.queue.length < 100) {
      state.queue.push(data.pcm);          // ready 전 최대 10초분을 모아 둔다 — 첫 마디를 잃지 않게
    }
  }

  function beginSession(state, code) {
    var model = state.model;
    // 빈 서식에서 시작한다
    clearForm(model, state.host);
    state.dirty = {}; state.voiceSet = {}; state.segments = []; state.partial = [];
    state.filled = 0; state.revision = 0; state.seconds = 0; state.ready = false; state.queue = [];
    state.sentBytes = 0; state.templatePending = false; state.gotFinal = false; state.endInfo = null;
    state.micClosed = false;
    state.host.querySelectorAll('.vf-filled,.vf-flash').forEach(function (n) { n.classList.remove('vf-filled', 'vf-flash'); });
    renderTranscript(state);

    var ws;
    try { ws = new WebSocket(wsUrl('/v1/session')); }
    catch (e) { fail(state, '중계 서버 주소가 올바르지 않습니다. 설정을 확인해 주세요.'); return; }
    ws.binaryType = 'arraybuffer';
    state.ws = ws;
    state.readyTimer = setTimeout(function () {
      if (state.ws === ws && !state.ready) fail(state, '중계 서버가 응답하지 않습니다. 잠시 뒤 다시 시도해 주세요.');
    }, 20000);
    ws.onopen = function () {
      var info = describe(state.form);
      ws.send(JSON.stringify({
        type: 'config', access_code: code, form: info.form, output_schema: info.output_schema,
        audio_format: 'pcm_s16le', sample_rate: 16000, channels: 1, language_hints: ['ko', 'en'],
        context: info.context, refresh_interval_seconds: REFRESH_SECONDS
      }));
    };
    ws.onmessage = function (event) {
      if (state.ws !== ws || typeof event.data !== 'string') return;
      var message;
      try { message = JSON.parse(event.data); } catch (e) { return; }
      onMessage(state, message);
    };
    ws.onclose = function (event) { if (state.ws === ws) onClose(state, event); };
  }

  function onMessage(state, message) {
    switch (message.type) {
      case 'ready':
        clearTimeout(state.readyTimer);
        state.ready = true;
        state.templatePending = !(message.template && message.template.id);
        state.phase = 'listening';
        state.startedAt = Date.now();
        state.timer = setInterval(function () { tick(state); }, 250);
        state.queue.splice(0).forEach(function (pcm) { state.ws.send(pcm); state.sentBytes += pcm.byteLength; });
        render(state);
        break;
      case 'template_ready':
        state.templatePending = false;
        render(state);
        break;
      case 'transcript':
        (message.segments || []).forEach(function (s) { state.segments.push(s); });
        state.partial = message.partial || [];
        renderTranscript(state);
        render(state);
        break;
      case 'document':
        onDocument(state, message);
        break;
      case 'session_expiring':
        setStatus(state, '세션 시간이 곧 끝납니다. 정지를 눌러 마무리해 주세요.');
        break;
      case 'session_end':
        state.endInfo = message;
        // 마지막 메시지다. 서버가 곧 닫지만, 닫힘이 늦으면 화면 쪽에서 닫아 마무리한다
        setTimeout(function () {
          if (state.ws && state.endInfo === message) { try { state.ws.close(1000); } catch (e) { /* 무시 */ } }
        }, 3000);
        break;
      default: break;   // 모르는 프레임은 무시한다(계약: 키·프레임이 늘 수 있다)
    }
  }

  function onDocument(state, doc) {
    state.revision = doc.revision || state.revision + 1;
    if (doc.final) state.gotFinal = true;
    if (doc.status !== 'completed' || !doc.output || doc.output.type !== 'json' || !doc.output.value) {
      if (doc.final) state.finalError = (doc.error && doc.error.code) || doc.status;
      render(state);
      return;
    }
    state.templatePending = false;
    var value = doc.output.value;
    var keys = Array.isArray(doc.changed) ? doc.changed : Object.keys(value);
    var touched = applyValues(state, value, keys);
    state.filled = Object.keys(state.voiceSet).length;
    render(state);
    if (touched.length && state.follow.checked && Date.now() - state.lastScroll > 4000) scrollTo(state, touched[0]);
  }

  function scrollTo(state, node) {
    var host = state.host;
    var top = node.getBoundingClientRect().top - host.getBoundingClientRect().top;
    var barH = state.bar.offsetHeight + 40;
    if (top > barH && top < host.clientHeight - 30) return;       // 이미 보인다
    state.autoScrolling = true;
    host.scrollTo({ top: host.scrollTop + top - Math.round(host.clientHeight * 0.4), behavior: 'smooth' });
    setTimeout(function () { state.autoScrolling = false; }, 600);
  }

  function mark(state, node) {
    var target = node.closest('.opt') || node;
    target.classList.add('vf-filled');
    target.classList.remove('vf-flash');
    void target.offsetWidth;                // 애니메이션 다시 시작
    target.classList.add('vf-flash');
    return target;
  }
  function unmark(node) {
    (node.closest('.opt') || node).classList.remove('vf-filled', 'vf-flash');
  }

  /* 서식 결과(JSON)를 칸에 넣는다. 손으로 고친 칸은 건드리지 않고, null 은 음성이 채웠던 칸만 비운다 */
  function applyValues(state, value, keys) {
    var touched = [];
    keys.forEach(function (key) {
      var f = state.byKey[key];
      if (!f || !(key in value)) return;
      var v = value[key];
      if (f.kind === 'grid') { applyGrid(state, f, v, touched); return; }
      if (state.dirty[key]) return;
      if (v === null || v === undefined || v === '' || (Array.isArray(v) && !v.length)) {
        if (!state.voiceSet[key]) return;
        delete state.voiceSet[key];
        elementsOf(f).forEach(function (node) {
          if (f.kind === 'radio' || f.kind === 'checks' || f.kind === 'bool') setChoice(node, false);
          else if (f.kind === 'select') node.selectedIndex = -1;
          else node.value = '';
          unmark(node);
        });
        return;
      }
      var nodes = elementsOf(f);
      switch (f.kind) {
        case 'radio': {
          var hit = nodes.filter(function (n) { return n.value === v; })[0];
          if (!hit) return;
          nodes.forEach(function (n) { setChoice(n, n === hit); unmark(n); });
          touched.push(mark(state, hit));
          break;
        }
        case 'checks': {
          var any = false;
          nodes.forEach(function (n) {
            var on = v.indexOf(n.value) !== -1;
            setChoice(n, on);
            if (on) { touched.push(mark(state, n)); any = true; } else unmark(n);
          });
          if (!any) return;
          break;
        }
        case 'bool': {
          var box = nodes[0];
          if (!box) return;
          if (v !== true && !state.voiceSet[key]) return;        // false 는 "체크 안 함" — 빈 칸 그대로
          setChoice(box, v === true);
          if (v === true) touched.push(mark(state, box));
          else { unmark(box); delete state.voiceSet[key]; return; }
          break;
        }
        case 'select': {
          var select = nodes[0];
          var index = f.options.indexOf(v);
          if (!select || index === -1) return;
          if (select.selectedIndex === index && state.voiceSet[key]) return;
          select.selectedIndex = index;
          touched.push(mark(state, select));
          break;
        }
        case 'date': {
          var date = nodes[0];
          if (!date || !/^\d{4}-\d{2}-\d{2}$/.test(String(v))) return;
          if (date.value === v) { state.voiceSet[key] = true; return; }
          date.value = v;
          touched.push(mark(state, date));
          break;
        }
        default: {
          var input = nodes[0];
          if (!input) return;
          var text = String(v);
          if (input.value === text) { state.voiceSet[key] = true; return; }
          input.value = text;
          touched.push(mark(state, input));
        }
      }
      state.voiceSet[key] = true;
    });
    computeRatios(state);
    return touched;
  }

  function applyGrid(state, f, rows, touched) {
    var cells = gridInputs(f.spec);
    var list = Array.isArray(rows) ? rows : [];
    var any = false;
    cells.forEach(function (rowCells, r) {
      var item = list[r] || {};
      rowCells.forEach(function (cell, c) {
        if (state.dirty[f.key + ':' + r + ':' + c]) return;
        var raw = item[f.columns[c].key];
        var text = raw == null ? '' : String(raw);
        if (text) any = true;
        if (cell.value === text) return;
        if (!text && !cell.classList.contains('vf-filled')) return;
        cell.value = text;
        if (text) touched.push(mark(state, cell)); else unmark(cell);
      });
    });
    if (any) state.voiceSet[f.key] = true; else delete state.voiceSet[f.key];
  }

  /* 같은 행의 "a / b = (자동 계산)" 칸 */
  function computeRatios(state) {
    state.model.ratios.forEach(function (ratio) {
      var a = nodeOf(ratio.a.spec), b = nodeOf(ratio.b.spec), target = nodeOf(ratio.target);
      if (!a || !b || !target) return;
      var x = parseFloat(a.value), y = parseFloat(b.value);
      target.value = (isFinite(x) && isFinite(y) && y !== 0) ? (x / y).toFixed(2) : '';
    });
  }

  function togglePause(state) {
    if (state.phase === 'listening') {
      state.seconds += (Date.now() - state.startedAt) / 1000;
      state.phase = 'paused';
      state.keepalive = setInterval(function () {
        if (state.ws && state.ws.readyState === 1) state.ws.send(JSON.stringify({ type: 'keepalive' }));
      }, 10000);
      if (state.ws && state.ws.readyState === 1) state.ws.send(JSON.stringify({ type: 'refresh' }));
    } else if (state.phase === 'paused') {
      clearInterval(state.keepalive);
      state.startedAt = Date.now();
      state.phase = 'listening';
    }
    render(state);
  }

  function finish(state) {
    if (state.phase === 'connecting') { abort(state); state.phase = 'idle'; render(state); setStatus(state, '녹음을 취소했습니다'); return; }
    if (state.phase !== 'listening' && state.phase !== 'paused') return;
    if (state.phase === 'listening') state.seconds += (Date.now() - state.startedAt) / 1000;
    clearInterval(state.keepalive);
    clearInterval(state.timer);
    if (state.capture) { try { state.capture.port.postMessage('flush'); } catch (e) { /* 무시 */ } }
    state.phase = 'finishing';
    render(state);
    // 워클릿에 남은 마지막 조각이 넘어온 뒤 finish 를 보낸다
    setTimeout(function () {
      stopMic(state);
      state.micClosed = true;
      if (state.ws && state.ws.readyState === 1) state.ws.send(JSON.stringify({ type: 'finish' }));
      state.finishTimer = setTimeout(function () {
        if (state.phase === 'finishing') fail(state, '마무리 응답이 오지 않아 연결을 닫았습니다. 채워진 칸까지는 화면에 남아 있습니다.');
      }, 90000);
    }, 150);
  }

  function onClose(state, event) {
    var end = state.endInfo;
    var wasFinishing = state.phase === 'finishing';
    var before = state.phase;
    clearTimeout(state.finishTimer);
    abort(state);
    state.partial = [];
    renderTranscript(state);
    if (end && end.status === 'completed') {
      state.phase = 'done';
      render(state);
      setStatus(state, state.filled ? '완료 — 음성으로 채운 칸 ' + state.filled + '개. 내용을 확인한 뒤 저장하세요.'
        : '완료 — 채울 내용을 찾지 못했습니다. 말하기 예시를 참고해 다시 녹음해 보세요.');
      return;
    }
    var text;
    if (end && end.status === 'incomplete') {
      var reason = end.incomplete_details && end.incomplete_details.reason;
      text = reason === 'duration_limit' ? CLOSE_TEXT[4003] : reason === 'idle_timeout' ? CLOSE_TEXT[4002]
        : '전사 확정이 끝나기 전에 세션이 닫혔습니다. 채워진 칸까지는 화면에 남아 있습니다.';
    } else if (end && end.status === 'failed') {
      text = end.error && end.error.code === 'generation_failed'
        ? '서식 결과를 만들지 못했습니다. 잠시 뒤 다시 녹음해 주세요.'
        : (CLOSE_TEXT[event.code] || '세션이 오류로 끝났습니다(' + ((end.error && end.error.code) || event.code) + ').');
    } else if (CLOSE_TEXT[event.code]) {
      text = CLOSE_TEXT[event.code];
    } else if (before === 'connecting') {
      text = '중계 서버에 연결하지 못했습니다. 네트워크와 설정의 서버 주소를 확인해 주세요.';
    } else {
      text = wasFinishing ? '마무리 중에 연결이 끊겼습니다. 채워진 칸까지는 화면에 남아 있습니다.'
        : '연결이 끊겼습니다. 채워진 칸까지는 화면에 남아 있습니다.';
    }
    if (event.code === 4001) setting(LS_CODE, null);
    state.phase = 'error';
    render(state);
    setStatus(state, text, true);
  }

  /* 세션 자원을 모두 놓는다(화면 상태는 그대로) */
  function abort(state) {
    clearInterval(state.timer);
    clearInterval(state.keepalive);
    clearTimeout(state.finishTimer);
    clearTimeout(state.readyTimer);
    stopMic(state);
    var ws = state.ws;
    state.ws = null;
    state.ready = false;
    state.queue = [];
    if (ws && ws.readyState <= 1) { try { ws.close(1000); } catch (e) { /* 무시 */ } }
  }

  global.addEventListener('pagehide', function () { if (current) abort(current); });

  global.VoiceFill = {
    attach: attach,
    detach: detach,
    describe: describe,
    /* 시험용: 지금 열린 서식을 비우고, 결과 JSON 을 그대로 넣어 본다 */
    clear: function () { if (current) clearForm(current.model, current.host); },
    fill: function (value, changed) {
      if (!current) return 0;
      return applyValues(current, value, changed || Object.keys(value)).length;
    },
    state: function () {
      return current ? { phase: current.phase, filled: current.filled, revision: current.revision,
        segments: current.segments.length, status: current.status.textContent,
        sentBytes: current.sentBytes || 0, seconds: current.seconds } : null;
    }
  };
})(window);
