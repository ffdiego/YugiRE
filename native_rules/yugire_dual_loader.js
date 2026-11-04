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
const kDrawEmitter = va('0x00578B60'); // FUN_00578B60(side, count)
const kDestroyEffect = va('0x00576960'); // FUN_00576960(request, mask, 14, 2)
const kInitMagic = 0x59554735; // YUG5
const kAbiVersion = 5;
const kSnapshotBytes = 88; // Two PlayerSnapshot structs, 11 DWORDs each

// The console command does NOT call Joey from Frida's RPC thread. A short
// request is staged here and consumed on the original duel update thread.
// For the documented EXE only (VA relative to image base 0x00400000).
const kDuelUpdate = va('0x005BC700');
const kQueueCount = va('0x00A558A8');
const kQueueFlags = va('0x00A558AC');
const kCommandStage = va('0x00A5780E');
const kDuelFlags = va('0x00A54E5C');
const kDeckCountBase = va('0x00A55D69');
const kMonsterSlotBase = va('0x00A55D74');
const kFieldSlotStride = 0x90;
const kFieldIdentityMask = 0x7F001FFF; // index + owner + occurrence key
const kPlayerStride = 0xD44; // documented per-player duel state stride
const kModeDamage = 1;
const kModeDrawFive = 2;
const kModeDamageDrawFive = 3;

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
let getMode = null;
let getDrawBatches = null;
let getManualDrawBatches = null;
let getManualDamageCommands = null;
let getManualDestroyAttempts = null;
let readNativeDuelState = null;
let manualDrawOnDuelThread = null;
let manualDamageOnDuelThread = null;
let manualDestroyOnDuelThread = null;
let duelUpdateHook = null;
let pendingConsoleAction = null;
let lastConsoleAction = null;
let consoleSequence = 0;

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
  const drawRange = Process.findRangeByAddress(kDrawEmitter);
  if (drawRange === null || !drawRange.protection.includes('x')) {
    throw new Error('Native draw command producer is not executable');
  }
  const destructionRange = Process.findRangeByAddress(kDestroyEffect);
  if (destructionRange === null || !destructionRange.protection.includes('x')) {
    throw new Error('Native destruction helper 0x00576960 is not executable');
  }
  const tickRange = Process.findRangeByAddress(kDuelUpdate);
  if (tickRange === null || !tickRange.protection.includes('x')) {
    throw new Error('Duel scheduler 0x005BC700 is not executable');
  }
  for (const address of [kQueueCount, kQueueFlags, kCommandStage, kDuelFlags,
                          kDeckCountBase, kDeckCountBase.add(kPlayerStride),
                           kMonsterSlotBase, kMonsterSlotBase.add(kPlayerStride)]) {
    const range = Process.findRangeByAddress(address);
    if (range === null || !range.protection.includes('r')) {
      throw new Error('Cannot read native duel queue state at ' + address);
    }
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

function queueGate() {
  const queueDepth = kQueueCount.readU32();
  const queueFlags = kQueueFlags.readU32();
  const commandStage = kCommandStage.readU16() >>> 8;
  const duelFlags = kDuelFlags.readU32();
  // 0x4000 marks a command being processed; stage must be zero.
  // 0x180==0x80 is a special terminal state which suppresses commands.
  const idle = queueDepth === 0 && (queueFlags & 0x4000) === 0 &&
    commandStage === 0 && (duelFlags & 0x180) !== 0x80;
  return {idle, queueDepth, busyCommand: (queueFlags & 0x4000) !== 0,
    commandStage, terminalFlag: (duelFlags & 0x180) === 0x80};
}

function monsterWord(side, slot) {
  return kMonsterSlotBase.add(side * kPlayerStride + slot * kFieldSlotStride).readU32();
}
function sameMonsterIdentity(observed, expected) {
  return (observed & 0xFFF) !== 0 && (expected & 0xFFF) !== 0 &&
    (observed & kFieldIdentityMask) === (expected & kFieldIdentityMask);
}

// Only called synchronously from Interceptor.onLeave for 0x005BC700,
// on the game's own duel-update thread. Never invoke NativeFunction
// YugiRE_ConsoleDraw from rpc.exports or a Python/Frida RPC thread.
function servicePendingConsoleAction() {
  // RPC stages an action; only Interceptor.onLeave invokes a native
  // producer, preserving the game-thread requirement.
  if (!installed || pendingConsoleAction === null) return;
  if (!queueGate().idle) return;
  const request = pendingConsoleAction;
  pendingConsoleAction = null;
  try {
    let accepted = 0;
    if (request.kind === 'draw') {
      const cardsRemaining = kDeckCountBase.add(request.side * kPlayerStride).readU8();
      if (cardsRemaining < request.count) {
        lastConsoleAction = {...request, state: 'rejected',
          detail: 'Deck has only ' + cardsRemaining + ' card(s); refusing draw ' + request.count};
        return;
      }
      accepted = manualDrawOnDuelThread(request.side, request.count);
    } else if (request.kind === 'damage') {
      accepted = manualDamageOnDuelThread(request.side, request.amount);
    } else if (request.kind === 'destroy') {
      if (!sameMonsterIdentity(monsterWord(request.side, request.slot), request.expectedWord)) {
        lastConsoleAction = {...request, state: 'rejected',
          detail: 'Target slot is empty or card identity changed while pending'};
        return;
      }
      accepted = manualDestroyOnDuelThread(request.side, request.slot, request.expectedWord);
    } else {
      throw new Error('Invalid pending console action');
    }
    lastConsoleAction = {...request,
      state: accepted === 1 ? (request.kind === 'destroy' ? 'submitted' : 'enqueued') : 'rejected',
      detail: accepted !== 1 ? 'The native producer refused the action' :
        request.kind === 'destroy' ?
          'Native effect-destruction helper invoked; check state/diff for actual removal' :
          'Original engine command(s) queued; resolution is asynchronous'};
  } catch (error) {
    lastConsoleAction = {...request, state: 'failed', detail: String(error)};
  }
}

function installConsoleHook() {
  duelUpdateHook = Interceptor.attach(kDuelUpdate, {
    onLeave(_retval) {
      servicePendingConsoleAction();
    },
  });
}

function stopConsoleHook() {
  pendingConsoleAction = null;
  if (duelUpdateHook !== null) {
    const hook = duelUpdateHook;
    duelUpdateHook = null;
    hook.detach();
  }
}

function stageConsoleAction(action) {
  if (!installed || duelUpdateHook === null) {
    throw new Error('Console actions require an installed patch');
  }
  if (pendingConsoleAction !== null) {
    throw new Error('A console action is already pending; wait or cancel it');
  }
  const request = {id: ++consoleSequence, ...action};
  pendingConsoleAction = request;
  return {state: 'pending', ...request};
}

function queueConsoleDraw(side, count) {
  if (!Number.isInteger(side) || (side !== 0 && side !== 1) ||
      !Number.isInteger(count) || count !== 5) {
    throw new Error('Use draw 5 [0|1]; only 5 cards and side 0/1 are supported');
  }
  return stageConsoleAction({kind: 'draw', side, count});
}

function queueConsoleDamage(side, amount) {
  if (!Number.isInteger(side) || (side !== 0 && side !== 1) ||
      !Number.isInteger(amount) || amount < 1 || amount > 9999) {
    throw new Error('Use damage <1..9999> <0|1>; explicit target side required');
  }
  return stageConsoleAction({kind: 'damage', side, amount});
}

function queueConsoleDestroy(side, slot) {
  if (!Number.isInteger(side) || (side !== 0 && side !== 1) ||
      !Number.isInteger(slot) || slot < 0 || slot >= 5) {
    throw new Error('Use destroy <0|1> <0..4>; explicit side and monster slot required');
  }
  const expectedWord = monsterWord(side, slot);
  if ((expectedWord & 0xFFF) === 0) throw new Error('Target monster slot is empty');
  return stageConsoleAction({kind: 'destroy', side, slot,
    expectedWord, expectedIndex: expectedWord & 0xFFF});
}

function cancelPendingConsoleAction() {
  if (pendingConsoleAction === null) return {state: 'none'};
  const request = pendingConsoleAction;
  pendingConsoleAction = null;
  lastConsoleAction = {...request, state: 'cancelled',
    detail: 'Cancelled before the duel scheduler accepted it'};
  return lastConsoleAction;
}

function duelState() {
  // Safe read-only RPC operation. This is NOT an atomic snapshot: the
  // original game may update memory between individual field reads.
  if (!installed || readNativeDuelState === null) {
    throw new Error('Duel-state inspection requires an installed patch');
  }
  const buffer = Memory.alloc(kSnapshotBytes);
  if (readNativeDuelState(main.base, buffer) !== 1) {
    throw new Error('The DLL refused the duel-state snapshot');
  }
  const sides = [0, 1].map(side => {
    const offset = side * 44;
    const word = n => buffer.add(offset + n * 4).readU32();
    return {
      side, lifePoints: word(0), handCount: word(1), deckCount: word(2),
      graveyardCount: word(3), banishedCount: word(4),
      terminalFlags: word(5),
      monsterIndices: [word(6), word(7), word(8), word(9), word(10)]
    };
  });
  return {sides, note: 'Non-atomic read; monster indices are internal, not resource IDs'};
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
    configuredMode: getMode === null ? null : getMode(card.resourceId),
    drawBatches: getDrawBatches === null ? 0 : getDrawBatches(card.resourceId),
  }));
  return {
    installed,
    modulePath: main.path,
    processId: Process.id,
    dllBase: dllBase === null ? null : dllBase.toString(),
    console: {
      batchesQueued: getManualDrawBatches === null ? 0 : getManualDrawBatches(),
      damageCommandsQueued: getManualDamageCommands === null ? 0 : getManualDamageCommands(),
      destroyAttempts: getManualDestroyAttempts === null ? 0 : getManualDestroyAttempts(),
      pending: pendingConsoleAction,
      last: lastConsoleAction,
      gate: installed ? queueGate() : null
    },
    entries,
  };
}

