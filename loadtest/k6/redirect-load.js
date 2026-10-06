import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Counter } from 'k6/metrics';
import exec from 'k6/execution';

const BASE = __ENV.BASE || 'http://localhost:8899';
const HOST = __ENV.VHOST || 'beacon.example.com';
const H = { Host: HOST };
const jsonH = { 'Content-Type': 'application/json', Host: HOST };
const api = (p) => `${BASE}/api/v1${p}`;

const r302 = new Counter('redirect_302');
const r404 = new Counter('redirect_404');
const rOther = new Counter('redirect_other');
const redirMs = new Trend('redirect_ms', true);
const mutMs = new Trend('mutation_ms', true);

export const options = {
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
  scenarios: {
    redirects: {
      executor: 'ramping-arrival-rate',
      exec: 'redirect',
      startRate: 100,
      timeUnit: '1s',
      preAllocatedVUs: 150,
      maxVUs: 2500,
      stages: [
        { target: 300, duration: '20s' },
        { target: 800, duration: '20s' },
        { target: 1500, duration: '20s' },
        { target: 2500, duration: '30s' },
        { target: 4000, duration: '30s' },
        { target: 4000, duration: '20s' },
        { target: 0, duration: '10s' },
      ],
    },
    mutations: {
      executor: 'per-vu-iterations',
      exec: 'mutate',
      vus: 1,
      iterations: 4,
      startTime: '72s',
      maxDuration: '40s',
    },
  },
};

export function setup() {
  const me = http.get(api('/me'), { headers: H });
  const workspaceId = me.json('memberships.0.workspaceId');
  const links = [];
  for (let i = 0; i < 10; i++) {
    const code = `load${i}`;
    const res = http.post(
      api(`/workspaces/${workspaceId}/links`),
      JSON.stringify({ destination: `https://example.com/dest/${i}`, code, title: `Load ${i}` }),
      { headers: jsonH },
    );
    links.push({ id: res.status === 200 ? res.json('id') : null, code, status: res.status });
  }
  console.log(`setup: workspace=${workspaceId} created=${JSON.stringify(links.map((l) => `${l.code}:${l.status}`))}`);
  return { workspaceId, links };
}

export function redirect(data) {
  const code = data.links[Math.floor(Math.random() * data.links.length)].code;
  const res = http.get(`${BASE}/${code}`, { headers: H, redirects: 0, tags: { name: 'redirect' } });
  redirMs.add(res.timings.duration);
  if (res.status === 302) r302.add(1);
  else if (res.status === 404) r404.add(1);
  else rOther.add(1);
  check(res, { 'redirect resolved (302|404)': (r) => r.status === 302 || r.status === 404 });
}

export function mutate(data) {
  const ws = data.workspaceId;
  const L = data.links;
  const steps = [
    () => mark('UPDATE load0 destination', http.put(api(`/workspaces/${ws}/links/${L[0].id}`),
      JSON.stringify({ destination: 'https://example.com/UPDATED-0' }), { headers: jsonH, tags: { name: 'update' } })),
    () => mark('UPDATE load1 destination', http.put(api(`/workspaces/${ws}/links/${L[1].id}`),
      JSON.stringify({ destination: 'https://example.com/UPDATED-1' }), { headers: jsonH, tags: { name: 'update' } })),
    () => mark('DISABLE load2', http.patch(api(`/workspaces/${ws}/links/${L[2].id}/active`),
      JSON.stringify({ isActive: false }), { headers: jsonH, tags: { name: 'disable' } })),
    () => mark('DELETE load3', http.del(api(`/workspaces/${ws}/links/${L[3].id}`),
      null, { headers: jsonH, tags: { name: 'delete' } })),
  ];
  steps[exec.scenario.iterationInInstance % steps.length]();
  sleep(3);
}

function mark(label, res) {
  mutMs.add(res.timings.duration);
  console.log(`[t+${(exec.instance.currentTestRunDuration / 1000).toFixed(0)}s] ${label} -> ${res.status} in ${res.timings.duration.toFixed(1)}ms`);
}
