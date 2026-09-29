// Converts the web demo's OB form schemas (ob-forms.js + ob-forms-data.js at the repo root)
// into the blank form definitions the Windows app reads (src/EmrDemo.Core/Seed/forms.json).
// Every input gets a stable key; all example values are dropped so each form starts empty.
// Usage: node windows/tools/export-ob-forms.mjs
import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const root = fileURLToPath(new URL('../../', import.meta.url));
const context = { document: {} };
context.window = context;
vm.createContext(context);
for (const file of ['ob-forms.js', 'ob-forms-data.js']) {
  vm.runInContext(readFileSync(root + file, 'utf8'), context, { filename: file });
}

function control(spec, key) {
  switch (spec.c) {
    case 'lbl': return { c: 'label', text: spec.text };
    case 'br': return { c: 'break' };
    case 'b': return { c: 'button', text: spec.label };
    case 't': return { c: 'text', key, w: spec.w ?? 90, ...(spec.fill && { fill: true }),
      ...(spec.hl && { highlight: true }), ...(spec.ro && { readOnly: true }) };
    case 'd': return { c: 'date', key };
    case 'ta': return { c: 'textarea', key, h: spec.h ?? 54, ...(spec.w && { w: spec.w }) };
    case 's': return { c: 'select', key, options: spec.opts, ...(spec.w && { w: spec.w }) };
    case 'r':
    case 'k': {
      const out = { c: spec.c === 'r' ? 'radio' : 'check', key, options: spec.opts };
      if (spec.stack) out.stack = true;
      if (spec.inlineFields) {
        out.inline = Object.fromEntries(Object.entries(spec.inlineFields).map(([option, items]) =>
          [option, items.map((item, i) => control(item, `${key}.${spec.opts.indexOf(option)}.${i}`))]));
      }
      if (spec.other) out.other = { key: `${key}.other`, w: spec.other, text: spec.otherLabel || 'Other' };
      if (spec.tail) out.tail = control(spec.tail, `${key}.tail`);
      return out;
    }
    default: throw new Error(`unknown control ${spec.c}`);
  }
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

const forms = context.OB_FORMS.map(convertForm);
const target = root + 'windows/src/EmrDemo.Core/Seed/forms.json';
writeFileSync(target, JSON.stringify(forms, null, 1) + '\n');
console.log(`wrote ${forms.length} forms to ${target}`);
