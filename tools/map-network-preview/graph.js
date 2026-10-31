// Facility travel and administrative membership are separate relations.
export function createGraph(data, cutoffDays = 60) {
    if (!Number.isFinite(cutoffDays) || cutoffDays <= 0 || cutoffDays > data.reference.maximumDays)
        throw new Error('Cutoff outside the prepared travel horizon');
    const nodes = new Map(data.nodes.map(n => [n.id, n]));
    const outgoing = new Map(), incoming = new Map(), durations = new Map();
    const edges = [];
    for (const edge of data.connections) {
        if (!nodes.has(edge.from) || !nodes.has(edge.to) || !Number.isFinite(edge.days) || edge.days < 0)
            throw new Error('Invalid facility travel reference');
        if (edge.days > cutoffDays) continue;
        edges.push(edge);
        if (!outgoing.has(edge.from)) outgoing.set(edge.from, new Set());
        if (!incoming.has(edge.to)) incoming.set(edge.to, new Set());
        outgoing.get(edge.from).add(edge.to); incoming.get(edge.to).add(edge.from);
        durations.set(`${edge.from}:${edge.to}`, edge.days);
    }
    const days = (from, to) => durations.get(`${from}:${to}`);
    const neighbors = id => [...new Set([...(outgoing.get(id) ?? []), ...(incoming.get(id) ?? [])])]
        .map(id => nodes.get(id)).sort((a, b) =>
            Math.min(days(id, a.id) ?? Infinity, days(a.id, id) ?? Infinity) -
            Math.min(days(id, b.id) ?? Infinity, days(b.id, id) ?? Infinity) || a.id - b.id);
    const children = city => data.nodes.filter(n => n.city === city && n.id !== city);
    const lines = new Map();
    for (const edge of edges) {
        const a = Math.min(edge.from, edge.to), b = Math.max(edge.from, edge.to), key = `${a}:${b}`;
        if (!lines.has(key)) lines.set(key, { a, b, forward: false, backward: false });
        const line = lines.get(key);
        if (edge.from === a) line.forward = true; else line.backward = true;
    }
    return { nodes, outgoing, incoming, neighbors, children, days, cutoffDays,
        connectionCount: edges.length, lines: [...lines.values()] };
}
