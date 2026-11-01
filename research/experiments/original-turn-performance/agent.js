'use strict';

// Measurement only: no game calls, return-value changes, speed changes or save writes.
const nativeSource = `
#include <gum/guminterceptor.h>
extern int __attribute__((stdcall)) QueryPerformanceCounter(void *out);
extern unsigned int __attribute__((stdcall)) GetCurrentThreadId(void);
typedef struct { int active; int index; double begin; double child; } Frame;
extern double stats[32][5];
extern Frame *stack[512];
extern int depth, overflow, cut;
extern unsigned int selected;
extern volatile int dummy;
extern double calibration_ticks;
double clock_ticks(void) { gint64 n = 0; QueryPerformanceCounter(&n); return (double)n; }
void clear(void) {
  int i, j; selected = 0; depth = 0; overflow = 0; cut = 0;
  for (i = 0; i < 32; i++) for (j = 0; j < 5; j++) stats[i][j] = 0;
}
void enable(unsigned int tid) { selected = tid; }
static void finish(Frame *f, double now) {
  double elapsed = now - f->begin;
  double *s = stats[f->index];
  s[0] += 1; s[1] += elapsed; s[2] += elapsed - f->child;
  if (elapsed > s[3]) s[3] = elapsed;
  f->active = 0;
  if (depth > 0) stack[depth - 1]->child += elapsed;
}
void stop(void) {
  double now = clock_ticks(); selected = 0;
  while (depth > 0) { Frame *f = stack[--depth]; cut++; finish(f, now); }
}
void *get_stats(void) { return stats; }
int get_overflow(void) { return overflow; }
int get_cut(void) { return cut; }
void on_enter(GumInvocationContext *ic) {
  Frame *f = gum_invocation_context_get_listener_invocation_data(ic, sizeof(Frame));
  f->active = 0;
  if (!selected || GetCurrentThreadId() != selected) return;
  if (depth >= 512) { overflow++; return; }
  f->index = (int)(gsize)gum_invocation_context_get_listener_function_data(ic);
  f->active = 1; f->child = 0; stack[depth++] = f; f->begin = clock_ticks();
}
void on_leave(GumInvocationContext *ic) {
  Frame *f = gum_invocation_context_get_listener_invocation_data(ic, sizeof(Frame));
  if (!f->active) return;
  double now = clock_ticks(); depth--; finish(f, now);
}
void calibration_inner(void) { dummy++; dummy++; dummy++; dummy++; }
void calibration_outer(void) { calibration_inner(); calibration_inner(); }
void calibration_run(int n) { int i; for (i = 0; i < n; i++) calibration_outer(); }
unsigned int __attribute__((stdcall)) calibration_worker(void *arg) {
  double begin; clear(); enable(GetCurrentThreadId()); begin = clock_ticks();
  calibration_run(5000); calibration_ticks = clock_ticks() - begin; stop(); return 0;
}
`;

const kernel = Process.getModuleByName('kernel32.dll');
const winAbi = Process.arch === 'ia32' ? 'stdcall' : 'win64';
const qpf = new NativeFunction(kernel.getExportByName('QueryPerformanceFrequency'), 'int', ['pointer'], winAbi);
const qpc = new NativeFunction(kernel.getExportByName('QueryPerformanceCounter'), 'int', ['pointer'], winAbi);
const getTimes = new NativeFunction(kernel.getExportByName('GetThreadTimes'), 'int',
    ['pointer', 'pointer', 'pointer', 'pointer', 'pointer'], winAbi);
const frequencyBuffer = Memory.alloc(8);
qpf(frequencyBuffer);
const frequency = Number(frequencyBuffer.readS64().toString());
const nativeStorage = { stats: Memory.alloc(32 * 40), stack: Memory.alloc(512 * Process.pointerSize),
    depth: Memory.alloc(4), overflow: Memory.alloc(4), cut: Memory.alloc(4), selected: Memory.alloc(4), dummy: Memory.alloc(4), calibration_ticks: Memory.alloc(8) };
