// MindSpace AI relay + AI backend. Node 18+, no dependencies.
// ANTHROPIC_API_KEY (optional) enables Claude; otherwise a local keyword fallback is used.
const http = require('http');
const PORT = process.env.PORT || 8787;
const MODEL = process.env.ANTHROPIC_MODEL || 'claude-sonnet-5-5';
const rooms = new Map();
const room = c => { c = c.toUpperCase(); if (!rooms.has(c)) rooms.set(c, { version: 1, cards: {}, decision: '', ai: null }); return rooms.get(c); };
const list = r => Object.values(r.cards);
const state = r => ({ version: r.version, cards: list(r), decision: r.decision, ai: r.ai, unchanged: false });

function localAI(cards) {
  const STOP = new Set('with that this from have will your their about into more than they them what when which could would should make using'.split(' '));
  const toks = t => new Set(t.toLowerCase().replace(/[^a-z ]/g, ' ').split(' ').filter(w => w.length > 3 && !STOP.has(w)));
  const gs = [];
  for (const c of cards) {
    const t = toks(c.text); let best = null, bs = 0;
    for (const g of gs) { let s = 0; t.forEach(w => g.w.has(w) && s++); if (s > bs) { bs = s; best = g; } }
    if (best) { best.ids.push(c.id); t.forEach(w => best.w.add(w)); }
    else { const l = [...t][0] || 'Idea'; gs.push({ ids: [c.id], w: new Set(t), label: l[0].toUpperCase() + l.slice(1) }); }
  }
  const size = id => gs.find(g => g.ids.includes(id)).ids.length;
  const top = [...cards].sort((a, b) => b.votes.length - a.votes.length || size(b.id) - size(a.id)).slice(0, 3);
  return { groups: gs.map(g => ({ label: g.label, ids: g.ids })),
    top: top.map(c => ({ id: c.id, why: 'Part of a large, well-supported theme.' })),
    plan: ['Define the problem and success measure', 'Interview 5 target users', 'Prototype the top idea in one week', 'Test with a small pilot', 'Review results and decide next steps'] };
}
async function claudeAI(cards) {
  const prompt = 'You facilitate a brainstorm. Group these ideas into 2-5 themes, pick the 3 most promising with a one-sentence reason, and write a 5-step action plan for the best one. Return ONLY JSON: {"groups":[{"label":"short","ids":["id"]}],"top":[{"id":"id","why":"reason"}],"plan":["step"]}. Every id in exactly one group. Ideas: ' + JSON.stringify(cards.map(c => ({ id: c.id, text: c.text })));
  const res = await fetch('https://api.anthropic.com/v1/messages', { method: 'POST',
    headers: { 'content-type': 'application/json', 'x-api-key': process.env.ANTHROPIC_API_KEY, 'anthropic-version': '2023-06-01' },
    body: JSON.stringify({ model: MODEL, max_tokens: 1500, messages: [{ role: 'user', content: prompt }] }) });
  if (!res.ok) throw new Error('AI HTTP ' + res.status);
  const txt = (await res.json()).content.map(b => b.text || '').join('');
  return JSON.parse(txt.slice(txt.indexOf('{'), txt.lastIndexOf('}') + 1));
}
function validate(r, cards) { // schema check; never trust model output
  const ids = new Set(cards.map(c => c.id)), used = new Set(), groups = [];
  if (!r || !Array.isArray(r.groups)) throw new Error('bad schema');
  for (const g of r.groups) { const x = (g.ids || []).filter(i => ids.has(i) && !used.has(i)); x.forEach(i => used.add(i)); if (x.length) groups.push({ label: String(g.label || 'Group').slice(0, 26), ids: x }); }
  const rest = [...ids].filter(i => !used.has(i)); if (rest.length) groups.push({ label: 'Other', ids: rest });
  if (!groups.length) throw new Error('empty');
  const byId = Object.fromEntries(cards.map(c => [c.id, c]));
  return { groups, top: (r.top || []).filter(t => ids.has(t.id)).slice(0, 3).map(t => ({ id: t.id, text: byId[t.id].text, why: String(t.why || '') })), plan: (r.plan || []).map(String).slice(0, 7) };
}
async function organize(r) {
  const cards = list(r); if (cards.length < 3) return;
  let out, source = 'Claude';
  try { if (!process.env.ANTHROPIC_API_KEY) throw new Error('no key'); out = validate(await claudeAI(cards), cards); }
  catch (e) { source = 'local fallback'; out = validate(localAI(cards), cards); }
  const n = out.groups.length;
  out.groups.forEach((g, gi) => g.ids.forEach((id, k) => { Object.assign(r.cards[id], { group: gi, groupLabel: g.label, x: (gi - (n - 1) / 2) * 2, y: 1.7 - k * 1.12, z: 0 }); }));
  r.ai = { source, groups: out.groups, top: out.top, plan: out.plan }; r.version++;
}
function markdown(r, code) {
  const L = ['# MindSpace AI session — room ' + code, ''], d = r.cards[r.decision];
  L.push('## Decision', d ? `**${d.text}** (${d.votes.length} votes)` : '_No decision confirmed yet._', '', '## Ideas by theme');
  const gs = {}; list(r).forEach(c => (gs[c.groupLabel || 'Unsorted'] ||= []).push(c));
  for (const [k, cs] of Object.entries(gs)) { L.push('', '### ' + k); cs.sort((a, b) => b.votes.length - a.votes.length).forEach(c => L.push(`- ${c.text} — ${c.author}, ▲ ${c.votes.length}`)); }
  if (r.ai) { L.push('', '## AI shortlist'); r.ai.top.forEach(t => L.push(`- **${t.text}**: ${t.why}`)); L.push('', '## Action plan'); r.ai.plan.forEach((p, i) => L.push(`${i + 1}. ${p}`)); }
  return L.join('\n') + '\n\n_AI suggestions are advisory; the team made the final decision._\n';
}
const body = req => new Promise(ok => { let b = ''; req.on('data', d => b += d); req.on('end', () => { try { ok(JSON.parse(b || '{}')); } catch { ok({}); } }); });
http.createServer(async (req, res) => {
  const u = new URL(req.url, 'http://x'), m = u.pathname.match(/^\/api\/room\/([A-Za-z0-9_-]{1,32})(?:\/(\w+))?$/);
  const send = (o, t = 'application/json') => { res.writeHead(200, { 'content-type': t + '; charset=utf-8', 'access-control-allow-origin': '*' }); res.end(typeof o === 'string' ? o : JSON.stringify(o)); };
  if (!m) { res.writeHead(404); return res.end(); }
  const r = room(m[1]), act = m[2] || '';
  if (req.method === 'GET' && !act) return send(Number(u.searchParams.get('since')) === r.version ? { version: r.version, cards: [], decision: r.decision, ai: null, unchanged: true } : state(r));
  if (req.method === 'GET' && act === 'export') return send(markdown(r, m[1].toUpperCase()), 'text/markdown');
  const b = await body(req);
  if (act === 'card') {
    const id = b.id || ('c' + Date.now().toString(36) + Math.random().toString(36).slice(2, 5));
    const c = r.cards[id] ||= { id, text: String(b.text || '').slice(0, 120), author: String(b.author || 'Guest').slice(0, 24), x: 0, y: 0, z: 0, group: -1, groupLabel: '', votes: [] };
    for (const k of ['x', 'y', 'z']) if (typeof b[k] === 'number') c[k] = b[k];
    r.version++;
  } else if (act === 'vote' && r.cards[b.id]) { const v = r.cards[b.id].votes, i = v.indexOf(b.voter); i < 0 ? v.push(b.voter) : v.splice(i, 1); r.version++; }
  else if (act === 'decide' && r.cards[b.id]) { r.decision = b.id; r.version++; }
  else if (act === 'organize') await organize(r);
  else if (act === 'clear') { r.cards = {}; r.decision = ''; r.ai = null; r.version++; }
  send(state(r));
}).listen(PORT, () => console.log(`MindSpace server on :${PORT}  (AI: ${process.env.ANTHROPIC_API_KEY ? MODEL : 'local fallback'})`));
