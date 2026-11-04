/*
 * Sparks-only replacement experiment for the exact Joey Power of Chaos EXE.
 * This is loaded via patch_sparks.py, not directly with the Frida REPL.
 *
 * Replaces *only* primary slot of resource ID 342, row 66, with an allocated
 * native x86 callback. The callback increments a counter, then implements
 * Sparks' already-reconstructed path:
 *     if ((request[4] & 4) == 0)
 *         0x005784E0(request[6] & 1, 200);
 *     return AX=0;
 *
 * Does not replace shared 0x00584E60 (used by Ookazi and other cards),
 * nor the separate target-preparation callback 0x00593CC0.
 *
 * IMPORTANT: Unvalidated on a real Joey process. This changes in-memory
 * function pointers but never modifies the on-disk executable.
 */
'use strict';

if (Process.platform !== 'windows' || Process.arch !== 'ia32') {
  throw new Error('Expected Windows x86 process');
}
const main = Process.mainModule;
if (main.name.toLowerCase() !== 'joey_pc.exe') {
  throw new Error('Unexpected process ' + main.name);
}

const ORIGINAL_BASE = ptr('0x00400000');
function va(absolute) {
  return main.base.add(ptr(absolute).sub(ORIGINAL_BASE));
}
const TABLE_ROW = va('0x005ED0A8').add(66 * 24);
const PRIMARY_SLOT = TABLE_ROW.add(4);
const PREPARATION_SLOT = TABLE_ROW.add(20);
const ORIGINAL_CALLBACK = va('0x00584E60');
const TARGET_PREPARATION_CALLBACK = va('0x00593CC0');
const EMIT_DAMAGE_COMMAND = va('0x005784E0');
const RESOURCE_ID = 342;

const virtualAlloc = new NativeFunction(
  Module.getGlobalExportByName('VirtualAlloc'), 'pointer',
  ['pointer', 'uint32', 'uint32', 'uint32'], 'stdcall');
const virtualProtect = new NativeFunction(
  Module.getGlobalExportByName('VirtualProtect'), 'bool',
  ['pointer', 'uint32', 'uint32', 'pointer'], 'stdcall');

let replacementCode = null;
let callCounter = null;
let installed = false;

function verifyOriginalRecord() {
  if (TABLE_ROW.readU32() !== RESOURCE_ID) {
    throw new Error('Record ID mismatch; refusing to patch');
  }
  if (!PRIMARY_SLOT.readPointer().equals(ORIGINAL_CALLBACK)) {
    throw new Error('Original primary pointer changed; refusing to patch: ' +
                    PRIMARY_SLOT.readPointer());
  }
  if (!PREPARATION_SLOT.readPointer().equals(TARGET_PREPARATION_CALLBACK)) {
    throw new Error('Unexpected target-preparation callback');
  }
  for (const offset of [8, 12, 16]) {
    if (!TABLE_ROW.add(offset).readPointer().isNull()) {
      throw new Error('Unexpected non-null slot at offset ' + offset);
    }
  }
}

function changePrimaryPointer(newValue) {
  const memoryRange = Process.findRangeByAddress(PRIMARY_SLOT);
  if (memoryRange === null || !memoryRange.protection.includes('r')) {
    throw new Error('Primary slot memory is not readable');
  }
  const oldProtection = Memory.alloc(4);
  const ignored = Memory.alloc(4);
  const writableProtection = memoryRange.protection.includes('x') ? 0x40 : 0x04;
  if (!virtualProtect(PRIMARY_SLOT, 4, writableProtection, oldProtection)) {
    throw new Error('VirtualProtect: cannot make slot writable');
  }
  let writeError = null;
  try {
    PRIMARY_SLOT.writePointer(newValue);
  } catch (error) {
    writeError = error;
  } finally {
    if (!virtualProtect(PRIMARY_SLOT, 4, oldProtection.readU32(), ignored)) {
      throw new Error('VirtualProtect: cannot restore original page protection');
    }
  }
  if (writeError !== null) throw writeError;
}

function le32(number) {
  const n = number >>> 0;
  return [n & 0xff, (n >>> 8) & 0xff, (n >>> 16) & 0xff, (n >>> 24) & 0xff];
}

