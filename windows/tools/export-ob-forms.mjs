// Converts the web demo's form schemas into the blank form definitions the Windows app reads
// (src/EmrDemo.Core/Seed/forms.json): the OB forms (ob-forms.js + ob-forms-data.js) and the
// paper-style surgery forms (paper-forms.js + surgery-forms-data.js) at the repo root.
// Every input gets a stable key; all example values are dropped so each form starts empty.
// Usage: node windows/tools/export-ob-forms.mjs
import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const root = fileURLToPath(new URL('../../', import.meta.url));
const context = { document: {} };
context.window = context;
vm.createContext(context);
for (const file of ['ob-forms.js', 'ob-forms-data.js', 'paper-forms.js', 'surgery-forms-data.js']) {
  vm.runInContext(readFileSync(root + file, 'utf8'), context, { filename: file });
}

function control(spec, key) {
  switch (spec.c) {
    case 'lbl': return { c: 'label', text: spec.text };
    case 'br': return { c: 'break' };
    case 'b': return { c: 'button', text: spec.label };
    case 't': return { c: 'text', key, w: spec.w ?? 90, ...(spec.fill && { fill: true }),
      ...(spec.hl && { highlight: true }), ...(spec.ro && { readOnly: true }), ...(spec.grow && { grow: true }) };
    case 'd': return { c: 'date', key };
    case 'ta': return { c: 'textarea', key, h: spec.h ?? 54, ...(spec.w && { w: spec.w }) };
    case 's': return { c: 'select', key, options: spec.opts, ...(spec.w && { w: spec.w }) };
    case 'r':
    case 'k': {
      const out = { c: spec.c === 'r' ? 'radio' : 'check', key, options: spec.opts };
      for (const option of Object.keys(spec.inlineFields || {})) {
        if (!spec.opts.includes(option)) throw new Error(`${key}: inline field for unknown option "${option}"`);
      }
      if (spec.other && spec.opts.includes(spec.otherLabel || 'Other')) {
        throw new Error(`${key}: other label duplicates an option`);
      }
      if (spec.stack) out.stack = true;
      if (spec.inlineFields) {
        out.inline = Object.fromEntries(Object.entries(spec.inlineFields).map(([option, items]) =>
          [option, items.map((item, i) => control(item, `${key}.${spec.opts.indexOf(option)}.${i}`))]));
      }
      if (spec.other) out.other = { key: `${key}.other`, w: spec.other, text: spec.otherLabel || 'Other' };
      if (spec.tail) out.tail = control(spec.tail, `${key}.tail`);
      return out;
    }
    // paper-forms.js 전용 컨트롤
    case 'lw': return { c: 'label', text: spec.text, ...(spec.w && { w: spec.w }) };
    case 'sign': return { c: 'label', text: '(서명)' };
    case 'pt': return { c: 'patient', field: spec.field };
    case 'logo': return { c: 'image', name: 'logo', w: 110, h: 26 };
    case 'mp': return { c: 'vbox', center: true, items: [
      { c: 'image', name: `mallampati${spec.n}`, w: 72, h: 90 },
      { c: 'check', key: `${key}.0`, options: [`Class ${spec.n}`] }] };
    case 'vlegend': return { c: 'legend', h: spec.h, min: spec.min, max: spec.max };
    case 'vchart': return { c: 'chart', columns: spec.cols, h: spec.h, min: spec.min, max: spec.max };
    case 'ln': case 'vb': case 'hb': {
      const items = spec.items.map((item, i) => control(item, `${key}.${i}`));
      const cls = (spec.cls || '').split(' ').filter(Boolean);
      for (const c of cls) {
        if (!['c', 'spread', 'pf-method'].includes(c)) throw new Error(`${key}: unknown ${spec.c} class "${c}"`);
      }
      if (spec.c === 'ln') return { c: 'line', items, ...(cls.includes('c') && { center: true }) };
      if (spec.c === 'vb') return { c: 'vbox', items, ...(spec.w && { w: spec.w }) };
      return { c: 'hbox', items, ...(cls.includes('spread') && { spread: true }) };
    }
    default: throw new Error(`unknown control ${spec.c}`);
  }
}

/* 웹 칸 클래스(paper-forms.css) → Windows 칸 스타일 */
const CELL_STYLES = {
  l: 'left', b: 'bold', plain: 'plain', nb: 'noBorder', 'pf-big': 'big', bh: 'head', c: 'center',
  spread: 'spread', k5: 'grid5', fill: 'fill', tg: 'timeGrid', top: 'top', dl: 'compact', ch: 'chart', bb: null,
};

