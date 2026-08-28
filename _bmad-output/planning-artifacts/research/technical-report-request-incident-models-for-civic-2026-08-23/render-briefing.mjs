import fs from 'node:fs';
import { execFileSync } from 'node:child_process';

const runDir = new URL('.', import.meta.url).pathname.replace(/^\/(.:)/, '$1');
const reportPath = `${runDir}research.md`;
const outputPath = `${runDir}research-briefing.html`;
const python = 'C:/Users/CytekPC/AppData/Roaming/uv/python/cpython-3.14-windows-x86_64-none/python.exe';
const kit = 'C:/Users/CytekPC/.agents/skills/bmad-deep-recon/scripts/recon_kit.py';

const escapeHtml = (value) => value
  .replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;')
  .replaceAll('"', '&quot;').replaceAll("'", '&#39;');

const slug = (value) => value.toLowerCase().normalize('NFD')
  .replace(/[\u0300-\u036f]/g, '').replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '');

const inline = (value) => escapeHtml(value)
  .replace(/`([^`]+)`/g, '<code>$1</code>')
  .replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
  .replace(/\[(\d+)\]/g, '<a class="cite" href="#src-$1">[$1]</a>');

function renderMarkdown(markdown) {
  const codeBlocks = [];
  markdown = markdown.replace(/```[^\n]*\n([\s\S]*?)```/g, (_, code) => {
    const token = `@@CODE${codeBlocks.length}@@`;
    codeBlocks.push(`<pre><code>${escapeHtml(code.trimEnd())}</code></pre>`);
    return token;
  });
  const lines = markdown.split(/\r?\n/);
  const out = [];
  for (let i = 0; i < lines.length;) {
    const line = lines[i];
    if (!line.trim()) { i++; continue; }
    if (/^@@CODE\d+@@$/.test(line)) { out.push(codeBlocks[Number(line.match(/\d+/)[0])]); i++; continue; }
    const heading = line.match(/^(#{1,4})\s+(.+)$/);
    if (heading) {
      const level = heading[1].length;
      const title = heading[2];
      out.push(`<h${level} id="${slug(title)}">${inline(title)}</h${level}>`);
      i++; continue;
    }
    if (line.startsWith('|') && i + 1 < lines.length && /^\|?[\s|:-]+\|?$/.test(lines[i + 1])) {
      const rows = [];
      while (i < lines.length && lines[i].startsWith('|')) rows.push(lines[i++]);
      rows.splice(1, 1);
      const cells = rows.map(row => row.slice(1, -1).split('|').map(cell => cell.trim()));
      const head = cells.shift();
      out.push('<div class="table-wrap"><table><thead><tr>' + head.map(c => `<th>${inline(c)}</th>`).join('') + '</tr></thead><tbody>' +
        cells.map(row => '<tr>' + row.map(c => `<td>${inline(c)}</td>`).join('') + '</tr>').join('') + '</tbody></table></div>');
      continue;
    }
    if (/^- /.test(line)) {
      const items = [];
      while (i < lines.length && /^- /.test(lines[i])) items.push(lines[i++].slice(2));
      out.push(`<ul>${items.map(item => `<li>${inline(item)}</li>`).join('')}</ul>`); continue;
    }
    if (/^\d+\. /.test(line)) {
      const items = [];
      while (i < lines.length && /^\d+\. /.test(lines[i])) items.push(lines[i++].replace(/^\d+\. /, ''));
      out.push(`<ol>${items.map(item => `<li>${inline(item)}</li>`).join('')}</ol>`); continue;
    }
    const paragraph = [line.trim()]; i++;
    while (i < lines.length && lines[i].trim() && !/^(#{1,4})\s|^- |^\d+\. |^\||^@@CODE/.test(lines[i])) paragraph.push(lines[i++].trim());
    out.push(`<p>${inline(paragraph.join(' '))}</p>`);
  }
  return out.join('\n');
}

let markdown = fs.readFileSync(reportPath, 'utf8').replace(/^---[\s\S]*?---\s*/, '');
const sourceStart = markdown.indexOf('## Source appendix');
const staleStart = markdown.indexOf('## Staleness map');
markdown = `${markdown.slice(0, sourceStart)}\n@@SOURCES@@\n${markdown.slice(staleStart)}`;
let body = renderMarkdown(markdown);

const sourceResult = JSON.parse(execFileSync(python, [kit, 'escape-sources', reportPath], {
  encoding: 'utf8',
  env: { ...process.env, PYTHONIOENCODING: 'utf-8' }
}));
const sourceBlock = `<h2 id="source-appendix">Source appendix</h2><details><summary>${sourceResult.rows} nguồn đã kiểm tra URL</summary><div class="table-wrap">${sourceResult.html}</div></details>`;
body = body.replace('<p>@@SOURCES@@</p>', sourceBlock);

const headings = [...markdown.matchAll(/^(##|###)\s+(.+)$/gm)]
  .filter(([, , title]) => title !== 'Source appendix')
  .map(([, hashes, title]) => `<a class="toc-${hashes.length}" href="#${slug(title)}">${escapeHtml(title)}</a>`).join('');

const html = `<!doctype html><html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Report/Request–Incident | UrbanService Research</title><style>
:root{color-scheme:light dark;--bg:#f5f7fb;--panel:#fff;--text:#172033;--muted:#5c677d;--line:#dbe1ea;--accent:#2557d6;--good:#147a4b;--warn:#9a5b00}*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:16px/1.65 system-ui,-apple-system,Segoe UI,sans-serif}.hero{background:linear-gradient(135deg,#173a8f,#3f6fe8);color:#fff;padding:48px max(24px,calc((100% - 1180px)/2))}.hero h1{max-width:900px;margin:8px 0;font-size:clamp(30px,5vw,52px);line-height:1.12}.meta{opacity:.86}.badges{display:flex;gap:10px;flex-wrap:wrap;margin-top:18px}.badge{background:#ffffff1c;border:1px solid #ffffff55;padding:6px 10px;border-radius:999px;font-weight:700}.badge.unverified{background:#6f3b00}.layout{max-width:1180px;margin:0 auto;display:grid;grid-template-columns:250px minmax(0,1fr);gap:28px;padding:28px 20px 64px}.toc{position:sticky;top:18px;align-self:start;background:var(--panel);border:1px solid var(--line);border-radius:14px;padding:16px;max-height:calc(100vh - 36px);overflow:auto}.toc a{display:block;color:var(--text);text-decoration:none;padding:5px 2px}.toc-3{padding-left:14px!important;color:var(--muted)!important;font-size:14px}.content{background:var(--panel);border:1px solid var(--line);border-radius:16px;padding:clamp(20px,4vw,48px);min-width:0}.content h1{display:none}.content h2{margin-top:48px;border-top:1px solid var(--line);padding-top:30px;font-size:27px}.content h2:first-of-type{margin-top:0;border:0;padding-top:0}.content h3{margin-top:28px;font-size:20px}.content p{max-width:82ch}.content code{background:#eef2fa;color:#243d73;padding:.12em .35em;border-radius:5px}.content pre{overflow:auto;background:#111827;color:#e5e7eb;padding:18px;border-radius:12px}.table-wrap{overflow:auto}table{border-collapse:collapse;width:100%;font-size:14px}th,td{border-bottom:1px solid var(--line);text-align:left;vertical-align:top;padding:10px}th{background:#eef2fa;color:#243d73}.cite{font-weight:700;color:var(--accent);text-decoration:none}details{border:1px solid var(--line);border-radius:12px;padding:12px}summary{cursor:pointer;font-weight:700}.sources a{color:var(--accent)}
@media(max-width:850px){.layout{display:block}.toc{position:relative;top:auto;margin-bottom:20px;max-height:none}.hero{padding:34px 20px}.content{padding:22px}}@media(prefers-color-scheme:dark){:root{--bg:#0d1320;--panel:#151d2c;--text:#edf2ff;--muted:#aeb8ca;--line:#2d3950;--accent:#8eb1ff}.content code,th{background:#202b3f;color:#dbe7ff}}
</style></head><body><header class="hero"><div class="meta">Technical research · 2026-08-23 · standard depth · normal verification</div><h1>Report/Request và Incident trong xử lý phản ánh đô thị</h1><p>Quyết định: mô hình dữ liệu và lộ trình áp dụng cho UrbanService.</p><div class="badges"><span class="badge">Verified claims: 7</span><span class="badge unverified">Recommendation: unverified until ADR/implementation validation</span><span class="badge">15 primary/official sources</span></div></header><div class="layout"><nav class="toc"><strong>Mục lục</strong>${headings}<a class="toc-2" href="#source-appendix">Source appendix</a></nav><main class="content">${body}</main></div></body></html>`;
fs.writeFileSync(outputPath, html, 'utf8');
console.log(outputPath);
