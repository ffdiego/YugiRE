/*
 * Joey experimental ABI observer — Frida 17+ / PE32 x86 / exact known EXE.
 * Read-only observations of requests and queued commands. Interceptor.attach
 * patches code in memory temporarily; it is NOT external read-only polling.
 * No function replacement, no argument rewriting and no raw RAM dump.
 */
'use strict';

const DOCUMENTED_BASE = ptr('0x00400000');
const MAX_PRIMARY = 48;
const MAX_QUEUE = 48;
const primaryVA = ptr('0x0059DEB0');
const queueVA = ptr('0x005B9170');

if (Process.platform !== 'windows' || Process.arch !== 'ia32') {
  throw new Error('This probe requires a Windows 32-bit x86 process.');
}
const module = Process.mainModule;
if (module.name.toLowerCase() !== 'joey_pc.exe') {
  throw new Error('Unexpected main module: ' + module.name);
}

function addressOf(va) {
  const address = module.base.add(va.sub(DOCUMENTED_BASE));
  const range = Process.findRangeByAddress(address);
  if (range === null || !range.protection.includes('x')) {
    throw new Error('Not executable: ' + address);
  }
  return address;
}

function requestWords(pointerValue) {
  try {
    if (pointerValue.isNull()) return { readable: false, reason: 'null' };
    const range = Process.findRangeByAddress(pointerValue);
    if (range === null || !range.protection.includes('r') ||
        pointerValue.add(8).compare(range.base.add(range.size)) > 0) {
      return { readable: false, reason: 'outside_readable_region' };
    }
    const words = [];
    for (let i = 0; i < 4; i++) {
      words.push(pointerValue.add(i * 2).readU16());
    }
    return {
      readable: true,
      word0_card_index: words[0],
      word1_hex: '0x' + words[1].toString(16).padStart(4, '0'),
      word2_hex: '0x' + words[2].toString(16).padStart(4, '0'),
      word3_hex: '0x' + words[3].toString(16).padStart(4, '0'),
    };
  } catch (error) {
    return { readable: false, reason: 'exception', error: String(error) };
  }
}

let primaryCalls = 0;
let queueCalls = 0;

Interceptor.attach(addressOf(primaryVA), {
  onEnter(args) {
    const call = ++primaryCalls;
    this.logged = call <= MAX_PRIMARY;
    if (!this.logged) return;
    // At 0x0059DEB0: push esi; mov esi, [esp+8]; mov ax, [esi].
    // That makes the first stack argument a pointer to a request whose
    // first uint16 is a property index (docs/13-parasite-creation.md).
    this.call = call;
    send({
      kind: 'primary_enter', call, threadId: this.threadId,
      returnAddress: this.returnAddress.toString(),
      requestPointer: args[0].toString(),
      arg1_raw: args[1].toString(),
      request: requestWords(args[0]),
    });
  },
  onLeave(retval) {
    if (!this.logged) return;
    send({ kind: 'primary_leave', call: this.call,
           return_eax_raw: retval.toString() });
  },
});

Interceptor.attach(addressOf(queueVA), {
  onEnter(args) {
    const call = ++queueCalls;
    if (call > MAX_QUEUE) return;
    // docs/11-command-4d.md: four arguments are stored as uint16 words.
    // Do not assign semantics to the other three words here.
    const words = [];
    for (let i = 0; i < 4; i++) words.push(args[i].toUInt32() & 0xffff);
    send({ kind: 'queued_command', call, threadId: this.threadId,
           returnAddress: this.returnAddress.toString(),
           opcode_base_hex: '0x' + (words[0] & 0x0fff).toString(16),
           side_bit: (words[0] & 0x8000) !== 0 ? 1 : 0,
           words_hex: words.map(w => '0x' + w.toString(16).padStart(4, '0')) });
  },
});

send({kind: 'ready_abi', pid: Process.id, actualBase: module.base.toString(),
      primary: addressOf(primaryVA).toString(),
      queue: addressOf(queueVA).toString(),
      primaryLimit: MAX_PRIMARY, queueLimit: MAX_QUEUE,
      warning: 'No native ABI contract proven; no functions replaced.'});

setInterval(() => {
  send({kind: 'totals', primaryCalls, queueCalls,
        loggedPrimary: Math.min(primaryCalls, MAX_PRIMARY),
        loggedQueue: Math.min(queueCalls, MAX_QUEUE)});
}, 10000);