// The restore is idempotent and also handles a failed install that only
// managed to patch the first card. It checks both slots BEFORE changing any.
function restore() {
  stopConsoleHook(); // also drops any unserviced command before unpatching
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
  duelstate: duelState,
  queuedraw: queueConsoleDraw,
  queuedamage: queueConsoleDamage,
  queuedestroy: queueConsoleDestroy,
  canceldraw: cancelPendingConsoleAction,
  install(dllPath, sparksDamage, raimeiDamage, sparksMode, raimeiMode) {
    if (installed) throw new Error('Already installed');
    if (typeof dllPath !== 'string' || !/^[a-zA-Z]:\\/.test(dllPath)) {
      throw new Error('Expected an absolute Windows DLL path');
    }
    const validDamage = value => Number.isInteger(value) && value >= 1 && value <= 9999;
    if (!validDamage(sparksDamage) || !validDamage(raimeiDamage)) {
      throw new Error('Damage must be an integer from 1 to 9999 LP per card');
    }
    const validMode = value => Number.isInteger(value) &&
      value >= kModeDamage && value <= kModeDamageDrawFive;
    if (!validMode(sparksMode) || !validMode(raimeiMode)) {
      throw new Error('Modes must be integers 1 (damage), 2 (draw), or 3 (damage-draw)');
    }
    verifyBeforeLoading();

    dllBase = loadLibrary(Memory.allocUtf16String(dllPath));
    if (dllBase.isNull()) throw new Error('LoadLibraryW failed for ' + dllPath);

    for (const card of cards) card.replacement = getExport(card.exportName);
    const abi = new NativeFunction(getExport('YugiRE_GetAbiVersion'),
      'uint32', [], 'mscdecl');
    if (abi() !== kAbiVersion) throw new Error('Incompatible YugiRE DLL ABI version');
    const initialize = new NativeFunction(getExport('YugiRE_Initialize'),
      'uint32', ['pointer', 'uint32', 'uint32', 'uint32', 'uint32'], 'mscdecl');
    getCalls = new NativeFunction(getExport('YugiRE_GetCallCount'),
      'uint32', ['uint32'], 'mscdecl');
    getEmits = new NativeFunction(getExport('YugiRE_GetEmitCount'),
      'uint32', ['uint32'], 'mscdecl');
    getDamage = new NativeFunction(getExport('YugiRE_GetConfiguredDamage'),
      'uint32', ['uint32'], 'mscdecl');
    getMode = new NativeFunction(getExport('YugiRE_GetConfiguredMode'),
      'uint32', ['uint32'], 'mscdecl');
    getDrawBatches = new NativeFunction(getExport('YugiRE_GetDrawBatchCount'),
      'uint32', ['uint32'], 'mscdecl');
    getManualDrawBatches = new NativeFunction(getExport('YugiRE_GetConsoleDrawBatchCount'),
      'uint32', [], 'mscdecl');
    manualDrawOnDuelThread = new NativeFunction(getExport('YugiRE_ConsoleDraw'),
      'uint32', ['uint32', 'uint32'], 'mscdecl');
    manualDamageOnDuelThread = new NativeFunction(getExport('YugiRE_ConsoleDamage'),
      'uint32', ['uint32', 'uint32'], 'mscdecl');
    getManualDamageCommands = new NativeFunction(getExport('YugiRE_GetConsoleDamageCount'),
      'uint32', [], 'mscdecl');
    manualDestroyOnDuelThread = new NativeFunction(getExport('YugiRE_ConsoleDestroy'),
      'uint32', ['uint32', 'uint32', 'uint32'], 'mscdecl');
    getManualDestroyAttempts = new NativeFunction(getExport('YugiRE_GetConsoleDestroyCount'),
      'uint32', [], 'mscdecl');
    readNativeDuelState = new NativeFunction(getExport('YugiRE_ReadDuelState'),
      'uint32', ['pointer', 'pointer'], 'mscdecl');

    if (initialize(main.base, sparksDamage, raimeiDamage,
                   sparksMode, raimeiMode) !== kInitMagic) {
      throw new Error('DLL returned wrong initialization marker');
    }
    if (getDamage(342) !== sparksDamage || getDamage(1007) !== raimeiDamage ||
        getMode(342) !== sparksMode || getMode(1007) !== raimeiMode) {
      throw new Error('DLL did not accept independent effect configurations');
    }

    try {
      for (const card of cards) writePrimary(card, card.replacement);
      installConsoleHook();
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
