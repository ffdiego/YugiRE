/*
 * Joey native-function invocation probe (Frida 17+).
 * This instruments code IN MEMORY, but does not replace functions, rewrite
 * arguments or alter return values. Run only against a disposable local copy.
 * It is not a complete behavior trace and may affect timing/stability.
 */
'use strict';

const EXPECTED_PROCESS = 'joey_pc.exe';
const DOCUMENTED_BASE = ptr('0x00400000');
const targets = [
  { name: 'InvokePrimaryCardCallback', va: '0x0059DEB0' },
  { name: 'QueueDuelCommand', va: '0x005B9170' },
  { name: 'CalculateCardAttributes', va: '0x0056DD30' },
];

if (Process.platform !== 'windows' || Process.arch !== 'ia32') {
  throw new Error('Expected a native Windows 32-bit (ia32) process');
}
const main = Process.mainModule;
if (main.name.toLowerCase() !== EXPECTED_PROCESS) {
  throw new Error('Expected ' + EXPECTED_PROCESS + ', got ' + main.name);
}

const counters = [];
for (const target of targets) {
  const offset = ptr(target.va).sub(DOCUMENTED_BASE);
  const address = main.base.add(offset);
  const range = Process.findRangeByAddress(address);
  if (range === null || !range.protection.includes('x')) {
    throw new Error('Function has no executable mapping: ' + target.name + ' at ' + address);
  }
  const entry = { name: target.name, va: target.va, actual: address.toString(), calls: 0 };
  counters.push(entry);
  Interceptor.attach(address, {
    onEnter() {
      entry.calls++;
      if (entry.calls <= 3) {
        // Raw register values only. No guessed ABI, card IDs or memory dumps.
        send({ kind: 'first_call', name: entry.name, count: entry.calls,
               threadId: this.threadId,
               ecx: this.context.ecx.toString(),
               edx: this.context.edx.toString(),
               returnAddress: this.returnAddress.toString() });
      }
    },
  });
}

send({ kind: 'ready', pid: Process.id, arch: Process.arch,
       moduleName: main.name, actualBase: main.base.toString(), targets: counters });

setInterval(() => {
  send({ kind: 'counts', rows: counters.map(c => ({ name: c.name, calls: c.calls })) });
}, 5000);
