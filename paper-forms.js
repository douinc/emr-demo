/* ============================================================
   종이 서식형 구조화 서식지 - 스키마 헬퍼 & 렌더러
   표(칸 병합)로 짠 인쇄용 서식을 그대로 옮긴 서식에 쓴다.
   컨트롤(t·ta·r·k·sel 등)은 ob-forms.js 의 것을 그대로 쓴다.
   window.PaperForms.render(container, form, ctx) 로 그린다.
   ctx : { id, name, dept } - 머리글의 환자 칸에 들어갈 값
   ============================================================ */
(function (global) {
  'use strict';
  var D = global.OBFormDSL;
  var renderCtrl = global.OBForms.renderCtrl;

  /* ---------------- 칸(셀) 단축 생성기 ---------------- */
  function L(text, o) { return Object.assign({ l: text }, o); }   // 라벨 칸
  function C(items, o) { return Object.assign({ i: items }, o); } // 입력 칸

  /* ---------------- 서식 전용 컨트롤 ---------------- */
  function f(o)            { return D.t(null, Object.assign({ grow: true }, o)); } // 칸 폭을 채우는 입력
  function lw(text, w)     { return { c: 'lw', text: text, w: w }; }               // 폭 고정 라벨
  function pt(field)       { return { c: 'pt', field: field }; }                   // 환자 정보(읽기 전용)
  function ln(items, o)    { return Object.assign({ c: 'ln', items: items }, o); } // 가로 한 줄
  function vb(items, o)    { return Object.assign({ c: 'vb', items: items }, o); } // 세로 묶음
  function hb(items, o)    { return Object.assign({ c: 'hb', items: items }, o); } // 가로 묶음
  function mallampati(n, checked) { return { c: 'mp', n: n, val: !!checked }; }
  function logo()          { return { c: 'logo' }; }
  function sign()          { return { c: 'sign' }; }
  function vlegend(o)      { return Object.assign({ c: 'vlegend' }, o); }
  function vchart(o)       { return Object.assign({ c: 'vchart' }, o); }

  /* ---------------- 블록 단축 생성기 ---------------- */
  var P = {
    title: function (text) { return { type: 'title', text: text }; },
    line:  function (items) { return { type: 'line', items: items }; },
    table: function (cols, rows, o) { return Object.assign({ type: 'table', cols: cols, rows: rows }, o); },
    /* 머리줄 + 입력 칸으로 된 상자 (초진기록지의 Chief Complaint 등) */
    box: function (label, h, val) {
      return P.table([null], [[L(label, { cls: 'bh' })], [C([D.ta(h, { val: val || '' })], { cls: 'bb' })]]);
    },
    gap: function (h) { return { type: 'gap', h: h }; }
  };

  /* ---------------- DOM 유틸 ---------------- */
  function el(tag, cls, text) {
    var n = document.createElement(tag);
    if (cls) n.className = cls;
    if (text != null) n.textContent = text;
    return n;
  }
  function px(v) { return typeof v === 'number' ? v + 'px' : v; }

  /* ---------------- 그림 : Mallampati 입 모양 ---------------- */
  function mouthSvg(n) {
    // 혀가 올라올수록(등급이 높을수록) 목젖·편도궁이 가려진다
    var tongueTop = [0, 60, 52, 44, 34][n];
    var cy = (tongueTop + 88) / 2, ry = (88 - tongueTop) / 2;
    var s = '<svg viewBox="0 0 80 100" width="72" height="90" aria-hidden="true">' +
      '<ellipse cx="40" cy="50" rx="37" ry="47" fill="#c62f3a"/>' +
      '<ellipse cx="40" cy="50" rx="29" ry="39" fill="#6e1220"/>' +
      '<ellipse cx="40" cy="34" rx="21" ry="14" fill="#d9707f"/>';
    if (n <= 2) s += '<path d="M22 40 Q24 62 28 74 M58 40 Q56 62 52 74" stroke="#a83448" stroke-width="3" fill="none"/>';
    if (n <= 3) s += '<ellipse cx="40" cy="' + (n === 3 ? 42 : 46) + '" rx="4" ry="' + (n === 3 ? 5 : 8) + '" fill="#b8475a"/>';
    s += '<ellipse cx="40" cy="' + cy + '" rx="25" ry="' + ry + '" fill="#f2a9b6" stroke="#d27b8d"/>' +
      '<line x1="40" y1="' + (tongueTop + 6) + '" x2="40" y2="82" stroke="#d98a9b"/>' +
      '<path d="M13 30 Q40 2 67 30 L62 35 Q40 12 18 35 Z" fill="#f6f6f2" stroke="#bdbdb5"/>' +
      '<path d="M15 72 Q40 99 65 72 L60 68 Q40 90 20 68 Z" fill="#f6f6f2" stroke="#bdbdb5"/>' +
      '</svg>';
    return s;
  }

  /* ---------------- 그림 : 마취기록지 활력징후 그래프 ---------------- */
  // 세로축 min~max(mmHg), 가로는 cols 칸(5분 단위). data 의 i 는 칸 번호.
  function chartGeom(o) {
    var h = o.h || 300, min = o.min || 10, max = o.max || 210;
    return { h: h, min: min, max: max, y: function (v) { return (max - v) / (max - min) * h; } };
  }

  function vchartSvg(o) {
    var g = chartGeom(o), cols = o.cols, cw = o.cw || 13, w = cols * cw, data = o.data || {};
    var s = '<svg viewBox="0 0 ' + w + ' ' + g.h + '" width="100%" height="' + g.h +
            '" preserveAspectRatio="none" aria-label="활력징후 그래프">';
    for (var v = g.min; v <= g.max; v += 10) {
      s += '<line x1="0" x2="' + w + '" y1="' + g.y(v) + '" y2="' + g.y(v) + '" stroke="' +
           (v % 20 === 0 ? '#555' : '#aaa') + '" stroke-width="' + (v % 20 === 0 ? 0.8 : 0.5) + '"/>';
    }
    for (var c = 1; c < cols; c++) {
      var major = c % 3 === 0;
      s += '<line y1="0" y2="' + g.h + '" x1="' + c * cw + '" x2="' + c * cw + '" stroke="' +
           (major ? '#222' : '#999') + '" stroke-width="' + (major ? 1.4 : 0.5) + '"/>';
    }
    function cx(i) { return i * cw + cw / 2; }
    (data.sbp || []).forEach(function (v, i) {        // 수축기 ∨ (꼭짓점이 값)
      if (v == null) return;
      var x = cx(i), y = g.y(v);
      s += '<path d="M' + (x - 4) + ' ' + (y - 6) + ' L' + x + ' ' + y + ' L' + (x + 4) + ' ' + (y - 6) +
           '" stroke="#c0141e" stroke-width="1.4" fill="none"/>';
    });
    (data.dbp || []).forEach(function (v, i) {        // 이완기 ∧
      if (v == null) return;
      var x = cx(i), y = g.y(v);
      s += '<path d="M' + (x - 4) + ' ' + (y + 6) + ' L' + x + ' ' + y + ' L' + (x + 4) + ' ' + (y + 6) +
           '" stroke="#c0141e" stroke-width="1.4" fill="none"/>';
    });
    var pulse = (data.pulse || []).map(function (v, i) { return v == null ? null : [cx(i), g.y(v)]; })
      .filter(Boolean);
    if (pulse.length > 1) {
      s += '<polyline points="' + pulse.map(function (p) { return p.join(','); }).join(' ') +
           '" stroke="#1b4fa0" stroke-width="0.8" fill="none"/>';
    }
    pulse.forEach(function (p) { s += '<circle cx="' + p[0] + '" cy="' + p[1] + '" r="2.2" fill="#1b4fa0"/>'; });
    (data.marks || []).forEach(function (m) {
      var x = cx(m.i), y = g.y(m.v || 205);
      if (m.s === 'x') {
        s += '<path d="M' + (x - 4) + ' ' + (y - 4) + ' L' + (x + 4) + ' ' + (y + 4) + ' M' + (x + 4) + ' ' +
             (y - 4) + ' L' + (x - 4) + ' ' + (y + 4) + '" stroke="#111" stroke-width="1.5"/>';
      } else if (m.s === 'o') {
        s += '<circle cx="' + x + '" cy="' + y + '" r="4.5" fill="#fff" stroke="#111" stroke-width="1.2"/>' +
             '<circle cx="' + x + '" cy="' + y + '" r="1.4" fill="#111"/>';
      }
    });
    (data.resp || []).forEach(function (mode, i) {     // 호흡 방식 : s=SPONT a=ASSIST c=CONTROL
      if (!mode) return;
      var x = cx(i), y = g.y(15);
      s += '<circle cx="' + x + '" cy="' + y + '" r="4" fill="#fff" stroke="#111"/>';
      if (mode === 'a') s += '<line x1="' + (x - 3) + '" y1="' + (y + 3) + '" x2="' + (x + 3) + '" y2="' + (y - 3) + '" stroke="#111"/>';
      if (mode === 'c') s += '<path d="M' + (x - 3) + ' ' + (y - 3) + ' L' + (x + 3) + ' ' + (y + 3) + ' M' + (x + 3) +
                             ' ' + (y - 3) + ' L' + (x - 3) + ' ' + (y + 3) + '" stroke="#111"/>';
    });
    return s + '</svg>';
  }

  function renderLegend(o) {
    var g = chartGeom(o);
    var box = el('div', 'pf-legend');
    box.style.height = px(g.h);
    var keys = el('div', 'pf-legend-keys');
    ['○ SPONT', '⊘ ASSIST', '⊗ CONTROL', '∧∨ BP', '• PULSE', '× ANESTHESIA', '⊙ OPERATION'].forEach(function (k) {
      keys.appendChild(el('div', null, k));
    });
    box.appendChild(keys);
    for (var v = 20; v <= 200; v += 20) {
      var tick = el('span', 'pf-legend-tick', String(v));
      tick.style.top = px(g.y(v) - 7);
      box.appendChild(tick);
    }
    return box;
  }

  /* ---------------- 컨트롤 렌더링 ---------------- */
  function renderItem(spec, ctx) {
    switch (spec.c) {
      case 'lw': {
        var label = el('span', 'unit pf-lw', spec.text);
        if (spec.w) label.style.width = px(spec.w);
        return label;
      }
      case 'pt': {
        var input = document.createElement('input');
        input.type = 'text';
        input.readOnly = true;
        input.className = 'pf-pt';
        input.value = ctx[spec.field] || '';
        var chars = input.value.split('').reduce(function (n, ch) { return n + (ch.charCodeAt(0) > 0x2e80 ? 1.6 : 1); }, 0);
        input.style.width = px(Math.max(60, Math.ceil(chars * 7.5) + 14));
        return input;
      }
      case 'ln':
      case 'vb':
      case 'hb': {
        var group = el('div', 'pf-' + spec.c + (spec.cls ? ' ' + spec.cls : ''));
        if (spec.w) { group.style.width = px(spec.w); group.style.flex = '0 0 auto'; }
        spec.items.forEach(function (item) { group.appendChild(renderItem(item, ctx)); });
        return group;
      }
      case 'mp': {
        var fig = el('div', 'pf-mp');
        var pic = el('div', 'pf-mp-pic');
        pic.innerHTML = mouthSvg(spec.n);
        fig.appendChild(pic);
        var name = 'Class ' + spec.n;
        fig.appendChild(renderCtrl(D.k([name], spec.val ? [name] : [])));
        return fig;
      }
      case 'logo': {
        // 병원 로고는 비식별화한 자리표시 로고로 대신한다
        var mark = el('div', 'pf-logo');
        mark.innerHTML = '<svg viewBox="0 0 24 24" width="24" height="24" aria-hidden="true">' +
          '<rect x="1" y="1" width="22" height="22" rx="5" fill="#5b6b80"/>' +
          '<path d="M10 5h4v5h5v4h-5v5h-4v-5H5v-4h5z" fill="#fff"/></svg>';
        mark.appendChild(el('span', null, '예시병원'));
        return mark;
      }
      case 'sign': return el('span', 'pf-sign', '(서명)');
      case 'vlegend': return renderLegend(spec);
      case 'vchart': {
        var chart = el('div', 'pf-vchart');
        chart.innerHTML = vchartSvg(spec);
        return chart;
      }
      default: return renderCtrl(spec);
    }
  }

  /* ---------------- 표 렌더링 ---------------- */
  // 폭이 null 인 열은 표 폭(width)에서 나머지를 똑같이 나눠 갖는다
  function renderTable(block, ctx, width) {
    var table = el('table', 'pf-tbl' + (block.cls ? ' ' + block.cls : ''));
    var fixed = 0, flex = 0;
    block.cols.forEach(function (w) { if (w == null) flex++; else fixed += w; });
    var share = flex ? Math.max(20, (width - fixed) / flex) : 0;
    table.style.width = px(fixed + share * flex);
    var colgroup = document.createElement('colgroup');
    block.cols.forEach(function (w) {
      var col = document.createElement('col');
      col.style.width = px(w == null ? share : w);
      colgroup.appendChild(col);
    });
    table.appendChild(colgroup);

    block.rows.forEach(function (row) {
      var tr = document.createElement('tr');
      row.forEach(function (cell) {
        if (typeof cell === 'string') cell = L(cell);
        var isLabel = cell.i == null;
        var td = el('td', (isLabel ? 'lb' : 'in') + (cell.cls ? ' ' + cell.cls : ''));
        if (cell.cs) td.colSpan = cell.cs;
        if (cell.rs) td.rowSpan = cell.rs;
        if (cell.h) td.style.height = px(cell.h);
        if (isLabel) {
          td.textContent = cell.l;
        } else {
          var box = el('div', 'cell');
          cell.i.forEach(function (item) { box.appendChild(renderItem(item, ctx)); });
          td.appendChild(box);
        }
        tr.appendChild(td);
      });
      table.appendChild(tr);
    });
    return table;
  }

  function render(container, form, ctx) {
    ctx = Object.assign({}, form.patient, ctx);
    if (ctx.sex || ctx.age) ctx.sexAge = (ctx.sex || '') + ' / ' + (ctx.age || '');
    container.innerHTML = '';
    var fm = el('div', 'fm pf');
    var width = form.width || 760;
    var page = el('div', 'pf-page');
    page.style.width = px(width + 24);

    form.blocks.forEach(function (block) {
      switch (block.type) {
        case 'title': page.appendChild(el('div', 'pf-title', block.text)); return;
        case 'gap': {
          var gap = el('div');
          gap.style.height = px(block.h || 6);
          page.appendChild(gap);
          return;
        }
        case 'line': {
          var line = el('div', 'pf-line');
          block.items.forEach(function (item) { line.appendChild(renderItem(item, ctx)); });
          page.appendChild(line);
          return;
        }
        case 'table': page.appendChild(renderTable(block, ctx, width)); return;
        default: throw new Error('unknown paper block ' + block.type);
      }
    });
    fm.appendChild(page);
    container.appendChild(fm);
  }

  global.PaperForms = { render: render };
  global.PaperFormDSL = { P: P, L: L, C: C, f: f, lw: lw, pt: pt, ln: ln, vb: vb, hb: hb,
    mallampati: mallampati, logo: logo, sign: sign, vlegend: vlegend, vchart: vchart };
})(window);
