/*
 * Third experiment: bounded observation of selected Joey native callbacks.
 * Verified exe fingerprint preflight is required. Frida attaches temporary
 * in-memory hooks. No interception of return values or native code replacement.
 */
'use strict';

const DOC_BASE = ptr('0x00400000');
const MAX_PER_FUNCTION = 48;
const TARGETS = [
  { name: 'PrimaryDispatcher', va: '0x0059DEB0', requestArg: true },
  { name: 'MorphingJarPrimary', va: '0x005860E0', requestArg: false },
  { name: 'SanganPrimary', va: '0x00583D20', requestArg: false },
  { name: 'MagicJammerPrimary', va: '0x00588D90', requestArg: false },
  { name: 'JamBreedingMachinePrimary', va: '0x00583F30', requestArg: false },
];

if (Process.platform !== 'windows' || Process.arch !== 'ia32') {
  throw new Error('Windows PE32 x86 process required');
}
const main = Process.mainModule;
if (main.name.toLowerCase() !== 'joey_pc.exe') {
  throw new Error('Unexpected process main module: ' + main.name);
}

function absoluteAddress(va) {
  const address = main.base.add(ptr(va).sub(DOC_BASE));
  const range = Process.findRangeByAddress(address);
  if (range === null || !range.protection.includes('x')) {
    throw new Error('Address is not executable: ' + va);
  }
  return address;
}

function readRequest20(candidate) {
  try {
    if (candidate.isNull()) return null;
    const range = Process.findRangeByAddress(candidate);
    if (range === null || !range.protection.includes('r')) return null;
    if (candidate.add(20).compare(range.base.add(range.size)) > 0) return null;
    const bytes = new Uint8Array(candidate.readByteArray(20));
    return Array.from(bytes).map(x => x.toString(16).padStart(2, '0')).join('');
  } catch (_e) {
    return null;
  }
}

// A single short/unsupported function must not abort all other probes.
const counts = [];
const statuses = [];
for (const target of TARGETS) {
  const counter = { name: target.name, calls: 0 };
  try {
    const address = absoluteAddress(target.va);
    Interceptor.attach(address, {
      onEnter(args) {
        counter.calls++;
        this.callNo = counter.calls;
        this.shouldLog = this.callNo <= MAX_PER_FUNCTION;
        if (!this.shouldLog) return;
        this.requestPointer = args[0];
        this.before20 = readRequest20(args[0]);
        send({ kind: 'enter', function: target.name, call: this.callNo,
               thread: this.threadId, returnAddress: this.returnAddress.toString(),
               arg0: args[0].toString(), arg1: args[1].toString(),
               ecx: this.context.ecx.toString(), edx: this.context.edx.toString(),
               maybeRequest20Hex: this.before20,
               requestAssumption: target.requestArg ? 'verified_first_stack_argument' : 'unknown' });
      },
      onLeave(retval) {
        if (!this.shouldLog) return;
        const after20 = readRequest20(this.requestPointer);
        send({ kind: 'leave', function: target.name, call: this.callNo,
               returnEax: retval.toString(), maybeRequest20HexAfter: after20,
               requestBytesChanged: this.before20 !== null && after20 !== null && this.before20 !== after20 });
      },
    });
    counts.push(counter);
    statuses.push({ name: target.name, va: target.va, status: 'attached' });
  } catch (error) {
    // Some valid x86 functions cannot be patched by Frida's Interceptor.
    // Do not pretend that this hook worked.
    let first16Bytes = null;
    try {
      const address = absoluteAddress(target.va);
      const bytes = new Uint8Array(address.readByteArray(16));
      first16Bytes = Array.from(bytes).map(x => x.toString(16).padStart(2, '0')).join('');
    } catch (_e) {
      // Binary bytes are diagnostic only; absence is not a fatal error.
    }
    const skipped = { name: target.name, va: target.va,
                      status: 'skipped', error: String(error),
                      first16Bytes };
    statuses.push(skipped);
    send({ kind: 'hook_skipped', ...skipped });
  }
}

send({ kind: 'ready_handlers', pid: Process.id,
       actualBase: main.base.toString(), targets: statuses,
       maxPerFunction: MAX_PER_FUNCTION,
       note: 'Skipped hooks do not contribute to counts.' });

if (counts.length > 0) {
  setInterval(() => send({ kind: 'totals', counts }), 10000);
}
