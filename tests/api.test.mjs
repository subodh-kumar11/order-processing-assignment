import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { createServer } from 'node:net';

const root = resolve(import.meta.dirname, '..');
let server, base, directory, logs = '';
async function start(interval = 300) {
  const probe = createServer();
  await new Promise(r => probe.listen(0, '127.0.0.1', r));
  const port = probe.address().port;
  await new Promise(r => probe.close(r));
  base = `http://127.0.0.1:${port}`;
  server = spawn('dotnet', [join(root, 'bin/Release/net10.0/OrderProcessing.dll')], {
    cwd: root,
    env: { ...process.env, ASPNETCORE_URLS: base, Orders__DataPath: join(directory, 'orders.json'),
      Orders__ProcessingIntervalSeconds: String(interval) },
    stdio: ['ignore', 'pipe', 'pipe']
  });
  server.stdout.on('data', b => { logs += b; });
  server.stderr.on('data', b => { logs += b; });
  server.on('error', e => { logs += e; });
  for (let i = 0; i < 100; i++) {
    try { if ((await fetch(`${base}/health`)).ok) return; } catch {}
    if (server.exitCode !== null) throw new Error(logs);
    await new Promise(r => setTimeout(r, 100));
  }
  throw new Error(`Server did not start: ${logs}`);
}
async function stop() {
  if (!server || server.exitCode !== null) return;
  await new Promise(resolve => { server.once('exit', resolve); server.kill(); });
}
async function api(path, method = 'GET', body) {
  const response = await fetch(base + path, { method,
    headers: body === undefined ? {} : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body) });
  const text = await response.text();
  let data; try { data = JSON.parse(text); } catch { data = text; }
  return { status: response.status, data, location: response.headers.get('location') };
}
const payload = () => ({ customerId: 'customer-1', items: [
  { productId: 'book', quantity: 2, unitPrice: 12.50 },
  { productId: 'pen', quantity: 3, unitPrice: 0.10 }
] });
const create = () => api('/orders', 'POST', payload());
before(async () => { directory = await mkdtemp(join(tmpdir(), 'orders-test-')); await start(); });
after(async () => { await stop(); await rm(directory, { recursive: true, force: true }); });

