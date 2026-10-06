import http from 'k6/http';
import { check } from 'k6';
import { Trend, Counter } from 'k6/metrics';

const BASE = __ENV.BASE || 'https://beacon.example.com';
const RATE = parseInt(__ENV.RATE || '50');
const DURATION = __ENV.DURATION || '40s';
const jsonH = { 'Content-Type': 'application/json' };
const api = (p) => `${BASE}/api/v1${p}`;

const r302 = new Counter('redirect_302');
const r404 = new Counter('redirect_404');
const rOther = new Counter('redirect_other');
const redirMs = new Trend('redirect_ms', true);

export const options = {
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
  scenarios: {
    redirects: {
      executor: 'constant-arrival-rate',
      rate: RATE,
      timeUnit: '1s',
      duration: DURATION,
      preAllocatedVUs: Math.max(20, Math.ceil(RATE / 2)),
      maxVUs: Math.max(50, RATE * 4),
      exec: 'redirect',
    },
  },
};

export function setup() {
  const me = http.get(api('/me'));
  const workspaceId = me.json('memberships.0.workspaceId');
  const links = [];
  for (let i = 0; i < 10; i++) {
    const code = `probe${i}`;
    const res = http.post(
      api(`/workspaces/${workspaceId}/links`),
      JSON.stringify({ destination: `https://example.com/probe/${i}`, code, title: `Probe ${i}` }),
      { headers: jsonH },
    );
    links.push({ id: res.status === 200 ? res.json('id') : null, code, status: res.status });
  }
  console.log(`setup RATE=${RATE} DURATION=${DURATION} ws=${workspaceId} ${JSON.stringify(links.map((l) => l.code + ':' + l.status))}`);
  return { workspaceId, links };
}

export function redirect(data) {
  const code = data.links[Math.floor(Math.random() * data.links.length)].code;
  const res = http.get(`${BASE}/${code}`, { redirects: 0, tags: { name: 'redirect' } });
  redirMs.add(res.timings.duration);
  if (res.status === 302) r302.add(1);
  else if (res.status === 404) r404.add(1);
  else rOther.add(1);
  check(res, { 'redirect is 302': (r) => r.status === 302 });
}

export function teardown(data) {
  let n = 0;
  for (const l of data.links) {
    if (l.id) {
      http.del(api(`/workspaces/${data.workspaceId}/links/${l.id}`));
      n++;
    }
  }
  console.log(`teardown: deleted ${n} probe links`);
}
