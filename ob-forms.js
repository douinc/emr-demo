/* ============================================================
   산과(OB) 구조화 서식지 - 스키마 헬퍼 & 렌더러
   window.OBForms.render(container, form) 로 서식지를 그린다.
   ============================================================ */
(function (global) {
  'use strict';

  /* ---------------- 컨트롤 단축 생성기 ---------------- */
  function t(w, o)   { return Object.assign({ c: 't', w: w || 90 }, o); }
  function d(val)    { return { c: 'd', val: val || '' }; }
  function ta(h, o)  { return Object.assign({ c: 'ta', h: h || 54 }, o); }
  function r(opts, val, o) { return Object.assign({ c: 'r', opts: opts, val: val }, o); }
  function k(opts, val, o) { return Object.assign({ c: 'k', opts: opts, val: val || [] }, o); }
  function sel(opts, val, w) { return { c: 's', opts: opts, val: val, w: w }; }
  function btn(label, light) { return { c: 'b', label: label, light: !!light }; }
  function lbl(text) { return { c: 'lbl', text: text }; }
  function br()      { return { c: 'br' }; }

  /* ---------------- 블록 단축 생성기 ---------------- */
  var S = {
    sec:  function (label) { return { type: 'sec', label: label }; },
    sub:  function (label) { return { type: 'sub', label: label }; },
    hr:   function () { return { type: 'hr' }; },
    note: function (text) { return { type: 'note', text: text }; },
    stat: function (text) { return { type: 'static', text: text }; },
    row:  function (label, items, indent) {
      return { type: 'row', label: label, items: items || [], indent: indent || 0 };
    },
    grid: function (o) { return Object.assign({ type: 'grid' }, o); },
    set:  function (label, rows) { return { type: 'set', label: label, rows: rows }; },
    draw: function (label) { return { type: 'draw', label: label }; }
  };

  /* ---------------- 반복 패턴 헬퍼 ---------------- */
  /* Normal / Not checked / Abnormal 3분기 (초음파 서식 전반에서 반복) */
  function nna(label, val, indent, extra) {
    var items = [r(['Normal', 'Not checked', 'Abnormal'], val || 'Normal'), t(160, { fill: true })];
    return S.row(label, extra ? items.concat(extra) : items, indent);
  }
  /* 직원 선택 필드 */
  function staff(label, val) {
    return S.row(label, [t(150, { val: val || '', hl: true }), btn('직원선택')]);
  }
  /* 기록자명 / 판독의 / 전문의 */
  function signBlock(val) {
    return [{ type: 'signstart' }, staff('기록자명', val), staff('판독의', val),
            staff('전문의', val), { type: 'signend' }];
  }
  /* 검사일 / 분만예정일 헤더 */
  function sonoHeader(exam, edcLnmp, edcSono) {
    return [S.row('Date of examination', [d(exam)]),
            S.row('EDC ( by LNMP )', [d(edcLnmp)]),
            S.row('EDC ( by sono )', [d(edcSono)])];
  }
  function imageQuality(val) {
    return S.row('Image quality', [r([
      'Poor Quality due to maternal obesity',
      'Poor Quality due to fetal position',
      'Moderate Quality',
      'Good Quality'
    ], val, { stack: true, other: 230 })]);
  }
  /* 태아 계측 1행 : [cm] [w] [d] */
  function biometry(label, cm, w, day) {
    return S.row(label, [t(66, { val: cm }), lbl('cm'), t(66, { val: w }), lbl('w'),
                         t(66, { val: day }), lbl('d')], 1);
  }
  /* 판독 소견 (Impression / Recommendation) */
  function impression(weeks, text) {
    return [S.row('Impression', [lbl('IUP'), t(44, { val: weeks }), lbl('weeks'),
                                 btn('서술문선택'), br(), ta(80, { val: text, w: 460 })]),
            S.row('Recommendation', [ta(72, { w: 460 })])];
  }
  /* 태반 위치 / 전치태반 / 유착태반 */
  function placentaBlock(site, previa) {
    return [
      S.sec('Placenta'),
      S.row('', [k(['Anterior', 'Posterior', 'Rt. lateral', 'Lt. lateral', 'Fundal'], site)], 1),
      S.row('Previa', [r(['Yes', 'No'], previa || 'No')], 1),
      S.row('', [lbl('-'), k(['low-lying', 'totalis', 'partialis', 'marginalis'])], 2),
      S.row('Internal os ~ placenta', [t(66), lbl('cm')], 2),
      S.row('Accreta', [k(['Definite', 'Likely', 'Less likely', 'No'], ['No'])], 2),
      S.row('Note', [ta(48, { w: 420 })], 2)
    ];
  }
  /* 모체 해부학적 소견 + 자궁경부 길이 */
  function maternalAnatomy(otherLabel) {
    return [
      S.sec('Maternal anatomy'),
      S.row('Uterus', [r(['not significant finding', 'significant finding'],
                         'not significant finding', { stack: true, tail: t(120, { fill: true }) })], 1),
      nna('Rt ovary', 'Not checked', 1),
      nna('Lt ovary', 'Not checked', 1),
      S.row('Cervix', [r(['not significant finding', 'significant finding'],
                         'not significant finding', { stack: true, tail: t(120, { fill: true }) })], 1),
      S.row(otherLabel || 'Other findings', [ta(48, { w: 420 })], 1),
      S.sec('Cervix length'),
      S.row('', [r(['checked', 'not checked'], 'not checked', { stack: true,
                   inlineFields: { checked: [t(66, { fill: true }), lbl('cm')] } })], 1),
      S.row('Funneling', [r(['No', 'Yes'], 'No', { tail: t(66, { fill: true }) })], 2)
    ];
  }

  /* ---------------- DOM 유틸 ---------------- */
  var seq = 0;
  function el(tag, cls, text) {
    var n = document.createElement(tag);
    if (cls) n.className = cls;
    if (text != null) n.textContent = text;
    return n;
  }
  function px(v) { return typeof v === 'number' ? v + 'px' : v; }

  function option(kind, name, text, checked) {
    var wrap = el('label', 'opt' + (checked ? ' on' : ''));
    var input = document.createElement('input');
    input.type = kind;
    input.name = name;
    input.value = text;
    input.checked = !!checked;
    input.addEventListener('change', function () {
      if (kind === 'radio') {
        Array.prototype.forEach.call(document.getElementsByName(name), function (sib) {
          var p = sib.closest('.opt');
          if (p) p.classList.toggle('on', sib.checked);
        });
      } else {
        wrap.classList.toggle('on', input.checked);
      }
    });
    wrap.appendChild(input);
    wrap.appendChild(el('span', null, text));
    return wrap;
  }

  /* ---------------- 컨트롤 렌더링 ---------------- */
  function renderCtrl(spec) {
    var node;
    switch (spec.c) {
      case 't': {
        node = document.createElement('input');
        node.type = 'text';
        node.style.width = px(spec.w);
        node.value = spec.val || '';
        if (spec.hl) node.classList.add('hl');
        if (spec.fill) node.classList.add('fill');
        if (spec.ro) node.readOnly = true;
        if (spec.ph) node.placeholder = spec.ph;
        break;
      }
      case 'd': {
        node = document.createElement('input');
        node.type = 'date';
        node.value = spec.val || '';
        break;
      }
      case 'ta': {
        node = document.createElement('textarea');
        node.rows = 2;
        node.style.height = px(spec.h);
        if (spec.w) node.style.width = px(spec.w);
        node.value = spec.val || '';
        break;
      }
      case 's': {
        node = document.createElement('select');
        if (spec.w) node.style.width = px(spec.w);
        spec.opts.forEach(function (o) {
          var op = el('option', null, o);
          op.value = o;
          if (o === spec.val) op.selected = true;
          node.appendChild(op);
        });
        break;
      }
      case 'b': {
        node = el('button', 'fbtn' + (spec.light ? ' light' : ''), spec.label);
        node.type = 'button';
        break;
      }
      case 'lbl': {
        node = el('span', 'unit', spec.text);
        break;
      }
      case 'br': {
        node = el('div');
        node.style.flexBasis = '100%';
        node.style.height = '0';
        break;
      }
      case 'r':
      case 'k': {
        node = el('span', 'optset');
        if (spec.stack) { node.style.display = 'flex'; node.style.flexDirection = 'column';
                          node.style.alignItems = 'flex-start'; node.style.gap = '1px'; }
        var name = 'g' + (++seq);
        var kind = spec.c === 'r' ? 'radio' : 'checkbox';
        var checkedList = spec.c === 'k' ? (spec.val || []) : [spec.val];
        spec.opts.forEach(function (o) {
          var line = spec.stack ? el('span', 'optline') : null;
          if (line) { line.style.display = 'inline-flex'; line.style.alignItems = 'center';
                      line.style.gap = '4px'; }
          var box = option(kind, name, o, checkedList.indexOf(o) !== -1);
          (line || node).appendChild(box);
          if (spec.inlineFields && spec.inlineFields[o]) {
            spec.inlineFields[o].forEach(function (f) { (line || node).appendChild(renderCtrl(f)); });
          }
          if (line) node.appendChild(line);
        });
        if (spec.other) {
          var oline = el('span', 'optline');
          oline.style.display = 'inline-flex';
          oline.style.alignItems = 'center';
          oline.style.gap = '4px';
          oline.appendChild(option(kind, name, spec.otherLabel || 'Other', false));
          oline.appendChild(renderCtrl(t(spec.other, { fill: true })));
          node.appendChild(oline);
        }
        if (spec.tail) node.appendChild(renderCtrl(spec.tail));
        break;
      }
      default:
        node = el('span', null, '');
    }
    if (spec.unit) {
      var wrap = el('span');
      wrap.style.display = 'inline-flex';
      wrap.style.alignItems = 'center';
      wrap.style.gap = '3px';
      wrap.appendChild(node);
      wrap.appendChild(el('span', 'unit', spec.unit));
      return wrap;
    }
    return node;
  }

  /* ---------------- 그리드(표) 렌더링 ---------------- */
  function renderGrid(block) {
    var box = el('div', 'fm-grid' + (block.peach ? ' peach' : ''));
    var head = el('div', 'fm-grid-head');
    head.appendChild(el('span', 'gt', block.title || ''));
    (block.btns || ['추가', '삭제']).forEach(function (b) {
      head.appendChild(renderCtrl(btn(b)));
    });
    box.appendChild(head);

    var wrap = el('div', 'fm-grid-wrap');
    var table = document.createElement('table');
    var hr = document.createElement('tr');
    hr.appendChild(el('th', null, ''));
    block.cols.forEach(function (c) {
      var th = el('th', null, c.h);
      if (c.w) th.style.width = px(c.w);
      hr.appendChild(th);
    });
    table.appendChild(hr);

    var data = block.data || [];
    var count = Math.max(block.rows || 0, data.length);
    for (var i = 0; i < count; i++) {
      var tr = document.createElement('tr');
      tr.appendChild(el('td', 'rn', String(i + 1)));
      for (var j = 0; j < block.cols.length; j++) {
        var td = el('td', block.peach ? 'v' : null);
        var input = document.createElement('input');
        input.type = 'text';
        input.value = (data[i] && data[i][j]) || '';
        td.appendChild(input);
        tr.appendChild(td);
      }
      table.appendChild(tr);
    }
    wrap.appendChild(table);
    box.appendChild(wrap);
    return box;
  }

  /* ---------------- 블록 렌더링 ---------------- */
  function renderBlock(block, form, host) {
    switch (block.type) {
      case 'sec':  host.appendChild(el('div', 'fm-sec', block.label)); return;
      case 'sub':  host.appendChild(el('div', 'fm-sub', block.label)); return;
      case 'hr':   host.appendChild(el('div', 'fm-hr')); return;
      case 'note': host.appendChild(el('div', 'fm-note', block.text)); return;
      case 'static': host.appendChild(el('div', 'fm-static', block.text)); return;
      case 'grid': host.appendChild(renderGrid(block)); return;
      case 'draw': {
        host.appendChild(el('div', 'fm-draw-label', block.label || 'Text'));
        host.appendChild(el('div', 'fm-draw', '[ 그림 작성 영역 - 데모에서는 비활성 ]'));
        return;
      }
      case 'set': {
        var setBox = el('div', 'fm-set');
        setBox.appendChild(el('div', 'fm-set-head', block.label));
        var body = el('div', 'fm-set-body');
        block.rows.forEach(function (b) { renderBlock(b, form, body); });
        setBox.appendChild(body);
        host.appendChild(setBox);
        return;
      }
      case 'signstart': {
        var sign = el('div', 'fm-sign');
        sign.dataset.sign = '1';
        host.appendChild(sign);
        return;
      }
      case 'signend': return;
      default: {
        var row = el('div', 'fm-row');
        var label = el('div', 'fm-label', block.label || '');
        label.style.width = px((form.labelW || 150) - (block.indent || 0) * 0);
        label.style.paddingLeft = px((block.indent || 0) * 14);
        row.appendChild(label);
        var ctrls = el('div', 'fm-ctrls');
        block.items.forEach(function (spec) { ctrls.appendChild(renderCtrl(spec)); });
        row.appendChild(ctrls);
        // 서명 블록이 열려 있으면 그 안에 넣는다
        var last = host.lastElementChild;
        if (last && last.dataset && last.dataset.sign === '1') { last.appendChild(row); return; }
        host.appendChild(row);
      }
    }
  }

  function render(container, form) {
    container.innerHTML = '';
    var fm = el('div', 'fm');

    var bar = el('div', 'fm-titlebar');
    var dateWrap = el('div', 'fm-date');
    dateWrap.appendChild(el('span', null, '작성일 :'));
    dateWrap.appendChild(renderCtrl(d(form.writtenOn || '')));
    bar.appendChild(dateWrap);
    bar.appendChild(el('div', 'fm-ttl', form.title));
    bar.appendChild(el('div', 'fm-date', form.dept || ''));
    fm.appendChild(bar);

    form.rows.forEach(function (block) { renderBlock(block, form, fm); });
    container.appendChild(fm);
  }

  global.OBForms = { render: render };
  global.OBFormDSL = { S: S, t: t, d: d, ta: ta, r: r, k: k, sel: sel, btn: btn, lbl: lbl, br: br,
    nna: nna, staff: staff, signBlock: signBlock, sonoHeader: sonoHeader,
    imageQuality: imageQuality, biometry: biometry, impression: impression,
    placentaBlock: placentaBlock, maternalAnatomy: maternalAnatomy };
})(window);