/* 폭이 null 인 열은 웹처럼 서식 폭에서 나머지를 똑같이 나눠 갖는다 */
function columnWidths(cols, width) {
  const fixed = cols.reduce((sum, w) => sum + (w ?? 0), 0);
  const flex = cols.filter((w) => w == null).length;
  if (!flex) return cols;
  const share = Math.max(20, (width - fixed) / flex);
  let rest = Math.round(share * flex);
  return cols.map((w, i) => {
    if (w != null) return w;
    const last = cols.slice(i + 1).every((x) => x != null);
    const out = last ? rest : Math.floor(share);
    rest -= out;
    return out;
  });
}

function convertPaperForm(form) {
  const width = form.width || 760;
  const blocks = form.blocks.map((block, index) => {
    const key = `${form.id}.b${index}`;
    switch (block.type) {
      case 'title': return { type: 'heading', text: block.text };
      case 'gap': return { type: 'gap', h: block.h || 6 };
      case 'line': return { type: 'line', items: block.items.map((item, i) => control(item, `${key}.${i}`)) };
      case 'table':
        return {
          type: 'table', columns: columnWidths(block.cols, width),
          rows: block.rows.map((row, r) => row.map((cell, c) => {
            if (typeof cell === 'string') cell = { l: cell };
            const style = (cell.cls || '').split(' ').filter(Boolean).map((cls) => {
              if (!(cls in CELL_STYLES)) throw new Error(`${key}: unknown cell class "${cls}"`);
              return CELL_STYLES[cls];
            }).filter(Boolean);
            return {
              ...(cell.cs && { colSpan: cell.cs }), ...(cell.rs && { rowSpan: cell.rs }),
              ...(cell.l != null ? { label: cell.l } : { items: cell.i.map((item, i) => control(item, `${key}.r${r}c${c}.${i}`)) }),
              ...(cell.h && { h: cell.h }), ...(style.length && { style }),
            };
          })),
        };
      default: throw new Error(`unknown paper block ${block.type}`);
    }
  });
  return {
    id: form.id, title: form.title, department: form.dept, summary: form.summary,
    labelWidth: 150, writtenOnKey: `${form.id}.writtenOn`, blocks,
    ...(form.patient && { patient: form.patient }),
  };
}

function convertForm(form) {
  let index = 0;
  const convert = (blocks) => {
    const out = [];
    for (const block of blocks) {
      const key = `${form.id}.b${index++}`;
      switch (block.type) {
        case 'sec': case 'sub': out.push({ type: block.type, text: block.label }); break;
        case 'note': case 'static': out.push({ type: block.type, text: block.text }); break;
        case 'draw': out.push({ type: 'draw', text: block.label || 'Text' }); break;
        case 'row':
          out.push({ type: 'row', label: block.label || '', indent: block.indent || 0,
            items: block.items.map((item, i) => control(item, `${key}.${i}`)) });
          break;
        case 'grid':
          out.push({ type: 'grid', key, title: block.title || '', buttons: block.btns || ['추가', '삭제'],
            columns: block.cols.map((c) => ({ header: c.h, w: c.w })),
            rows: Math.max(block.rows || 0, (block.data || []).length), ...(block.peach && { peach: true }) });
          break;
        case 'set': out.push({ type: 'set', text: block.label, blocks: convert(block.rows) }); break;
        case 'signstart': out.push({ type: 'sign', blocks: [] }); break;
        case 'signend': break;
        default: throw new Error(`unknown block ${block.type}`);
      }
      const open = out.findLast((b) => b.type === 'sign');
      if (open && block.type === 'row' && out.at(-1) !== open && open === out.at(-2)) {
        open.blocks.push(out.pop());
      }
    }
    return out;
  };
  return {
    id: form.id, title: form.title, department: form.dept, summary: form.summary,
    labelWidth: form.labelW || 150, writtenOnKey: `${form.id}.writtenOn`, blocks: convert(form.rows),
  };
}

const forms = [...context.OB_FORMS.map(convertForm), ...context.SURGERY_FORMS.map(convertPaperForm)];
const target = root + 'windows/src/EmrDemo.Core/Seed/forms.json';
writeFileSync(target, JSON.stringify(forms, null, 1) + '\n');
console.log(`wrote ${forms.length} forms to ${target}`);
