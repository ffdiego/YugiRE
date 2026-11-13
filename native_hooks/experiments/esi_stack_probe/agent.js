/* Injected exclusively into esi_fixture_x86.exe, by run_fixture.py.
 * MODE and TARGET are constants prepended by that Python script.
 */
'use strict';
if (Process.arch !== 'ia32') throw new Error('Synthetic fixture requires ia32');
if (MODE !== 'probe' && MODE !== 'object') throw new Error('Unexpected probe mode');

const target = ptr(TARGET);
let hits = 0;
function observeInstruction() {
  hits++;
  const regs = this.context;
  send({kind: 'hit', mode: MODE, hit: hits,
        esi: regs.esi.toString(), esp: regs.esp.toString(),
        eip: regs.eip.toString(), eax: regs.eax.toString()});
}
// Deliberately different Frida overloads. The object form is the unsafe
// negative control; it may rewrite the saved ESI at [ESP] as an on-leave return.
const listener = MODE === 'probe'
  ? Interceptor.attach(target, observeInstruction)
  : Interceptor.attach(target, {onEnter: observeInstruction});

send({kind: 'armed', mode: MODE, target: target.toString(),
      runtime: (typeof Script === 'undefined') ? null : Script.runtime,
      fridaVersion: (typeof Frida === 'undefined') ? null : Frida.version,
      architecture: Process.arch});
