/*
 * Frida 17 adapter: selectively redirects Sparks and Raimei to C++ DLL x86.
 * Only edits two PRIMARY pointer slots, not the shared original function.
 * Runtime effect code is always in the DLL; JS only installs/monitors/restores.
 * Loaded by patch_dual_rules.py after hash / PE preflight.
 *
 * The DLL is NEVER unloaded from Joey: an in-flight callback may still be
 * executing while the function-pointer table is restored. Restart the game
 * before a fresh test if Frida exits unexpectedly.
 */
'use strict';
if (Process.platform !== 'windows' || Process.arch !== 'ia32') {
  throw new Error('Requires Windows x86 (ia32)');
}
const main = Process.mainModule;
if (main.name.toLowerCase() !== 'joey_pc.exe') {
  throw new Error('Unexpected process: ' + main.name);
}

const DOCUMENTED_BASE = ptr('0x00400000');
function va(value) {
  return main.base.add(ptr(value).sub(DOCUMENTED_BASE));
}
const kTable = va('0x005ED0A8');
const kStride = 24;
const kPrimaryOriginal = va('0x00584E60');
const kTargetPreparation = va('0x00593CC0');
const kDamageEmitter = va('0x005784E0');
const kInitMagic = 0x59554732; // YUG2
const kAbiVersion = 1;

const cards = [
  {name: 'Sparks', resourceId: 342, row: 66, exportName: 'YugiRE_Sparks'},
  {name: 'Raimei', resourceId: 1007, row: 172, exportName: 'YugiRE_Raimei'},
];
for (const card of cards) {
  card.record = kTable.add(card.row * kStride);
  card.slot = card.record.add(4);
  card.replacement = null;
}

const loadLibrary = new NativeFunction(Module.getGlobalExportByName('LoadLibraryW'),
  'pointer', ['pointer'], 'stdcall');
const getProc = new NativeFunction(Module.getGlobalExportByName('GetProcAddress'),
  'pointer', ['pointer', 'pointer'], 'stdcall');
const virtualProtect = new NativeFunction(Module.getGlobalExportByName('VirtualProtect'),
  'bool', ['pointer', 'uint32', 'uint32', 'pointer'], 'stdcall');

let dllBase = null;
let installed = false;
let getCalls = null;
let getEmits = null;
let getDamage = null;

function verifyOriginalRecord(card) {
  if (card.record.readU32() !== card.resourceId) {
    throw new Error(card.name + ' resource ID mismatch at row ' + card.row);
  }
  if (!card.slot.readPointer().equals(kPrimaryOriginal)) {
    throw new Error(card.name + ' primary pointer differs from shared original: ' +
                    card.slot.readPointer());
  }
  for (const off of [8, 12, 16]) {
    if (!card.record.add(off).readPointer().isNull()) {
      throw new Error(card.name + ' unexpected non-null slot +' + off);
    }
  }
  if (!card.record.add(20).readPointer().equals(kTargetPreparation)) {
    throw new Error(card.name + ' target preparation callback mismatch');
  }
}

function verifyBeforeLoading() {
  for (const card of cards) verifyOriginalRecord(card);
  const damageRange = Process.findRangeByAddress(kDamageEmitter);
  if (damageRange === null || !damageRange.protection.includes('x')) {
    throw new Error('Native damage command producer is not executable');
  }
}

function writePrimary(card, pointerValue) {
  const range = Process.findRangeByAddress(card.slot);
  if (range === null || !range.protection.includes('r')) {
    throw new Error('Card table is not readable for ' + card.name);
  }
  const oldProtection = Memory.alloc(4);
  const dummy = Memory.alloc(4);
  const writable = range.protection.includes('x') ? 0x40 : 0x04;
  if (!virtualProtect(card.slot, 4, writable, oldProtection)) {
    throw new Error('VirtualProtect cannot enable writing: ' + card.name);
  }
  let writeError = null;
  try {
    card.slot.writePointer(pointerValue);
  } catch (error) {
    writeError = error;
  } finally {
    if (!virtualProtect(card.slot, 4, oldProtection.readU32(), dummy)) {
      throw new Error('Cannot restore original memory permissions for ' + card.name);
    }
  }
  if (writeError !== null) throw writeError;
  if (!card.slot.readPointer().equals(pointerValue)) {
    throw new Error('Primary pointer readback failed for ' + card.name);
  }
}

