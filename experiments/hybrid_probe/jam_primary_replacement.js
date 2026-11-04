/*
 * Experimental exact-build pointer replacement for Jam Breeding Machine.
 * PE32 x86 only. Loaded by patch_jam_breeding.py, NOT with frida -l.
 * The replacement is native x86 code allocated by VirtualAlloc, not a
 * NativeCallback. It remains valid even if the Frida script disconnects.
 *
 * Records: table VA 0x5ED0A8, stride 24, JBM row 378 / resource ID 1426.
 * The primary pointer at row+4 must initially be 0x00583F30.
 * Replacement: lock inc dword ptr [counter]; xor eax,eax; ret
 * The wrapper 0x0059DEB0 calls primary(pedido, arg2) and cleans the stack.
 * No other callback slot, executable code, or original EXE file is changed.
 */
'use strict';

if (Process.platform !== 'windows' || Process.arch !== 'ia32') {
  throw new Error('Windows x86 (ia32) required');
}
const main = Process.mainModule;
if (main.name.toLowerCase() !== 'joey_pc.exe') {
  throw new Error('Main module is not joey_pc.exe');
}

const DOC_BASE = ptr('0x00400000');
const TABLE_VA = ptr('0x005ED0A8');
const RESOURCE_ID = 1426;
const ROW_INDEX = 378;
const EXPECTED_PRIMARY_VA = ptr('0x00583F30');
const MAIN_ROW = main.base.add(TABLE_VA.sub(DOC_BASE)).add(ROW_INDEX * 24);
const PRIMARY_SLOT = MAIN_ROW.add(4);
const EXPECTED_POINTER = main.base.add(EXPECTED_PRIMARY_VA.sub(DOC_BASE));

const kernelVirtualAlloc = new NativeFunction(
  Module.getGlobalExportByName('VirtualAlloc'),
  'pointer', ['pointer', 'uint32', 'uint32', 'uint32'], 'stdcall');
const kernelVirtualProtect = new NativeFunction(
  Module.getGlobalExportByName('VirtualProtect'),
  'bool', ['pointer', 'uint32', 'uint32', 'pointer'], 'stdcall');

let stub = null;
let counterAddress = null;
let installed = false;

function assertOriginalRecord() {
  if (MAIN_ROW.readU32() !== RESOURCE_ID) {
    throw new Error('Card table record ID mismatch at ' + MAIN_ROW);
  }
  if (!PRIMARY_SLOT.readPointer().equals(EXPECTED_POINTER)) {
    throw new Error('Primary pointer mismatch: expected ' + EXPECTED_POINTER +
                    ', found ' + PRIMARY_SLOT.readPointer());
  }
}

function writeSlot(value) {
  const previousPermissions = Memory.alloc(4);
  const ignoredPrevious = Memory.alloc(4);
  const region = Process.findRangeByAddress(PRIMARY_SLOT);
  if (region === null || !region.protection.includes('r')) {
    throw new Error('Card table slot is not mapped/readable');
  }
  const writablePermissions = region.protection.includes('x') ? 0x40 : 0x04;
  if (!kernelVirtualProtect(PRIMARY_SLOT, 4, writablePermissions, previousPermissions)) {
    throw new Error('VirtualProtect failed to make card pointer writable');
  }
  let writeError = null;
  try {
    PRIMARY_SLOT.writePointer(value);
  } catch (error) {
    writeError = error;
  } finally {
    if (!kernelVirtualProtect(PRIMARY_SLOT, 4, previousPermissions.readU32(), ignoredPrevious)) {
      throw new Error('Failed to restore card table memory permissions');
    }
  }
  if (writeError !== null) throw writeError;
}

function makeNativeStub() {
  const page = Process.pageSize;
  const allocation = kernelVirtualAlloc(ptr(0), page * 2, 0x3000, 0x04);
  if (allocation.isNull()) throw new Error('VirtualAlloc failed');
  const counter = allocation.add(page);
  counter.writeU32(0);
  const n = counter.toUInt32();
  // LOCK INC dword ptr [absolute x86 address]; XOR EAX,EAX; RET
  const machineCode = [0xF0, 0xFF, 0x05,
    n & 0xff, (n >>> 8) & 0xff, (n >>> 16) & 0xff, (n >>> 24) & 0xff,
    0x31, 0xC0, 0xC3];
  allocation.writeByteArray(machineCode);
  if (!Memory.protect(allocation, page, 'r-x')) {
    throw new Error('Unable to mark replacement stub executable');
  }
  return {code: allocation, count: counter};
}

function status() {
  return {
    installed,
    processId: Process.id,
    modulePath: main.path,
    cardResourceId: RESOURCE_ID,
    cardRow: ROW_INDEX,
    slot: PRIMARY_SLOT.toString(),
    expectedOriginalPointer: EXPECTED_POINTER.toString(),
    currentPointer: PRIMARY_SLOT.readPointer().toString(),
    replacementPointer: stub === null ? null : stub.toString(),
    callbackCalls: counterAddress === null ? 0 : counterAddress.readU32(),
    note: 'Counts primary callback invocations, not token creations or Standby Phases.',
  };
}

rpc.exports = {
  install() {
    if (installed) return status();
    assertOriginalRecord();
    const native = makeNativeStub();
    // Keep native allocation alive until the process exits, including after
    // restore. No dependency on a JS NativeCallback or Frida script lifetime.
    stub = native.code;
    counterAddress = native.count;
    try {
      writeSlot(stub);
      if (!PRIMARY_SLOT.readPointer().equals(stub)) {
        throw new Error('Post-install pointer verification failed');
      }
      installed = true;
      return status();
    } catch (error) {
      if (PRIMARY_SLOT.readPointer().equals(stub)) {
        writeSlot(EXPECTED_POINTER);
      }
      throw error;
    }
  },
  status,
  restore() {
    if (!installed) return status();
    if (!PRIMARY_SLOT.readPointer().equals(stub)) {
      throw new Error('Slot changed by another tool; refusing to overwrite');
    }
    writeSlot(EXPECTED_POINTER);
    installed = false;
    if (!PRIMARY_SLOT.readPointer().equals(EXPECTED_POINTER)) {
      throw new Error('Restored pointer failed verification');
    }
    // Do not free the stub: a call might still be returning through it.
    // This leaves two tiny pages allocated until Joey exits.
    return status();
  },
};