test('order lifecycle, validation, persistence, and scheduled processing', async t => {
  let id;
  await t.test('creates multiple items with exact total, location and timestamps', async () => {
    const result = await create();
    assert.equal(result.status, 201);
    id = result.data.id;
    assert.equal(result.location, `/orders/${id}`);
    assert.equal(result.data.total, 25.3);
    assert.equal(result.data.status, 'PENDING');
    assert.equal(result.data.items.length, 2);
    assert.ok(Date.parse(result.data.createdAt));
    assert.deepEqual((await api(`/orders/${id}`)).data, result.data);
  });
  await t.test('rejects invalid items and missing required fields', async () => {
    const bad = [ {}, { ...payload(), customerId: ' ' }, { ...payload(), items: [] },
      { ...payload(), items: [null] },
      ...[0, -1, 10001, 1.5].map(quantity => ({ ...payload(), items: [{ productId: 'x', quantity, unitPrice: 1 }] })),
      ...[0, -1, 1.001, 1000001].map(unitPrice => ({ ...payload(), items: [{ productId: 'x', quantity: 1, unitPrice }] })),
      { ...payload(), items: [payload().items[0], payload().items[0]] }
    ];
    for (const body of bad) assert.equal((await api('/orders', 'POST', body)).status, 400);
    for (const status of [5, 'UNKNOWN', null])
      assert.equal((await api(`/orders/${id}/status`, 'PATCH', { status })).status, 400);
    assert.equal((await api(`/orders/${id}/status`, 'PATCH', {})).status, 400);
    const malformed = await fetch(base + '/orders', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{' });
    assert.equal(malformed.status, 400);
  });
  await t.test('unknown orders return 404 for reads, updates and cancellations', async () => {
    const path = '/orders/00000000-0000-0000-0000-000000000001';
    assert.equal((await api(path)).status, 404);
    assert.equal((await api(path + '/status', 'PATCH', { status: 'PROCESSING' })).status, 404);
    assert.equal((await api(path + '/cancel', 'POST')).status, 404);
  });
  await t.test('enforces forward transitions and terminal states', async () => {
    assert.equal((await api(`/orders/${id}/status`, 'PATCH', { status: 'SHIPPED' })).status, 409);
    for (const status of ['PROCESSING', 'SHIPPED', 'DELIVERED']) {
      const result = await api(`/orders/${id}/status`, 'PATCH', { status });
      assert.equal(result.status, 200);
      assert.equal(result.data.status, status);
      assert.equal((await api(`/orders/${id}/cancel`, 'POST')).status, 409);
    }
    assert.equal((await api(`/orders/${id}/status`, 'PATCH', { status: 'PENDING' })).status, 409);
    assert.equal((await api(`/orders/${id}/status`, 'PATCH', { status: 'DELIVERED' })).status, 200);
  });
  await t.test('cancels pending orders with safe retries', async () => {
    const pending = (await create()).data;
    assert.equal((await api(`/orders/${pending.id}/cancel`, 'POST')).data.status, 'CANCELLED');
    assert.equal((await api(`/orders/${pending.id}/cancel`, 'POST')).status, 200);
    assert.equal((await api(`/orders/${pending.id}/status`, 'PATCH', { status: 'PROCESSING' })).status, 409);
  });
  await t.test('filters and paginates, rejecting invalid queries', async () => {
    await create();
    const result = await api('/orders?status=PENDING&limit=1&offset=0');
    assert.equal(result.data.orders.length, 1);
    assert.ok(result.data.orders.every(o => o.status === 'PENDING'));
    assert.ok(result.data.total >= 1);
    assert.deepEqual((await api('/orders?offset=99999')).data.orders, []);
    for (const query of ['status=invalid', 'status=0', 'limit=0', 'limit=101', 'offset=-1'])
      assert.equal((await api('/orders?' + query)).status, 400);
  });
  await t.test('simultaneous cancellation and processing have one winner', async () => {
    const order = (await create()).data;
    const results = await Promise.all([
      api(`/orders/${order.id}/cancel`, 'POST'),
      api(`/orders/${order.id}/status`, 'PATCH', { status: 'PROCESSING' })
    ]);
    assert.deepEqual(results.map(r => r.status).sort(), [200, 409]);
  });
  await t.test('concurrent creates do not lose writes', async () => {
    const before = (await api('/orders')).data.total;
    const results = await Promise.all(Array.from({ length: 20 }, create));
    assert.ok(results.every(r => r.status === 201));
    assert.equal(new Set(results.map(r => r.data.id)).size, 20);
    assert.equal((await api('/orders')).data.total, before + 20);
  });
  await t.test('persists across restart and worker processes only pending orders', async () => {
    const snapshot = (await api('/orders?limit=100')).data;
    await stop();
    await start(1);
    assert.deepEqual((await api(`/orders/${id}`)).data, snapshot.orders.find(o => o.id === id));
    assert.equal((await api('/orders')).data.total, snapshot.total);
    for (let i = 0; i < 50; i++) {
      if ((await api('/orders?status=PENDING')).data.total === 0) break;
      await new Promise(r => setTimeout(r, 100));
    }
    assert.equal((await api('/orders?status=PENDING')).data.total, 0);
    for (const old of snapshot.orders) {
      const current = (await api(`/orders/${old.id}`)).data;
      assert.equal(current.status, old.status === 'PENDING' ? 'PROCESSING' : old.status);
    }
    const racers = await Promise.all(Array.from({ length: 10 }, create));
    for (const result of racers) {
      const cancelled = await api(`/orders/${result.data.id}/cancel`, 'POST');
      assert.ok([200, 409].includes(cancelled.status));
    }
    await new Promise(r => setTimeout(r, 1200));
    for (const result of racers)
      assert.ok(['CANCELLED', 'PROCESSING'].includes((await api(`/orders/${result.data.id}`)).data.status));
  });
});