const cm = new CModule(Process.arch === 'ia32' ? nativeSource : nativeSource.replaceAll('__attribute__((stdcall))', ''), {
    ...nativeStorage,
    QueryPerformanceCounter: kernel.getExportByName('QueryPerformanceCounter'),
    GetCurrentThreadId: kernel.getExportByName('GetCurrentThreadId')
});
const clear = new NativeFunction(cm.clear, 'void', []);
const enable = new NativeFunction(cm.enable, 'void', ['uint']);
const stop = new NativeFunction(cm.stop, 'void', []);
const getStats = new NativeFunction(cm.get_stats, 'pointer', []);
const getOverflow = new NativeFunction(cm.get_overflow, 'int', []);
const getCut = new NativeFunction(cm.get_cut, 'int', []);
const createThread = new NativeFunction(kernel.getExportByName('CreateThread'), 'pointer', ['pointer', 'size_t', 'pointer', 'pointer', 'uint', 'pointer'], winAbi);
const waitThread = new NativeFunction(kernel.getExportByName('WaitForSingleObject'), 'uint', ['pointer', 'uint'], winAbi);
const closeHandle = new NativeFunction(kernel.getExportByName('CloseHandle'), 'int', ['pointer'], winAbi);
function calibrationRun() {
    const worker = Process.arch === 'ia32' ? cm['_calibration_worker@4'] : cm.calibration_worker;
    if (worker === undefined) throw new Error('Missing native worker export: ' + Object.keys(cm).join(','));
    const handle = createThread(NULL, 0, worker, NULL, 0, NULL);
    if (handle.isNull()) throw new Error('Calibration CreateThread failed');
    if (waitThread(handle, 10000) !== 0) throw new Error('Calibration worker failed to finish');
    closeHandle(handle);
    return nativeStorage.calibration_ticks.readDouble();
}
const scratch = Memory.alloc(40);
function ticks() { qpc(scratch); return Number(scratch.readS64().toString()); }
function threadCpuMs() {
    if (!getTimes(ptr('0xfffffffe'), scratch.add(8), scratch.add(16), scratch.add(24), scratch.add(32)))
        throw new Error('GetThreadTimes failed');
    return (Number(scratch.add(24).readU64().toString()) + Number(scratch.add(32).readU64().toString())) / 10000;
}

