/*
 * Host-side Frida adapter for C++ native_sparks/joey_rules.dll.
 * Installs ONE pointer swap in the known Joey PE32 x86 card table.
 * Native card logic and counters reside entirely in the DLL.
 *
 * Loaded by patch_sparks_dll.py; not intended for `frida -l` directly.
 * Never call FreeLibrary here: a callback may still be executing when the
 * pointer is restored. The DLL remains loaded until Joey exits.
 */
'use strict';

if (Process.platform !== 'windows' || Process.arch !== 'ia32') {
  throw new Error('Only Windows x86 processes are supported');
}

const main = Process.mainModule;
if (main.name.toLowerCase() !== 'joey_pc.exe') {
  throw new Error('Unexpected process: ' + main.name);
}

const DOCUMENTED_BASE = ptr('0x00400000');
const resolveVA = value => main.base.add(ptr(value).sub(DOCUMENTED_BASE));
const TABLE_ROW = resolveVA('0x005ED0A8').add(66 * 24);
const PRIMARY_SLOT = TABLE_ROW.add(4);
const ORIGINAL_PRIMARY = resolveVA('0x00584E60');
const TARGET_PREPARATION = resolveVA('0x00593CC0');
const DAMAGE_HELPER = resolveVA('0x005784E0');
const RESOURCE_ID = 342;
const INIT_MAGIC = 0x59554731;

const loadLibrary = new NativeFunction(
  Module.getGlobalExportByName('LoadLibraryW'),
  'pointer', ['pointer'], 'stdcall');
const getProcAddress = new NativeFunction(
  Module.getGlobalExportByName('GetProcAddress'),
  'pointer', ['pointer', 'pointer'], 'stdcall');
const virtualProtect = new NativeFunction(
  Module.getGlobalExportByName('VirtualProtect'),
  'bool', ['pointer', 'uint32', 'uint32', 'pointer'], 'stdcall');

let dllBase = null;
let dllCallback = null;
let installed = false;
let getCalls = null;
let getEmits = null;
let getDamage = null;

function verifyOriginalRow() {
  if (TABLE_ROW.readU32() !== RESOURCE_ID) {
    throw new Error('Resource ID 342 not found at row 66');
  }
  if (!PRIMARY_SLOT.readPointer().equals(ORIGINAL_PRIMARY)) {
    throw new Error('Sparks callback slot differs from expected original: ' +
                    PRIMARY_SLOT.readPointer());
  }
  for (const offset of [8, 12, 16]) {
    if (!TABLE_ROW.add(offset).readPointer().isNull()) {
      throw new Error('Unexpected non-null card slot at +' + offset);
    }
  }
  if (!TABLE_ROW.add(20).readPointer().equals(TARGET_PREPARATION)) {
    throw new Error('Target preparation callback mismatch');
  }
  const helperRange = Process.findRangeByAddress(DAMAGE_HELPER);
  if (helperRange === null || !helperRange.protection.includes('x')) {
    throw new Error('Original damage helper is not executable');
  }
}

function changePrimaryPointer(newPointer) {
  const range = Process.findRangeByAddress(PRIMARY_SLOT);
  if (range === null || !range.protection.includes('r')) {
    throw new Error('Card table memory not readable');
  }
  const previous = Memory.alloc(4);
  const ignored = Memory.alloc(4);
  const newProtection = range.protection.includes('x') ? 0x40 : 0x04;
  if (!virtualProtect(PRIMARY_SLOT, 4, newProtection, previous)) {
    throw new Error('VirtualProtect could not unlock primary slot');
  }
  let writeError = null;
  try {
    PRIMARY_SLOT.writePointer(newPointer);
  } catch (error) {
    writeError = error;
  } finally {
    if (!virtualProtect(PRIMARY_SLOT, 4, previous.readU32(), ignored)) {
      throw new Error('VirtualProtect failed restoring original page permissions');
    }
  }
  if (writeError !== null) throw writeError;
}

function getDllExport(name) {
  const address = getProcAddress(dllBase, Memory.allocUtf8String(name));
  if (address.isNull()) throw new Error('DLL export missing: ' + name);
  const range = Process.findRangeByAddress(address);
  if (range === null || !range.protection.includes('x')) {
    throw new Error('DLL export is not executable: ' + name);
  }
  return address;
}

function status() {
  return {
    processId: Process.id,
    modulePath: main.path,
    installed,
    resourceId: RESOURCE_ID,
    cardTableRow: 66,
    originalPrimaryPointer: ORIGINAL_PRIMARY.toString(),
    currentPrimaryPointer: PRIMARY_SLOT.readPointer().toString(),
    replacementPrimaryPointer: dllCallback === null ? null : dllCallback.toString(),
    loadedDllBase: dllBase === null ? null : dllBase.toString(),
    callbackCalls: getCalls === null ? 0 : getCalls(),
    damageCommandsEmitted: getEmits === null ? 0 : getEmits(),
    configuredDamage: getDamage === null ? null : getDamage(),
  };
}

rpc.exports = {
  status,
  install(path, damage) {
    if (installed) throw new Error('Callback already redirected');
    if (typeof path !== 'string' || !/^[a-zA-Z]:\\/.test(path)) {
      throw new Error('Provide an absolute Windows DLL path');
    }
    if (damage !== 200 && damage !== 300) {
      throw new Error('Only 200 and 300 damage are supported in this experiment');
    }
    verifyOriginalRow();

    // Once loaded the module is intentionally kept loaded, including after
    // restore(), to avoid dangling native return addresses during teardown.
    dllBase = loadLibrary(Memory.allocUtf16String(path));
    if (dllBase.isNull()) throw new Error('LoadLibraryW failed: ' + path);
    dllCallback = getDllExport('YugiRE_Sparks');
    const initialize = new NativeFunction(
      getDllExport('YugiRE_Initialize'), 'uint32', ['pointer', 'uint32'], 'mscdecl');
    getCalls = new NativeFunction(getDllExport('YugiRE_GetCallCount'), 'uint32', [], 'mscdecl');
    getEmits = new NativeFunction(getDllExport('YugiRE_GetEmitCount'), 'uint32', [], 'mscdecl');
    getDamage = new NativeFunction(getDllExport('YugiRE_GetConfiguredDamage'), 'uint32', [], 'mscdecl');
    if (initialize(main.base, damage) !== INIT_MAGIC) {
      throw new Error('YugiRE_Initialize returned an invalid initialization marker');
    }
    if (getDamage() !== damage) {
      throw new Error('DLL damage configuration mismatch');
    }

    try {
      changePrimaryPointer(dllCallback);
      if (!PRIMARY_SLOT.readPointer().equals(dllCallback)) {
        throw new Error('Callback pointer did not change as expected');
      }
      installed = true;
      return status();
    } catch (error) {
      if (PRIMARY_SLOT.readPointer().equals(dllCallback)) {
        changePrimaryPointer(ORIGINAL_PRIMARY);
      }
      throw error;
    }
  },
  restore() {
    if (!installed) return status();
    if (!PRIMARY_SLOT.readPointer().equals(dllCallback)) {
      throw new Error('Pointer changed by another tool; refusing to overwrite');
    }
    changePrimaryPointer(ORIGINAL_PRIMARY);
    if (!PRIMARY_SLOT.readPointer().equals(ORIGINAL_PRIMARY)) {
      throw new Error('Original callback pointer was not restored');
    }
    installed = false;
    return status();
  },
};
