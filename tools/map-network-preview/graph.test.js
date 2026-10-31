import test from 'node:test';
import assert from 'node:assert/strict';
import { createGraph } from './graph.js';

const data = { reference: { maximumDays: 65 }, nodes: [
    { id: 80, region: 10, city: 80, kind: 'city' },
    { id: 90, region: 20, city: 80, kind: 'port' },
    { id: 95, region: 30, city: 81, kind: 'fort' },
    { id: 99, region: 40, city: 80, kind: 'facility' },
], connections: [
    { from: 80, to: 90, days: 10 }, { from: 90, to: 80, days: 11 },
    { from: 90, to: 95, days: 30 }, { from: 95, to: 90, days: 30.01 },
] };

test('cutoff includes the exact boundary and does not round in an over-limit route', () => {
    const g = createGraph(data, 30);
    assert.equal(g.days(90, 95), 30);
    assert.equal(g.days(95, 90), undefined);
    assert.deepEqual([...g.outgoing.get(95) ?? []], []);
    assert.deepEqual([...g.incoming.get(95)], [90]);
    assert.equal(createGraph(data, 31).days(95, 90), 30.01);
});

test('administrative membership does not imply a travel edge', () => {
    const g = createGraph(data);
    assert.deepEqual(g.children(80).map(n => n.id), [90, 99]);
    assert.equal(g.neighbors(90).some(n => n.id === 99), false);
    assert.equal(g.nodes.get(90).region, 20);
});

test('reciprocal display lines retain per-direction cutoff results', () => {
    const g = createGraph(data, 30);
    assert.deepEqual(g.lines, [
        { a: 80, b: 90, forward: true, backward: true },
        { a: 90, b: 95, forward: true, backward: false },
    ]);
    assert.equal(g.connectionCount, 3);
});

test('an intermediate facility does not suppress an in-horizon relation', () => {
    const g = createGraph({ ...data, connections: [...data.connections,
        { from: 80, to: 95, days: 40 },
    ] }, 50);
    assert.equal(g.days(80, 90), 10);
    assert.equal(g.days(90, 95), 30);
    assert.equal(g.days(80, 95), 40);
});

test('invalid references/horizons are rejected without changing inputs', () => {
    const before = structuredClone(data);
    assert.equal(createGraph(data).cutoffDays, 60);
    assert.deepEqual(data, before);
    assert.equal(createGraph(data, 65).cutoffDays, 65);
    assert.throws(() => createGraph(data, 66), /horizon/);
    assert.throws(() => createGraph({ ...data, connections: [{ from: 80, to: 999, days: 1 }] }), /Invalid/);
});