function assembleCallback(codeAddress, counterAddress) {
  // lock inc DWORD PTR ds:[counter]    ; track actual native calls
  // push ebx; push esi                  ; preserve nonvolatile registers
  // mov esi,[esp+0xc]                   ; first cdecl argument = request
  // test BYTE PTR [esi+4],4; jnz done
  // xor ebx,ebx; mov bl,[esi+6]; and ebx,1
  // push 200; push ebx
  // call 0x005784E0                     ; existing native command producer
  // add esp,8
  // done: xor ax,ax; pop esi; pop ebx; ret
  const prefix = [0xF0, 0xFF, 0x05, ...le32(counterAddress.toUInt32())];
  const body = [
    0x53, 0x56,
    0x8B, 0x74, 0x24, 0x0C,
    0xF6, 0x46, 0x04, 0x04,
    0x75, 0x00,
    0x31, 0xDB,
    0x8A, 0x5E, 0x06,
    0x83, 0xE3, 0x01,
    0x68, 0xC8, 0x00, 0x00, 0x00,
    0x53,
    0xE8, 0x00, 0x00, 0x00, 0x00,
    0x83, 0xC4, 0x08,
    0x66, 0x33, 0xC0,
    0x5E, 0x5B, 0xC3,
  ];

  const jnzDisplacement = 11;
  const doneIndex = body.length - 6; // xor ax,ax; pop esi; pop ebx; ret
  const skip = doneIndex - (jnzDisplacement + 1);
  if (skip < 0 || skip > 127) throw new Error('Invalid short jump displacement');
  body[jnzDisplacement] = skip;

  const callOpcodeIndex = body.indexOf(0xE8);
  if (callOpcodeIndex < 0) throw new Error('Call opcode not found');
  const returnAddress = codeAddress.add(prefix.length + callOpcodeIndex + 5);
  const relative = (EMIT_DAMAGE_COMMAND.toUInt32() - returnAddress.toUInt32()) | 0;
  body.splice(callOpcodeIndex + 1, 4, ...le32(relative));
  return [...prefix, ...body];
}

function allocateReplacement() {
  const pageSize = Process.pageSize;
  const allocation = virtualAlloc(ptr(0), 2 * pageSize, 0x3000, 0x04);
  if (allocation.isNull()) throw new Error('VirtualAlloc failed');
  const counter = allocation.add(pageSize);
  counter.writeU32(0);
  const code = assembleCallback(allocation, counter);
  allocation.writeByteArray(code);
  if (!Memory.protect(allocation, pageSize, 'r-x')) {
    throw new Error('Could not mark native callback memory executable');
  }
  return {codeAddress: allocation, counterAddress: counter, bytes: code};
}

function status() {
  return {
    installed,
    processId: Process.id,
    modulePath: main.path,
    resourceId: RESOURCE_ID,
    row: 66,
    currentPrimaryPointer: PRIMARY_SLOT.readPointer().toString(),
    originalPrimaryPointer: ORIGINAL_CALLBACK.toString(),
    replacementPrimaryPointer: replacementCode === null ? null : replacementCode.toString(),
    callbackCalls: callCounter === null ? 0 : callCounter.readU32(),
    note: 'Counts Sparks primary callback calls, not successful damage events.',
  };
}

rpc.exports = {
  status,
  install() {
    if (installed) return status();
    verifyOriginalRecord();
    const native = allocateReplacement();
    replacementCode = native.codeAddress;
    callCounter = native.counterAddress;
    try {
      changePrimaryPointer(replacementCode);
      if (!PRIMARY_SLOT.readPointer().equals(replacementCode)) {
        throw new Error('Post-install readback mismatch');
      }
      installed = true;
      return status();
    } catch (error) {
      if (PRIMARY_SLOT.readPointer().equals(replacementCode)) {
        changePrimaryPointer(ORIGINAL_CALLBACK);
      }
      throw error;
    }
  },
  restore() {
    if (!installed) return status();
    if (!PRIMARY_SLOT.readPointer().equals(replacementCode)) {
      throw new Error('Another tool changed the primary slot; refusing to overwrite');
    }
    changePrimaryPointer(ORIGINAL_CALLBACK);
    if (!PRIMARY_SLOT.readPointer().equals(ORIGINAL_CALLBACK)) {
      throw new Error('Restoration readback mismatch');
    }
    installed = false;
    // Native allocation is left reserved until Joey exits, so any in-flight
    // instruction cannot jump into a freed page during teardown.
    return status();
  },
};