// Exact SRC-SAN9PK-TC-101 addresses verified against the project IDA database.
const probes = [
    [0x485d20, 'task_dispatch', '568bf18b46303dea0300007f'],
    [0x507da0, 'xun_dispatch', '6aff68d7195f0064a1000000'],
    [0x50ec90, 'force_dispatch', '6aff64a10000000068e01e5f'],
    [0x4bde30, 'ai_dispatch', '568bf18b46345733ff3de803'],
    [0x4bdae0, 'ai_strategy_preparation', '6aff68f89c5e0064a1000000'],
    [0x4bd030, 'ai_work_factory', '64a1000000006aff68dd9c5e'],
    [0x4af420, 'ai_bounded_work', '568bf18b464085c07c0583f8'],
    [0x50a170, 'movement_dispatch', '568bf18b46343dea0300007f'],
    [0x50e8f0, 'daily_maintenance', '568bf1e858ffffff8bcee8a1'],
    [0x509af0, 'daily_commands_and_combat_dispatch', '6aff689e1b5f0064a1000000'],
    [0x506500, 'tactic_dispatch', '64a1000000006aff6880145f'],
    [0x484e70, 'xun_world_updates', '568bf1e898ebffff8bcee891'],
    [0x49cf30, 'unit_movement', '83ec28568b7138837e240689'],
    [0x49b460, 'normal_attack_dispatch', '6aff68e6715e0064a1000000'],
    [0x478290, 'square_path_search', '568b742408578bf98bcee841'],
    [0x50a340, 'date_and_map_update', '56576a006a006a018bf9e801']
];
let armed = false, current = null, runNumber = 0, label = '', initialized = false, detailed = false, opening = false;
const actorTasks = new Map();
let moduleBase, calibration;
function address(va) { return moduleBase.add(va - 0x400000); }
function bytes(at, length) { return Array.from(new Uint8Array(at.readByteArray(length)), b => b.toString(16).padStart(2, '0')).join(''); }
function date() {
    const code = address(0x123269c).readS32();
    return { code, year: Math.floor(code / 360) + 130, month: Math.floor((code % 360) / 30) + 1, day: code % 30 + 1 };
}
function emit(kind, fields) { send({ kind, utc: new Date().toISOString(), ...fields }); }
function rows() {
    const base = getStats();
    return (detailed ? probes : []).map((p, i) => {
        const at = base.add(i * 40);
        return { name: p[1], va: '0x' + p[0].toString(16), calls: at.readDouble(),
            inclusive_ms: at.add(8).readDouble() * 1000 / frequency,
            exclusive_ms: at.add(16).readDouble() * 1000 / frequency,
            maximum_ms: at.add(24).readDouble() * 1000 / frequency };
    });
}
function leaderNameHex(person) {
    return person.isNull() ? null : bytes(person.add(0x10), 10);
}
function forceIdentity(force) {
    if (force.isNull()) throw new Error('Null automatic force');
    const slot = force.sub(address(0x1253c38)).toInt32() / 0xd4;
    if (!Number.isInteger(slot) || slot < 0 || slot >= 50) throw new Error('Force pointer outside registered table');
    const root = force.add(0xb8).readPointer();
    const rootSlot = root.sub(address(0x1253c38)).toInt32() / 0xd4;
    if (!Number.isInteger(rootSlot) || rootSlot < 0 || rootSlot >= 50) throw new Error('Invalid root force pointer');
    return { slot, root_slot: rootSlot, is_root: root.equals(force),
        leader_name_big5_hex: leaderNameHex(force.add(0xbc).readPointer()),
        ruler_name_big5_hex: leaderNameHex(root.add(0xbc).readPointer()), flags: force.add(0x34).readU32() };
}
function openingError(error) { if (current !== null) current.opening_errors.push(String(error)); }
function installOpeningHooks() {
    const timed = [
        [0x4bdae0, 'strategy_work62', '6aff68f89c5e0064a1000000', true],
        [0x4bdb40, 'personnel_work22', '6aff681b9d5e0064a1000000', true],
        [0x4bdbd0, 'combined_corps_preparation', '568bf1e808ffffff8bce5ee9', true],
        [0x4be7e0, 'global_preparation', '568bf1e8f8f7ffff8bcee8d1', false],
        [0x4bdfe0, 'global_record_refresh', '6aff68489d5e0064a1000000', false],
        [0x4be2c0, 'global_strategy_groups', '6aff688e9d5e0064a1000000', false],
        [0x4be670, 'global_facility_unit_refresh', '64a1000000006aff68a89d5e', false],
        [0x484e70, 'xun_world_updates', '568bf1e898ebffff8bcee891', false],
        [0x484300, 'force_emergence_independence', '568bf1e878eeffff8bce5ee9', false],
        [0x483140, 'xun_cleanup', '568bf1e818d9ffff8bcee821', false]
    ];
    const lifecycle = [
        [0x4bcfc0, 'automatic_actor_ctor', '8b442408568bf18b4c240850'],
        [0x4bd000, 'automatic_actor_dtor', 'c701cc986000e9250f000090'],
        [0x50a240, 'daily_simulation_ctor', '8b44240456508bf1e893ddff']
    ];
    for (const [va, name, signature] of timed.concat(lifecycle))
        if (bytes(address(va), signature.length / 2) !== signature) throw new Error('Opening signature mismatch: ' + name);
    for (const [va, name, , corps] of timed) Interceptor.attach(address(va), {
        onEnter() {
            if (current === null) return;
            try {
                this.event = { name, date: date(), ...(corps ? forceIdentity(this.context.ecx.add(0x30).readPointer()) : {}),
                    start_ticks: ticks() };
            } catch (e) { openingError(e); }
        },
        onLeave() {
            if (!this.event || current === null) return;
            const endTicks = ticks(), event = this.event;
            event.elapsed_ms = (endTicks - event.start_ticks) * 1000 / frequency;
            event.since_start_ms = (event.start_ticks - current.start_ticks) * 1000 / frequency;
            delete event.start_ticks;
            current.opening_events.push(event);
        }
    });
    Interceptor.attach(address(0x4bcfc0), {
        onLeave(task) {
            if (current === null) return;
            try {
                if (actorTasks.size) current.opening_errors.push('Overlapping automatic actor lifetimes');
                actorTasks.set(task.toString(), { ...forceIdentity(task.add(0x30).readPointer()), date: date(),
                    baseline: rows(), start_cpu_ms: threadCpuMs(), start_ticks: ticks() });
            } catch (e) { openingError(e); }
        }
    });
    Interceptor.attach(address(0x4bd000), {
        onEnter() {
            if (current === null) return;
            const key = this.context.ecx.toString(), actor = actorTasks.get(key);
            if (!actor) { current.opening_errors.push('Automatic actor destructor without measured constructor'); return; }
            const endTicks = ticks(), cpu = threadCpuMs();
            actorTasks.delete(key);
            const after = rows();
            actor.probes = after.map((p, i) => ({ name: p.name, calls: p.calls - actor.baseline[i].calls,
                inclusive_ms: p.inclusive_ms - actor.baseline[i].inclusive_ms,
                exclusive_ms: p.exclusive_ms - actor.baseline[i].exclusive_ms }));
            actor.elapsed_ms = (endTicks - actor.start_ticks) * 1000 / frequency;
            actor.thread_cpu_ms = cpu - actor.start_cpu_ms;
            actor.since_start_ms = (actor.start_ticks - current.start_ticks) * 1000 / frequency;
            delete actor.start_ticks; delete actor.start_cpu_ms; delete actor.baseline;
            current.automatic_actors.push(actor);
        }
    });
    Interceptor.attach(address(0x50a240), {
        onEnter() {
            if (current === null) return;
            current.ai_phase_end = { date: date(), since_start_ms: (ticks() - current.start_ticks) * 1000 / frequency,
                thread_cpu_ms: threadCpuMs() - current.start_cpu_ms, actors_completed: current.automatic_actors.length,
                actors_active: actorTasks.size };
            emit('ai_phase_end', { run: current.run, ...current.ai_phase_end });
        }
    });
}
function begin(tid) {
    if (!armed) return;
    if (current !== null) end('superseded', false);
    clear();
    actorTasks.clear();
    current = { run: ++runNumber, label, thread: tid, start_date: date(),
        started_utc: new Date().toISOString(), start_ticks: ticks(), start_cpu_ms: threadCpuMs(), days: [],
        ...(opening ? { automatic_actors: [], opening_events: [], opening_errors: [] } : {}) };
    enable(tid);
    emit('turn_begin', { run: current.run, label, thread: tid, date: current.start_date });
}
function end(reason, completed) {
    if (current === null) return;
    if (current.aborted) { reason = 'operator_aborted'; completed = false; }
    const endTicks = ticks(), cpu = threadCpuMs();
    stop();
    const result = { ...current, reason, completed, end_date: date(),
        mode: opening ? 'opening' : detailed ? 'detail' : 'boundary',
        elapsed_ms: (endTicks - current.start_ticks) * 1000 / frequency,
        main_thread_cpu_ms: cpu - current.start_cpu_ms, probes: rows(),
        truncated_outer_callbacks: getCut(), stack_overflow: getOverflow() };
    delete result.start_ticks; delete result.start_cpu_ms;
    current = null;
    emit('turn_end', result);
}
function calibrate() {
    const n = 5000;
    const bare = calibrationRun();
    const outer = Interceptor.attach(cm.calibration_outer, { onEnter: cm.on_enter, onLeave: cm.on_leave }, ptr(30));
    const inner = Interceptor.attach(cm.calibration_inner, { onEnter: cm.on_enter, onLeave: cm.on_leave }, ptr(31));
    Interceptor.flush();
    const hooked = calibrationRun();
    const s = getStats();
    const outerCount = s.add(30 * 40).readDouble(), innerCount = s.add(31 * 40).readDouble();
    const outerTotal = s.add(30 * 40 + 8).readDouble();
    const selfTotal = s.add(30 * 40 + 16).readDouble() + s.add(31 * 40 + 16).readDouble();
    outer.detach(); inner.detach(); Interceptor.flush();
    if (outerCount !== n || innerCount !== 2 * n || Math.abs(outerTotal - selfTotal) > 1 || getOverflow())
        throw new Error('Native nested profiler self-test failed: ' + JSON.stringify({ outerCount, innerCount, outerTotal, selfTotal }));
    clear();
    return { iterations: n, callbacks: 3 * n, bare_ms: bare * 1000 / frequency,
        instrumented_ms: hooked * 1000 / frequency, mean_added_us_per_call: (hooked - bare) * 1e6 / frequency / (3 * n),
        nested_counts_verified: true, exclusive_sum_verified: true };
}
rpc.exports = {
    initialize(selfTest, mode) {
        if (initialized) throw new Error('Already initialized');
        initialized = true;
        detailed = mode === 'detail' || mode === 'opening';
        opening = mode === 'opening';
        calibration = calibrate();
        if (selfTest) return { calibration, frida: Frida.version, arch: Process.arch };
        if (Process.arch !== 'ia32') throw new Error('Expected the 32-bit original');
        moduleBase = Process.mainModule.base;
        const checks = probes.concat([
            [0x50fe80, 'accepted_advance', 'e88b81ffff85c0741c8b108b'],
            [0x50f460, 'strategy_input', '56e8caffffff8bf085f67430']
        ]);
        for (const [va, name, signature] of checks)
            if (bytes(address(va), signature.length / 2) !== signature) throw new Error('Signature mismatch: ' + name);
        (detailed ? probes : []).forEach((p, i) => Interceptor.attach(address(p[0]),
            { onEnter: cm.on_enter, onLeave: cm.on_leave }, ptr(i)));
        if (opening) installOpeningHooks();
        Interceptor.attach(address(0x50fe80), { onEnter() {
            if (this.returnAddress.equals(address(0x51045c))) begin(this.threadId);
        } });
        Interceptor.attach(address(0x50f460), {
            onEnter(args) { this.finish = current !== null && this.returnAddress.equals(address(0x516617)) && args[0].toInt32() === 1; },
            onLeave() { if (this.finish) end('strategy_input_enabled', true); }
        });
        Interceptor.attach(address(0x50a340), { onLeave() {
            if (current !== null) {
                const d = { ...date(), since_start_ms: (ticks() - current.start_ticks) * 1000 / frequency,
                    thread_cpu_ms: threadCpuMs() - current.start_cpu_ms };
                current.days.push(d); emit('day', { run: current.run, date: d });
            }
        } });
        Interceptor.flush();
        const result = { pid: Process.id, path: Process.mainModule.path, base: moduleBase.toString(),
            frida: Frida.version, arch: Process.arch, mode, frequency, probes: (detailed ? probes : []).map(p => ({ va: p[0], name: p[1] })), calibration, armed };
        emit('ready', result); return result;
    },
    arm(value) { if (current !== null) throw new Error('Cannot arm during an active turn'); label = value || ''; armed = true; emit('armed', { label }); return { armed, label }; },
    disarm() { if (current !== null) throw new Error('Active turn must finish or be explicitly aborted'); armed = false; emit('disarmed', {}); },
    status() { return { armed, label, run: runNumber, active: current === null ? null : { run: current.run, start_date: current.start_date, days: current.days } }; },
    abort() { if (current !== null) current.aborted = true; armed = false; }
};