function getExport(name) {
  const address = getProc(dllBase, Memory.allocUtf8String(name));
  if (address.isNull()) throw new Error('Required DLL export not found: ' + name);
  const range = Process.findRangeByAddress(address);
  if (range === null || !range.protection.includes('x')) {
    throw new Error('DLL export is not executable: ' + name);
  }
  return address;
}

function status() {
  const entries = cards.map(card => ({
    name: card.name,
    resourceId: card.resourceId,
    row: card.row,
    originalPointer: kPrimaryOriginal.toString(),
    currentPointer: card.slot.readPointer().toString(),
    replacementPointer: card.replacement === null ? null : card.replacement.toString(),
    callbackCalls: getCalls === null ? 0 : getCalls(card.resourceId),
    commandsEmitted: getEmits === null ? 0 : getEmits(card.resourceId),
    configuredDamage: getDamage === null ? null : getDamage(card.resourceId),
  }));
  return {
    installed,
    modulePath: main.path,
    processId: Process.id,
    dllBase: dllBase === null ? null : dllBase.toString(),
    entries,
  };
}

// The restore is idempotent and also handles a failed install that only
// managed to patch the first card. It checks both slots BEFORE changing any.
function restore() {
  for (const card of cards) {
    const current = card.slot.readPointer();
    if (!current.equals(kPrimaryOriginal) &&
        (card.replacement === null || !current.equals(card.replacement))) {
      throw new Error('Unexpected ' + card.name + ' pointer; refusing partial restore: ' + current);
    }
  }
  const errors = [];
  for (const card of cards.slice().reverse()) {
    if (card.replacement !== null && card.slot.readPointer().equals(card.replacement)) {
      try { writePrimary(card, kPrimaryOriginal); }
      catch (error) { errors.push(card.name + ': ' + error); }
    }
  }
  installed = false;
  if (errors.length > 0) throw new Error('Partial restore: ' + errors.join('; '));
  return status();
}

rpc.exports = {
  status,
  install(dllPath, sparksDamage, raimeiDamage) {
    if (installed) throw new Error('Already installed');
    if (typeof dllPath !== 'string' || !/^[a-zA-Z]:\\/.test(dllPath)) {
      throw new Error('Expected an absolute Windows DLL path');
    }
    if (![200, 300].includes(sparksDamage) || ![300, 400].includes(raimeiDamage)) {
      throw new Error('Damage configuration outside tested safe experiment values');
    }
    verifyBeforeLoading();

    dllBase = loadLibrary(Memory.allocUtf16String(dllPath));
    if (dllBase.isNull()) throw new Error('LoadLibraryW failed for ' + dllPath);

    for (const card of cards) card.replacement = getExport(card.exportName);
    const abi = new NativeFunction(getExport('YugiRE_GetAbiVersion'),
      'uint32', [], 'mscdecl');
    if (abi() !== kAbiVersion) throw new Error('Incompatible YugiRE DLL ABI version');
    const initialize = new NativeFunction(getExport('YugiRE_Initialize'),
      'uint32', ['pointer', 'uint32', 'uint32'], 'mscdecl');
    getCalls = new NativeFunction(getExport('YugiRE_GetCallCount'),
      'uint32', ['uint32'], 'mscdecl');
    getEmits = new NativeFunction(getExport('YugiRE_GetEmitCount'),
      'uint32', ['uint32'], 'mscdecl');
    getDamage = new NativeFunction(getExport('YugiRE_GetConfiguredDamage'),
      'uint32', ['uint32'], 'mscdecl');

    if (initialize(main.base, sparksDamage, raimeiDamage) !== kInitMagic) {
      throw new Error('DLL returned wrong initialization marker');
    }
    if (getDamage(342) !== sparksDamage || getDamage(1007) !== raimeiDamage) {
      throw new Error('DLL did not accept independent damage configurations');
    }

    try {
      for (const card of cards) writePrimary(card, card.replacement);
      installed = true;
      return status();
    } catch (error) {
      let restoreError = null;
      try { restore(); } catch (inner) { restoreError = inner; }
      throw new Error('Installation failed: ' + error +
        (restoreError === null ? '; original pointers restored' :
          '; RESTORE FAILED: ' + restoreError));
    }
  },
  restore,
};
