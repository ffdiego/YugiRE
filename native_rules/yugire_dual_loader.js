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
const kAbiVersion = 6;
const kSnapshotPlayerWords = 108; // six scalars, 5+6 slots, 11 positions, 80 hand
const kSnapshotBytes = kSnapshotPlayerWords * 4 * 2;

// The console command does NOT call Joey from Frida's RPC thread. A short
// request is staged here and consumed on the original duel update thread.
// For the documented EXE only (VA relative to image base 0x00400000).
const kDuelUpdate = va('0x005BC700');
// Experimental -speedy setting, only for the verified PE32 fingerprint.
// FUN_00411B50 writes 60 at 00411CA2; 00411CAC is the NEXT instruction.
const kSpeedySetting = va('0x005EB618');
const kSpeedyWriteFinished = va('0x00411CAC');
// Ghidra: FUN_0054D460 calls CreateMutexA and checks ERROR_ALREADY_EXISTS;
// FUN_0055DB10 builds the mutex name from GetModuleFileNameA with '\\' -> '+'.
const kSingletonMutexCtor = va('0x0054D460');
const kSingletonMutexCtorEnd = va('0x0054D550');
// Common command producer. Observer only: no argument/retval replacement.
const kCommandProducer = va('0x005B9170');
// Known CE callsite and native placement descriptor.
// These are read ONLY for CE emitted by FUN_005ABD00.
const kCeCallsiteReturn = va('0x005ABEAB');
const kPlacementContext = va('0x00A55080');
const kPlacementSource = va('0x00A55084');
const kPlacementStage = va('0x00A5508E');
const kPlacementAuxFlags = va('0x00A5508C');
// Read-only observation of the normal summon/tribute path, based on
// docs/evidence/normal-summon-{level-consumers,tribute-context,...}.
const kSummonPrepTargets = [
  {name: 'eligibility', va: '0x005AAB90', address: va('0x005AAB90'), argCount: 2},
  // Ghidra 2026-11-04: FUN_005AD740(side, handIndex, slot, tributeData, mode).
  // This builds the normal summon/set descriptor but does not validate legality.
  {name: 'normal_builder', va: '0x005AD740', address: va('0x005AD740'), argCount: 5},
  {name: 'dispatcher', va: '0x005AB820', address: va('0x005AB820'), argCount: 0},
  {name: 'placement', va: '0x005ABD00', address: va('0x005ABD00'), argCount: 0},
  {name: 'adjust', va: '0x005AC870', address: va('0x005AC870'), argCount: 0}
];
const kSummonPlayerFlags = va('0x00A56430'); // includes 0x20000 normal-summon bit
const kSummonDuelPhase = va('0x00A577FA');
const kDuelRunning = va('0x00A577F8');
const kHandCountBase = va('0x00A55D68');
const kHandWordBase = va('0x00A56434');
// Native engine entry points known from Ghidra (same executable SHA).
const kSummonEligible = va('0x005AAB90');
const kFindMonsterSlot = va('0x0056A030');
const kSlotLegal = va('0x00569F70');
const kMonsterIdentity = va('0x00402690');
const kMonsterLevelClass = va('0x004026C0');
const kNormalSummonBuilder = va('0x005AD740');
const kPlayerFlagEmitter = va('0x00579930');
const kTraceDefaultCapacity = 8192;
const kTraceMaxCapacity = 20000;
// First opt-in player-side native AI experiment. Normal summon/set only.
const kAiSelectNormal = va('0x005B1360'); // side, pass -> handIndex
// The observed callsite of FUN_005B1360 passes modeSelector=0
// directly to FUN_005B6FF0. FUN_005AE270 belongs to OTHER AI branches.
const kAiForceSet = va('0x00570A70');     // zero forces set in native executor
// Opt-in passive hook: CPU's normal-placement executor, not all card actions.
const kDemoNormalExecutor = va('0x005B6FF0');
// Two pre-duel deck objects (0xE2 bytes each): 80/15/15 ushort
// card arrays followed by 3 ushort counts at +0xDC/+0xDE/+0xE0.
// FUN_00569380 copies the main and the +0xBE auxiliary section.
const kDemoSourceDeck0 = va('0x00A54E60');
const kDemoSourceDeckStride = 0xE2;
const kDemoModeWord = va('0x00A54E5C');
const kDemoSceneMode = va('0x005F3294');
const kDemoSetupTargets = [
  // Scene mode writer and scene factory are upstream of the duel bootstrap.
  // The exact menu idle timer/caller is not known yet.
  {name: 'scene_config', va: '0x0044BEE0', address: va('0x0044BEE0')},
  // Called twice in mode 7, once before and once after source 1 -> 0 copy.
  {name: 'deck_resource_step', va: '0x0044A920', address: va('0x0044A920')},
  {name: 'scene_factory', va: '0x00483500', address: va('0x00483500')},
  {name: 'duel_entry', va: '0x005BFEB0', address: va('0x005BFEB0')},
  {name: 'state_reset', va: '0x005B9E50', address: va('0x005B9E50')},
  {name: 'deck_prepare', va: '0x00569380', address: va('0x00569380')},
  {name: 'deck_shuffle', va: '0x00568D80', address: va('0x00568D80')}
];
// All four probes are optional and side-effect-free Interceptor observers.
// Selector entry/exit bounds the simulated field evaluation window.
const kAiProbeTargets = [
  {name: 'select_normal', va: '0x005B1360', address: kAiSelectNormal, kind: 'selector'},
  {name: 'select_priority', va: '0x005B16C0', address: va('0x005B16C0'), kind: 'selector'},
  {name: 'combat_event', va: '0x005B0F40', address: va('0x005B0F40'),
    kind: 'output', outputIndex: 3, outputSize: 4},
  {name: 'computed_stats', va: '0x0056DD30', address: va('0x0056DD30'),
    kind: 'output', outputIndex: 2, outputSize: 12}
];
const kQueueCount = va('0x00A558A8');
const kQueueFlags = va('0x00A558AC');
const kCommandStage = va('0x00A5780E');
const kDuelFlags = va('0x00A54E5C');
const kDeckCountBase = va('0x00A55D69');
const kDeckWordBase = va('0x00A56574'); // 32-bit cards before/after shuffle
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
let observerOnly = false;
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
let nativeSummonEligible = null;
let nativeFindMonsterSlot = null;
let nativeSlotLegal = null;
let nativeMonsterIdentity = null;
let nativeMonsterLevelClass = null;
let nativeSummonBuilder = null;
let nativePlayerFlagEmitter = null;
let duelUpdateHook = null;
let speedyOverrideHook = null;
let speedyOverrideValue = null;
let speedyOverrideApplied = false;
let speedyOverrideHits = 0;
let singletonMutexHook = null;
let singletonNamePtr = null;
let singletonName = null;
let singletonMutexHits = 0;
let singletonMutexNameMisses = 0;
let pendingConsoleAction = null;
let lastConsoleAction = null;
let consoleSequence = 0;
// Bounded command trace; recording happens on the calling game thread.
// RPC only retrieves snapshots, so high-frequency commands never spam send().
let commandTraceHook = null;
let traceBuffer = [];
let traceHead = 0;
let traceCapacity = kTraceDefaultCapacity;
let traceSeen = 0;
let traceSession = 0;
let traceExportedSession = 0;
let traceExportedSeen = -1;
let summonPrepHooks = [];
let summonPrepCallSequence = 0;
let nativeAiSelectNormal = null;
let nativeAiForceSet = null;
let aiProbeHooks = [];
let aiProbeCallSequence = 0;
let aiSelectorDepth = new Map();
let demoExecutorHook = null;
let demoExecutorCalls = 0;
let demoSetupHooks = [];
let demoSetupCalls = 0;

// Changing 60 to 120 is deliberately an experiment, not a claim that
// this setting controls FPS: static Ghidra xrefs found only its writer.
function speedStatus() {
  let value = null;
  try { value = kSpeedySetting.readU32(); } catch (_) {}
  return {armed: speedyOverrideHook !== null,
    requested: speedyOverrideValue,
    applied: speedyOverrideApplied,
    writerHits: speedyOverrideHits, value};
}
function armSpeedyOverride(value) {
  if (!installed || !observerOnly || commandTraceHook === null) {
    throw new Error('Speed override requires observer-only mode and trace on');
  }
  if (speedyOverrideHook !== null) throw new Error('Speed override already armed');
  // The target uses a 32-bit unsigned DWORD. Do not impose an arbitrary
  // FPS ceiling, but reject zero, negatives and values that would wrap.
  if (!Number.isSafeInteger(value) || value < 1 || value > 0xFFFFFFFF) {
    throw new Error('Experimental speed value must fit an unsigned 32-bit integer (1..4294967295)');
  }
  const instructionRange = Process.findRangeByAddress(kSpeedyWriteFinished);
  const settingRange = Process.findRangeByAddress(kSpeedySetting);
  if (instructionRange === null || !instructionRange.protection.includes('x') ||
      settingRange === null || !settingRange.protection.includes('w')) {
    throw new Error('Speedy writer/setting is not mapped with expected protections');
  }
  // Validate the exact MOV [005EB618],0x3C observed in Ghidra. Reject other
  // binaries/relocations rather than silently patching an unknown instruction.
  const expected = [0xC7, 0x05, 0x18, 0xB6, 0x5E, 0x00, 0x3C, 0, 0, 0];
  const actual = new Uint8Array(kSpeedyWriteFinished.sub(10).readByteArray(10));
  if (actual.length !== expected.length ||
      actual.some((byte, i) => byte !== expected[i])) {
    throw new Error('Unexpected -speedy writer bytes; refusing speed experiment');
  }
  speedyOverrideValue = value;
  speedyOverrideApplied = false;
  speedyOverrideHits = 0;
  speedyOverrideHook = Interceptor.attach(kSpeedyWriteFinished, {
    onEnter() {
      speedyOverrideHits++;
      if (!speedyOverrideApplied && kSpeedySetting.readU32() === 60) {
        kSpeedySetting.writeU32(speedyOverrideValue);
        speedyOverrideApplied = true;
        recordTraceEvent({kind: 'speedy_override', phase: 'applied',
          previous: 60, requested: speedyOverrideValue,
          writerVa: '0x00411CA2'});
      }
    }
  });
  return speedStatus();
}
function stopSpeedyOverride() {
  if (speedyOverrideHook !== null) {
    const hook = speedyOverrideHook;
    speedyOverrideHook = null;
    hook.detach();
  }
  // Preserve a value changed later by Joey or another tool. Only restore our
  // own value while it still matches what we wrote.
  let restored = false;
  if (speedyOverrideApplied && speedyOverrideValue !== null &&
      kSpeedySetting.readU32() === speedyOverrideValue) {
    kSpeedySetting.writeU32(60);
    restored = true;
  }
  return {restored, ...speedStatus()};
}

// Per-process mutex name avoids changing Windows synchronization globally
// or forging GetLastError. We replace only the name passed by Joey's
// executable-path single-instance constructor, before the game starts.
function multiInstanceStatus() {
  return {armed: singletonMutexHook !== null, remapped: singletonMutexHits,
    skippedUnexpectedName: singletonMutexNameMisses,
    replacementName: singletonName};
}
function armMultiInstance() {
  if (!installed || !observerOnly || commandTraceHook === null) {
    throw new Error('Multi-instance experiment requires observer-only startup and trace on');
  }
  if (singletonMutexHook !== null) throw new Error('Multi-instance hook already armed');
  const range = Process.findRangeByAddress(kSingletonMutexCtor);
  if (range === null || !range.protection.includes('x')) {
    throw new Error('Singleton constructor address not executable');
  }
  const exportAddress = Module.getGlobalExportByName('CreateMutexA');
  if (exportAddress === null) throw new Error('CreateMutexA export unavailable');
  // Unique for concurrent Joey processes. Keep NativePointer alive until
  // the original API call returns and while the hook remains installed.
  const name = 'YugiRE.Joey.' + Process.id;
  const namePtr = Memory.allocUtf8String(name);
  const expected = main.path.replace(/\\/g, '+').toLowerCase();
  singletonMutexHits = 0;
  singletonMutexNameMisses = 0;
  singletonName = name;
  singletonNamePtr = namePtr;
  singletonMutexHook = Interceptor.attach(exportAddress, {
    onEnter(args) {
      const caller = this.returnAddress;
      if (caller.compare(kSingletonMutexCtor) < 0 ||
          caller.compare(kSingletonMutexCtorEnd) >= 0 ||
          args[2].isNull()) {
        return; // not the verified executable-path single-instance check
      }
      const nameFromGame = args[2].readAnsiString();
      if (nameFromGame === null ||
          nameFromGame.toLowerCase() !== expected) {
        singletonMutexNameMisses++;
        return; // do not rename unknown/intra-game synchronization mutexes
      }
      args[2] = namePtr;
      singletonMutexHits++;
      recordTraceEvent({kind: 'singleton_mutex', phase: 'remapped',
        returnAddress: caller.toString(), replacementName: name});
    }
  });
  return multiInstanceStatus();
}
function stopMultiInstance() {
  if (singletonMutexHook !== null) {
    const hook = singletonMutexHook;
    singletonMutexHook = null;
    hook.detach();
  }
  // An already-created Windows mutex lasts until all its handles close.
  // Disarming the hook does NOT rename or destroy an existing object.
  singletonNamePtr = null;
  return multiInstanceStatus();
}

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

// Guarded level 1..4 face-up summons and face-down sets, on original duel thread.
// The eligibility engine may show error notifications, so avoid invoking
// it when inexpensive phase/turn/hand checks already reject the request.
function handCount(side) {
  return kHandCountBase.add(side * kPlayerStride).readU8();
}
function handWord(side, index) {
  return kHandWordBase.add(side * kPlayerStride + index * 4).readU32();
}
function preflightNormalPlacement(request) {
  const fail = detail => ({allowed: false, detail});
  const side = request.side;
  if (kDuelRunning.readU16() === 0) return fail('No active duel');
  const phaseByte = kSummonDuelPhase.readU8();
  const phase = (phaseByte >>> 1) & 7;
  if ((kDuelFlags.readU32() & 1) !== side || (phaseByte & 1) !== side) {
    return fail('Not the selected player turn');
  }
  if (phase !== 2 && phase !== 4) return fail('Not a main phase');
  const playerFlags = kSummonPlayerFlags.add(side * kPlayerStride).readU32();
  if ((playerFlags & 0x20000) !== 0) {
    return {allowed: false, playerFlags, normalSummonUsed: true,
      detail: 'Normal summon/set already used (player flag 0x20000)'};
  }
  if ((kPlacementStage.readU16() & 1) !== 0) {
    return fail('Native placement is already active');
  }
  const count = handCount(side);
  if (count !== request.expectedHandCount || request.handIndex >= count) {
    return fail('Hand size or index changed while pending');
  }
  const card = handWord(side, request.handIndex);
  if ((card & 0xFFF) === 0 || card !== request.expectedWord) {
    return fail('Selected hand card changed while pending');
  }
  if (nativeMonsterIdentity(card) === 0) return fail('Selected card is not a monster');
  const levelClass = nativeMonsterLevelClass(card) & 0xFFFF;
  if (levelClass < 1 || levelClass > 4) {
    return fail('Only ordinary level 1..4 monsters without tributes are supported');
  }
  const eligibility = nativeSummonEligible(side, card) >>> 0;
  // Independent flags in FUN_005AAB90: bit 0 face-up summon, bit 4 set.
  const eligibilityMask = request.kind === 'set' ? 0x10 : 0x01;
  if ((eligibility & eligibilityMask) === 0) {
    return {allowed: false, eligibility, eligibilityMask,
      detail: request.kind === 'set' ?
        'Engine rejects normal face-down set' :
        'Engine rejects normal face-up summon'};
  }
  const selectedSlot = nativeFindMonsterSlot(side) >>> 0;
  if (selectedSlot >= 5) {
    return {allowed: false, eligibility, selectedSlot: null,
      detail: 'No permitted monster slot (including zone blocks)'};
  }
  if ((monsterWord(side, selectedSlot) & 0xFFF) !== 0 ||
      nativeSlotLegal(side, selectedSlot) === 0) {
    return {allowed: false, eligibility, selectedSlot,
      detail: 'Selected monster slot is no longer permitted'};
  }
  return {allowed: true, eligibility, eligibilityMask, selectedSlot,
    playerFlags, normalSummonUsed: false, cardIndex: card & 0xFFF,
    handIndex: request.handIndex, levelClass,
    detail: request.kind === 'set' ?
      'Native set preflight approved' : 'Native summon preflight approved'};
}

// Only called synchronously from Interceptor.onLeave for 0x005BC700,
// on the game's own duel-update thread. Never invoke NativeFunction
// YugiRE_ConsoleDraw from rpc.exports or a Python/Frida RPC thread.
function servicePendingConsoleAction() {
  // RPC stages an action; only Interceptor.onLeave invokes a native
  // producer, preserving the game-thread requirement.
  if (!installed || pendingConsoleAction === null) return;
  if (!queueGate().idle) return;
  // Queue idle does not necessarily mean placement is idle between ticks.
  if ((pendingConsoleAction.kind === 'summon' ||
       pendingConsoleAction.kind === 'set' ||
       pendingConsoleAction.kind === 'ai_normal') &&
      (kPlacementStage.readU16() & 1) !== 0) return;
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
    } else if (request.kind === 'ai_normal') {
      // Record decisions explicitly: native calls originating in an
      // Interceptor callback may not produce nested Interceptor callbacks.
      // This diagnostic path does not cause additional native invocations.
      recordAiConsoleTrace(request, 'start', {
        handCountAtRequest: request.expectedHandCount
      });
      // The AI selector mutates temporary field state; invoke only here on
      // the game's original update thread. No native function calls from RPC.
      const fail = detail => {
        lastConsoleAction = {...request, allowed: false,
          state: request.checkOnly ? 'checked' : 'rejected', detail};
        recordAiConsoleTrace(request, 'decision', {
          allowed: false, state: lastConsoleAction.state, detail
        });
      };
      const phaseByte = kSummonDuelPhase.readU8();
      const phase = (phaseByte >>> 1) & 7;
      if (kDuelRunning.readU16() === 0 ||
          (kDuelFlags.readU32() & 1) !== request.side ||
          (phaseByte & 1) !== request.side || (phase !== 2 && phase !== 4)) {
        fail('Not the player main phase; native AI selection not attempted');
        return;
      }
      if ((kPlacementStage.readU16() & 1) !== 0) {
        fail('Native placement busy; selection not attempted');
        return;
      }
      if ((kSummonPlayerFlags.add(request.side * kPlayerStride).readU32() & 0x20000) !== 0) {
        fail('Normal summon/set already used; selection not attempted');
        return;
      }
      const count = handCount(request.side);
      if (count !== request.expectedHandCount || count > 80) {
        fail('Hand size changed before native AI selection');
        return;
      }
      // Mirror original caller at 0x005B73D2: pass=0 first,
      // only try pass=-1 when first result is exactly -1.
      recordAiConsoleTrace(request, 'select_enter', {selectorPass: 0});
      const firstPass = nativeAiSelectNormal(request.side, 0) | 0;
      recordAiConsoleTrace(request, 'select_leave', {
        selectorPass: 0, selectedHandIndex: firstPass
      });
      let secondPass = null;
      if (firstPass === -1) {
        recordAiConsoleTrace(request, 'select_enter', {selectorPass: -1});
        secondPass = nativeAiSelectNormal(request.side, -1) | 0;
        recordAiConsoleTrace(request, 'select_leave', {
          selectorPass: -1, selectedHandIndex: secondPass
        });
      }
      const handIndex = secondPass === null ? firstPass : secondPass;
      const attempts = {normalPass0: firstPass, tributeFallbackPassMinus1: secondPass};
      if (handIndex < 0) {
        // Explain the result without repeatedly invoking the native
        // eligibility function, which can have user-visible side effects.
        const handCandidates = [];
        for (let i = 0; i < count; i++) {
          const word = handWord(request.side, i);
          if ((word & 0xFFF) === 0) continue;
          const monster = nativeMonsterIdentity(word) !== 0;
          handCandidates.push({handIndex: i, internalIndex: word & 0xFFF,
            monster, levelClass: monster ? nativeMonsterLevelClass(word) & 0xFFFF : null});
        }
        lastConsoleAction = {...request, ...attempts, handCandidates,
          allowed: false, state: request.checkOnly ? 'checked' : 'rejected',
          detail: 'Both native normal-select passes returned no candidate; ' +
            'this does NOT establish that the hand lacks legal summons. ' +
            'Other AI branches use FUN_005B16C0.'};
        recordAiConsoleTrace(request, 'decision', {
          ...attempts, allowed: false, state: lastConsoleAction.state,
          candidateCount: handCandidates.length, detail: lastConsoleAction.detail
        });
        return;
      }
      if (handIndex >= count) {
        lastConsoleAction = {...request, ...attempts,
          allowed: false, state: request.checkOnly ? 'checked' : 'rejected',
          detail: 'AI returned out-of-bounds hand index ' + handIndex};
        recordAiConsoleTrace(request, 'decision', {
          ...attempts, allowed: false, state: lastConsoleAction.state,
          selectedHandIndex: handIndex, detail: lastConsoleAction.detail
        });
        return;
      }
      const card = handWord(request.side, handIndex);
      if ((card & 0xFFF) === 0) {
        lastConsoleAction = {...request, ...attempts,
          allowed: false, state: request.checkOnly ? 'checked' : 'rejected',
          detail: 'AI selected an empty hand slot'};
        recordAiConsoleTrace(request, 'decision', {
          ...attempts, allowed: false, state: lastConsoleAction.state,
          selectedHandIndex: handIndex, detail: lastConsoleAction.detail
        });
        return;
      }
      // Original FUN_005B1360 caller sends modeSelector=0 (summon);
      // FUN_005B6FF0 overrides to set only if FUN_00570A70 is zero.
      // Do not mix in FUN_005AE270: it belongs to FUN_005B16C0 paths.
      const forceSetRaw = nativeAiForceSet(request.side) | 0;
      const kind = forceSetRaw === 0 ? 'set' : 'summon';
      const choice = {...attempts, handIndex,
        selectedInternalIndex: card & 0xFFF, modeSelector: 0,
        forceSetRaw, selectedKind: kind,
        selectorPass: secondPass === null ? 0 : -1};
      const preflight = preflightNormalPlacement({
        ...request, kind, handIndex, expectedHandCount: count, expectedWord: card
      });
      if (request.checkOnly || !preflight.allowed) {
        lastConsoleAction = {...request, ...choice, ...preflight,
          state: request.checkOnly ? 'checked' : 'rejected',
          detail: 'AI selected ' + kind + '; ' + preflight.detail};
        recordAiConsoleTrace(request, 'decision', {
          ...attempts, handIndex, internalIndex: card & 0xFFF,
          selectedKind: kind, selectedSlot: preflight.selectedSlot ?? null,
          allowed: preflight.allowed, state: lastConsoleAction.state,
          detail: lastConsoleAction.detail
        });
        return;
      }
      const mode = kind === 'set' ? 0 : 1;
      nativeSummonBuilder(request.side, handIndex, preflight.selectedSlot, 0, mode);
      nativePlayerFlagEmitter(request.side, 0x11, 1);
      lastConsoleAction = {...request, ...choice, ...preflight, mode,
        state: 'submitted', detail: 'AI-selected ' + kind +
          ' submitted; verify eventual field resolution with diff'};
      recordAiConsoleTrace(request, 'decision', {
        ...attempts, handIndex, internalIndex: card & 0xFFF,
        selectedKind: kind, selectedSlot: preflight.selectedSlot,
        allowed: true, state: 'submitted',
        detail: lastConsoleAction.detail
      });
      return;
    } else if (request.kind === 'summon' || request.kind === 'set') {
      const preflight = preflightNormalPlacement(request);
      if (request.checkOnly || !preflight.allowed) {
        lastConsoleAction = {...request, ...preflight,
          state: request.checkOnly ? 'checked' : 'rejected'};
        return;
      }
      // The original engine is responsible for all later placement ticks.
      const mode = request.kind === 'set' ? 0 : 1;
      nativeSummonBuilder(request.side, request.handIndex,
        preflight.selectedSlot, 0, mode);
      // Both operations consume the shared normal-summon bit 17.
      nativePlayerFlagEmitter(request.side, 0x11, 1);
      lastConsoleAction = {...request, ...preflight, mode, state: 'submitted',
        detail: 'Native ' + request.kind +
          ' builder invoked; verify eventual field state with diff'};
      return;
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
  if (observerOnly) throw new Error('Observer-only mode: native actions disabled');
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

function queueConsoleMonsterPlacement(kind, side, index, checkOnly) {
  if ((kind !== 'summon' && kind !== 'set') || side !== 1 ||
      !Number.isInteger(index) || index < 0 || index >= 80 ||
      typeof checkOnly !== 'boolean') {
    throw new Error('Use summon/set [check] 1 <handIndex 0..79>; player side only');
  }
  // Snapshot at RPC time; validate same identity again on the duel thread.
  const count = handCount(side);
  if (count > 80 || index >= count) throw new Error('Selected hand index is not present');
  const card = handWord(side, index);
  if ((card & 0xFFF) === 0) throw new Error('Selected hand card is empty');
  return stageConsoleAction({kind, side, handIndex: index,
    expectedHandCount: count, expectedWord: card, expectedIndex: card & 0xFFF,
    checkOnly});
}
function queueConsoleSummon(side, index, checkOnly = false) {
  return queueConsoleMonsterPlacement('summon', side, index, checkOnly);
}
function queueConsoleSet(side, index, checkOnly = false) {
  return queueConsoleMonsterPlacement('set', side, index, checkOnly);
}

function prepareAiSelection() {
  if (nativeAiSelectNormal !== null) return;
  for (const address of [kAiSelectNormal, kAiForceSet]) {
    const range = Process.findRangeByAddress(address);
    if (range === null || !range.protection.includes('x')) {
      throw new Error('AI native function not executable: ' + address);
    }
  }
  // Instantiating NativeFunction does not run Joey; actual calls happen
  // on the original game update thread inside servicePendingConsoleAction.
  nativeAiSelectNormal = new NativeFunction(kAiSelectNormal,
    'int32', ['uint32', 'int32'], 'mscdecl');
  nativeAiForceSet = new NativeFunction(kAiForceSet,
    'int32', ['uint32'], 'mscdecl');
}

function queueAiDecision(side, checkOnly) {
  if (observerOnly) throw new Error('Observer-only mode: active AI disabled');
  if (side !== 1 || typeof checkOnly !== 'boolean') {
    throw new Error('Use ai check 1 or ai step 1 (player side only)');
  }
  if (!installed) throw new Error('AI experiment requires installed adapter');
  if (pendingConsoleAction !== null) {
    throw new Error('A console action is already pending');
  }
  prepareAiSelection();
  const count = handCount(1);
  if (count > 80) throw new Error('Player hand count exceeds the safe limit');
  return stageConsoleAction({kind: 'ai_normal', side, checkOnly,
    expectedHandCount: count});
}

function cancelPendingConsoleAction() {
  if (pendingConsoleAction === null) return {state: 'none'};
  const request = pendingConsoleAction;
  pendingConsoleAction = null;
  lastConsoleAction = {...request, state: 'cancelled',
    detail: 'Cancelled before the duel scheduler accepted it'};
  return lastConsoleAction;
}

// Record console-driven AI calls separately from passive Interceptor hooks.
// Frida may suppress nested Interceptor callbacks for native calls from
// an Interceptor callback. These are verified call-site observations,
// NOT an explanation of the candidate score or confirmed field resolution.
function recordAiConsoleTrace(request, phase, details = {}) {
  if (commandTraceHook === null || aiProbeHooks.length === 0) return;
  recordTraceEvent({
    kind: 'ai_console', origin: 'yugire_console_native_call',
    requestId: request.id, side: request.side,
    checkOnly: request.checkOnly,
    phase, threadId: Process.getCurrentThreadId(),
    ...details
  });
}

// Trace records raw producer inputs; their semantics differ by opcode.
// Calls to 0x005B9170 have been observed with four 32-bit arguments for
// this exact EXE. Sequence is monotonically increasing within a session.
function traceStatus() {
  return {
    active: commandTraceHook !== null,
    prepActive: summonPrepHooks.length > 0,
    prepHooks: summonPrepHooks.length,
    aiActive: aiProbeHooks.length > 0,
    aiHooks: aiProbeHooks.length,
    demoActive: demoExecutorHook !== null,
    demoHooks: (demoExecutorHook === null ? 0 : 1) + demoSetupHooks.length,
    session: traceSession,
    producer: kCommandProducer.toString(),
    capacity: traceCapacity,
    seen: traceSeen,
    retained: traceBuffer.length,
    overwritten: traceSeen - traceBuffer.length,
    firstSeq: traceBuffer.length === 0 ? null :
      traceBuffer[traceHead].seq,
    hasUnsaved: traceBuffer.length > 0 &&
      (traceSession !== traceExportedSession || traceSeen !== traceExportedSeen)
  };
}

function recordTraceEvent(event) {
  event.seq = ++traceSeen;
  event.timestampMs = Date.now();
  if (traceBuffer.length < traceCapacity) {
    traceBuffer.push(event);
  } else {
    traceBuffer[traceHead] = event;
    traceHead = (traceHead + 1) % traceCapacity;
  }
}

// No writes or calls into Joey. Sample raw globals on its own game thread.
function readSummonPrepState() {
  try {
    return {
      context: kPlacementContext.readU32(),
      source: kPlacementSource.readU32(),
      stage: kPlacementStage.readU16(),
      auxFlags: kPlacementAuxFlags.readU16(),
      turnFlags: kDuelFlags.readU32(),
      duelPhaseRaw: kSummonDuelPhase.readU8(),
      playerFlags: [kSummonPlayerFlags.readU32(),
        kSummonPlayerFlags.add(kPlayerStride).readU32()],
      queueCount: kQueueCount.readU32()
    };
  } catch (error) {
    return {readError: String(error)};
  }
}

function isSummonDispatcherActive(state) {
  // FUN_005AB820 mode 1 when ((stage>>2)&7) == 1.
  return Number.isInteger(state.stage) &&
    (state.stage & 1) !== 0 && ((state.stage >> 2) & 7) === 1;
}

function stopSummonPrepProbe() {
  const hooks = summonPrepHooks;
  summonPrepHooks = [];
  let failure = null;
  for (let i = hooks.length - 1; i >= 0; i--) {
    try { hooks[i].detach(); } catch (error) { failure = error; }
  }
  if (failure !== null) throw new Error('Summon prep hook detach failed: ' + failure);
  return traceStatus();
}

function startSummonPrepProbe() {
  if (!installed || commandTraceHook === null) {
    throw new Error('Start trace on before enabling the summon prep probe');
  }
  if (summonPrepHooks.length > 0) throw new Error('Summon prep probe is already active');
  for (const target of kSummonPrepTargets) {
    const range = Process.findRangeByAddress(target.address);
    if (range === null || !range.protection.includes('x')) {
      throw new Error('Summon prep target is not executable: ' + target.va);
    }
  }
  const attached = [];
  try {
    for (const target of kSummonPrepTargets) {
      const hook = Interceptor.attach(target.address, {
        onEnter(args) {
          const state = readSummonPrepState();
          this.prepObserved = target.name !== 'dispatcher' ||
            isSummonDispatcherActive(state);
          if (!this.prepObserved) return;
          this.prepCallId = ++summonPrepCallSequence;
          const event = {
            kind: 'summon_prep', phase: 'enter', function: target.name,
            functionVa: target.va, callId: this.prepCallId,
            threadId: Process.getCurrentThreadId(),
            caller: this.returnAddress.toString(), state
          };
          if (target.argCount > 0) {
            event.argsRaw = Array.from({length: target.argCount},
              (_, i) => args[i].toUInt32());
          }
          if (target.name === 'normal_builder') {
            // Meaning of these five arguments is supported by the Ghidra
            // builder body. Raw values retained for unusual duel effects.
            const [sideRaw, handIndexRaw, fieldSlotRaw, tributeData, modeRaw] = event.argsRaw;
            event.builderIntent = {
              side: sideRaw & 1,
              handIndex: handIndexRaw & 0xFF,
              fieldSlot: fieldSlotRaw | 0,
              tributeData,
              modeRaw,
              modeCandidate: modeRaw === 0 ? 'set' : 'summon',
              fieldSlotValid: (fieldSlotRaw | 0) >= 0 && (fieldSlotRaw | 0) < 5
            };
          }
          recordTraceEvent(event);
        },
        onLeave(retval) {
          if (!this.prepObserved) return;
          recordTraceEvent({
            kind: 'summon_prep', phase: 'leave', function: target.name,
            functionVa: target.va, callId: this.prepCallId,
            threadId: Process.getCurrentThreadId(),
            returnRaw: retval.toUInt32(),
            state: readSummonPrepState()
          });
        }
      });
      attached.push(hook);
    }
  } catch (error) {
    for (let i = attached.length - 1; i >= 0; i--) {
      try { attached[i].detach(); } catch (_) { /* rollback best effort */ }
    }
    throw new Error('Summon prep probe install failed; hooks rolled back: ' + error);
  }
  summonPrepHooks = attached;
  summonPrepCallSequence = 0;
  return traceStatus();
}

// Extract raw native output bytes without interpreting undocumented rules.
// The bytes can be decoded offline by the two scripts under scripts/.
function readAiProbeHex(pointer, count) {
  if (pointer === null || pointer.isNull()) throw new Error('null output pointer');
  const range = Process.findRangeByAddress(pointer);
  if (range === null || !range.protection.includes('r')) {
    throw new Error('output pointer is not readable');
  }
  if (pointer.add(count - 1).compare(range.base.add(range.size)) >= 0) {
    throw new Error('output pointer crosses a memory range');
  }
  let hex = '';
  for (let i = 0; i < count; i++) {
    hex += pointer.add(i).readU8().toString(16).padStart(2, '0');
  }
  return hex.toUpperCase();
}

// This is a *passive* probe: never invokes FUN_005B0F40 or FUN_0056DD30.
// Only events nested inside an observed selector are kept to avoid renderer noise.
function stopAiProbe() {
  const hooks = aiProbeHooks;
  aiProbeHooks = [];
  aiSelectorDepth = new Map();
  let failure = null;
  for (let i = hooks.length - 1; i >= 0; i--) {
    try { hooks[i].detach(); } catch (error) { failure = error; }
  }
  if (failure !== null) throw new Error('AI probe detach failed: ' + failure);
  return traceStatus();
}

function startAiProbe() {
  if (!installed || commandTraceHook === null) {
    throw new Error('Start trace on before trace ai on');
  }
  if (aiProbeHooks.length > 0) throw new Error('AI probe is already active');
  for (const target of kAiProbeTargets) {
    const range = Process.findRangeByAddress(target.address);
    if (range === null || !range.protection.includes('x')) {
      throw new Error('AI probe function not executable: ' + target.va);
    }
  }
  const hooks = [];
  try {
    for (const target of kAiProbeTargets) {
      const hook = Interceptor.attach(target.address, {
        onEnter(args) {
          this.aiThread = Process.getCurrentThreadId();
          this.aiCallId = ++aiProbeCallSequence;
          this.aiSide = args[0].toUInt32() & 1;
          this.aiIsSelector = target.kind === 'selector';
          if (this.aiIsSelector) {
            aiSelectorDepth.set(this.aiThread,
              (aiSelectorDepth.get(this.aiThread) || 0) + 1);
            recordTraceEvent({
              kind: 'ai_probe', phase: 'enter', function: target.name,
              functionVa: target.va, callId: this.aiCallId,
              threadId: this.aiThread, side: this.aiSide,
              caller: this.returnAddress.toString(),
              pass: target.name === 'select_normal' ? args[1].toInt32() : null
            });
          } else {
            this.aiInsideSelection = (aiSelectorDepth.get(this.aiThread) || 0) > 0;
            if (this.aiInsideSelection) {
              this.aiBuffer = args[target.outputIndex];
              this.aiArguments = target.name === 'combat_event' ?
                [args[0].toUInt32(), args[1].toUInt32(), args[2].toUInt32()] :
                [args[0].toUInt32(), args[1].toUInt32()];
            }
          }
        },
        onLeave(retval) {
          if (this.aiIsSelector) {
            const previous = aiSelectorDepth.get(this.aiThread) || 0;
            if (previous <= 1) aiSelectorDepth.delete(this.aiThread);
            else aiSelectorDepth.set(this.aiThread, previous - 1);
            recordTraceEvent({
              kind: 'ai_probe', phase: 'leave', function: target.name,
              functionVa: target.va, callId: this.aiCallId,
              threadId: this.aiThread, side: this.aiSide,
              returnRaw: retval.toUInt32(), selectedHandIndex: retval.toInt32()
            });
            return;
          }
          if (!this.aiInsideSelection) return;
          const event = {
            kind: 'ai_probe', phase: 'leave', function: target.name,
            functionVa: target.va, callId: this.aiCallId,
            threadId: this.aiThread, side: this.aiSide,
            withinSelector: true, rawArgs: this.aiArguments,
            byteCount: target.outputSize
          };
          try {
            event.outputHex = readAiProbeHex(this.aiBuffer, target.outputSize);
          } catch (error) {
            event.readError = String(error);
          }
          recordTraceEvent(event);
        }
      });
      hooks.push(hook);
    }
  } catch (error) {
    for (let i = hooks.length - 1; i >= 0; i--) {
      try { hooks[i].detach(); } catch (_) { /* best effort rollback */ }
    }
    throw new Error('AI probe installation failed; rolled back: ' + error);
  }
  aiProbeHooks = hooks;
  aiProbeCallSequence = 0;
  aiSelectorDepth = new Map();
  return traceStatus();
}

// Passive instrumentation only: never invokes a Joey function or changes
// native state. A placement executor call alone cannot prove AI control.
// Read native deck data without invoking the randomizer or changing memory.
function readDemoDeckSource(slot) {
  const base = kDemoSourceDeck0.add(slot * kDemoSourceDeckStride);
  const mainCount = base.add(0xDC).readU16();
  const secondCount = base.add(0xDE).readU16();
  const thirdCount = base.add(0xE0).readU16();
  if (mainCount > 80 || secondCount > 15 || thirdCount > 15) {
    return {slot, mainCount, secondCount, thirdCount,
      error: 'Source deck count exceeds 80/15/15 bounds'};
  }
  const readSection = (offset, count) => {
    const raw = [];
    for (let i = 0; i < count; i++) {
      raw.push(base.add(offset + i * 2).readU16());
    }
    return raw;
  };
  const mainRaw = readSection(0, mainCount);
  return {
    slot, mainCount, secondCount, thirdCount,
    mainIndices: mainRaw.map(v => v & 0xFFF),
    secondIndices: readSection(0xA0, secondCount).map(v => v & 0xFFF),
    thirdIndices: readSection(0xBE, thirdCount).map(v => v & 0xFFF)
  };
}
function safeDemoDeckSource(slot) {
  try { return readDemoDeckSource(slot); }
  catch (error) { return {slot, readError: String(error)}; }
}
function demoSourceConfig() {
  try {
    const mode = kDemoModeWord.readU32();
    return {sceneMode: kDemoSceneMode.readU32(),
      duelModeWord: mode >>> 0, deckSideBit: mode & 1,
      launchGlobals: [
        va('0x005F3288').readU32(), va('0x005F328C').readU32(),
        va('0x005F3290').readU32()
      ]};
  } catch (error) {
    return {readError: String(error)};
  }
}

function readDemoDeck(side) {
  const count = kDeckCountBase.add(side * kPlayerStride).readU8();
  if (count > 80) return {side, count, error: 'invalid deck count (>80)'};
  const words = [], indices = [];
  for (let i = 0; i < count; i++) {
    const word = kDeckWordBase.add(side * kPlayerStride + i * 4).readU32();
    words.push(word >>> 0);
    indices.push(word & 0xFFF);
  }
  return {side, count, words, indices};
}
function safeDemoDeck(side) {
  try { return readDemoDeck(side); }
  catch (error) { return {side, readError: String(error)}; }
}

function stopDemoProbe() {
  const hooks = demoSetupHooks;
  demoSetupHooks = [];
  const executor = demoExecutorHook;
  demoExecutorHook = null;
  let failure = null;
  for (let i = hooks.length - 1; i >= 0; i--) {
    try { hooks[i].detach(); } catch (error) { failure = error; }
  }
  if (executor !== null) {
    try { executor.detach(); } catch (error) { failure = error; }
  }
  if (failure !== null) throw new Error('Demo probe detach failed: ' + failure);
  return traceStatus();
}

function startDemoProbe() {
  if (!installed || commandTraceHook === null) {
    throw new Error('Start trace on before trace demo on');
  }
  if (demoExecutorHook !== null) throw new Error('Demo executor probe already active');
  for (const target of [
    {va: '0x005B6FF0', address: kDemoNormalExecutor}, ...kDemoSetupTargets
  ]) {
    const range = Process.findRangeByAddress(target.address);
    if (range === null || !range.protection.includes('x')) {
      throw new Error('Demo hook target is not executable: ' + target.va);
    }
  }
  const hook = Interceptor.attach(kDemoNormalExecutor, {
    onEnter(args) {
      const sideRaw = args[0].toUInt32();
      const handIndex = args[1].toInt32();
      this.demoCallId = ++demoExecutorCalls;
      this.demoThreadId = Process.getCurrentThreadId();
      this.demoSide = sideRaw;
      const event = {
        kind: 'demo_executor', function: 'normal_placement',
        functionVa: '0x005B6FF0', phase: 'enter',
        callId: this.demoCallId, threadId: this.demoThreadId,
        sideRaw, side: sideRaw <= 1 ? sideRaw : null,
        handIndex, modeSelector: args[2].toInt32(),
        caller: this.returnAddress.toString()
      };
      // Protect against stale menu memory; never read out-of-range hands.
      try {
        event.duelRunning = kDuelRunning.readU16() !== 0;
        if (event.duelRunning && sideRaw <= 1 &&
            handIndex >= 0 && handIndex < 80) {
          const count = handCount(sideRaw);
          event.handCount = count;
          if (count <= 80 && handIndex < count) {
            event.handInternalIndex = handWord(sideRaw, handIndex) & 0xFFF;
          }
        }
      } catch (error) {
        event.readError = String(error);
      }
      recordTraceEvent(event);
    },
    onLeave(retval) {
      recordTraceEvent({
        kind: 'demo_executor', function: 'normal_placement',
        functionVa: '0x005B6FF0', phase: 'leave',
        callId: this.demoCallId, threadId: this.demoThreadId,
        sideRaw: this.demoSide,
        side: this.demoSide <= 1 ? this.demoSide : null,
        returnRaw: retval.toUInt32()
      });
    }
  });
  // Observe the duel bootstrap and deck *shuffle*, not the unknown
  // menu idle trigger or the yet-unidentified deck population routine.
  // Install all-or-nothing; if the newer hooks fail, roll back executor.
  const setup = [];
  try {
    for (const target of kDemoSetupTargets) {
      setup.push(Interceptor.attach(target.address, {
        onEnter(args) {
          this.setupId = ++demoSetupCalls;
          this.setupThreadId = Process.getCurrentThreadId();
          this.setupSide = null;
          this.setupSceneConfig = null;
          const event = {
            kind: 'demo_init', function: target.name, functionVa: target.va,
            phase: 'enter', callId: this.setupId,
            threadId: this.setupThreadId, caller: this.returnAddress.toString()
          };
          if (target.name === 'scene_config' ||
              target.name === 'scene_factory' ||
              target.name === 'deck_resource_step') {
            this.setupSceneConfig = demoSourceConfig();
            event.config = this.setupSceneConfig;
            if (target.name === 'deck_resource_step') {
              event.argument0Raw = args[0].toUInt32();
              // Two small 0xE2-byte source records; capture only at
              // the boundaries of the existing resource-loader call.
              event.sourceDecks = [safeDemoDeckSource(0), safeDemoDeckSource(1)];
            }
            if (target.name === 'scene_config') {
              event.requestedSceneMode = args[0].toUInt32();
              event.sourceDecks = [safeDemoDeckSource(0), safeDemoDeckSource(1)];
            }
            if (target.name === 'scene_factory') {
              // thiscall ECX is excluded by Frida from stack args.
              // Treat this as an opaque stack value until its ABI is checked.
              event.stackArg0Raw = args[0].toUInt32();
            }
          }
          if (target.name === 'duel_entry' || target.name === 'state_reset') {
            event.config = demoSourceConfig();
            event.sourceDecks = [safeDemoDeckSource(0), safeDemoDeckSource(1)];
          }
          if (target.name === 'deck_shuffle' || target.name === 'deck_prepare') {
            const sideRaw = args[0].toUInt32();
            this.setupSide = sideRaw;
            event.sideRaw = sideRaw;
            event.side = sideRaw < 2 ? sideRaw : null;
            if (target.name === 'deck_shuffle') {
              event.shufflePasses = args[1].toInt32();
            }
            if (sideRaw < 2) {
              event.deck = safeDemoDeck(sideRaw);
              if (target.name === 'deck_prepare') {
                const config = demoSourceConfig();
                event.config = config;
                if (Number.isInteger(config.deckSideBit)) {
                  const sourceSlot = sideRaw ^ config.deckSideBit;
                  event.sourceSlot = sourceSlot;
                  event.sourceDeck = safeDemoDeckSource(sourceSlot);
                }
              }
            }
          } else if (target.name === 'state_reset') {
            event.decks = [safeDemoDeck(0), safeDemoDeck(1)];
          }
          recordTraceEvent(event);
        },
        onLeave() {
          const event = {
            kind: 'demo_init', function: target.name, functionVa: target.va,
            phase: 'leave', callId: this.setupId,
            threadId: this.setupThreadId
          };
          if (target.name === 'scene_config' ||
              target.name === 'scene_factory' ||
              target.name === 'deck_resource_step') {
            event.config = demoSourceConfig();
            if (target.name === 'deck_resource_step' ||
                target.name === 'scene_config') {
              event.sourceDecks = [safeDemoDeckSource(0), safeDemoDeckSource(1)];
            }
            if (this.setupSceneConfig?.sceneMode !== undefined &&
                event.config.sceneMode !== undefined) {
              event.sceneModeChanged =
                event.config.sceneMode !== this.setupSceneConfig.sceneMode;
            }
          }
          if ((target.name === 'deck_shuffle' || target.name === 'deck_prepare') &&
              this.setupSide !== null && this.setupSide < 2) {
            event.side = this.setupSide;
            event.deck = safeDemoDeck(this.setupSide);
          } else if (target.name === 'state_reset') {
            event.decks = [safeDemoDeck(0), safeDemoDeck(1)];
            event.config = demoSourceConfig();
          }
          recordTraceEvent(event);
        }
      }));
    }
  } catch (error) {
    for (let i = setup.length - 1; i >= 0; i--) {
      try { setup[i].detach(); } catch (_) {}
    }
    try { hook.detach(); } catch (_) {}
    throw new Error('Demo setup hook installation failed; rolled back: ' + error);
  }
  demoExecutorHook = hook;
  demoSetupHooks = setup;
  demoExecutorCalls = 0;
  demoSetupCalls = 0;
  return traceStatus();
}

function startCommandTrace(capacity = kTraceDefaultCapacity) {
  if (!installed) throw new Error('Trace requires installed native adapter');
  if (commandTraceHook !== null) throw new Error('Command trace is already active');
  if (!Number.isInteger(capacity) || capacity < 16 || capacity > kTraceMaxCapacity) {
    throw new Error('Trace capacity must be an integer from 16 to 20000');
  }
  const range = Process.findRangeByAddress(kCommandProducer);
  if (range === null || !range.protection.includes('x')) {
    throw new Error('0x005B9170 is not executable in this process');
  }
  // Attaching may fail on an unsupported build or a short prologue.
  // Keep prior trace data intact if it fails.
  const hook = Interceptor.attach(kCommandProducer, {
    onEnter(args) {
      // Only read the four stack arguments; no engine state manipulation.
      // The general observer reads four stack DWORDs, but the CE callsite
      // in FUN_005ABD00 passes only TWO defined arguments; the tail may
      // be caller stack residue, not genuine producer parameters.
      const raw = args[0].toUInt32();
      const event = {
        kind: 'command',
        threadId: Process.getCurrentThreadId(),
        caller: this.returnAddress.toString(),
        args: [raw, args[1].toUInt32(),
               args[2].toUInt32(), args[3].toUInt32()]
      };
      if ((raw & 0x7FFF) === 0xCE &&
          this.returnAddress.equals(kCeCallsiteReturn)) {
        // Sampling these globals at the producer entry preserves the
        // complete context before the 32-bit left shift loses high bits.
        // The descriptor fields are raw: do not infer legal summons.
        try {
          event.ceNativeState = {
            context: kPlacementContext.readU32(),
            source: kPlacementSource.readU32(),
            stage: kPlacementStage.readU16()
          };
        } catch (readError) {
          event.ceNativeReadError = String(readError);
        }
      }
      recordTraceEvent(event);
    }
  });
  traceBuffer = [];
  traceHead = 0;
  traceSeen = 0;
  traceCapacity = capacity;
  traceSession++;
  commandTraceHook = hook;
  return traceStatus();
}

function stopCommandTrace() {
  // The probe must stop before anyone can export this bounded buffer.
  stopDemoProbe();
  stopAiProbe();
  stopSummonPrepProbe();
  if (commandTraceHook !== null) {
    const hook = commandTraceHook;
    commandTraceHook = null;
    hook.detach();
  }
  return traceStatus();
}

function markCommandTrace(label) {
  if (commandTraceHook === null) throw new Error('Start trace before marking');
  if (typeof label !== 'string' || label.length === 0 || label.length > 120 ||
      /[\r\n]/.test(label)) {
    throw new Error('Marker label must contain 1..120 chars without line breaks');
  }
  // Marker comes from RPC, not the game's duel thread; do not treat its
  // timestamp as proof of command completion.
  recordTraceEvent({kind: 'marker', label});
  return traceStatus();
}

function acknowledgeTraceExport(session, seen) {
  if (commandTraceHook !== null) throw new Error('Stop trace before acknowledging export');
  if (!Number.isInteger(session) || !Number.isInteger(seen) ||
      session !== traceSession || seen !== traceSeen) {
    throw new Error('Trace changed since export started; refusing acknowledgement');
  }
  traceExportedSession = session;
  traceExportedSeen = seen;
  return traceStatus();
}

function commandTraceEvents(offset, count = 256) {
  if (commandTraceHook !== null) throw new Error('Stop trace before exporting');
  if (!Number.isInteger(offset) || offset < 0 ||
      !Number.isInteger(count) || count < 1 || count > 256) {
    throw new Error('Trace pagination requires offset >= 0 and count 1..256');
  }
  const retained = traceBuffer.length;
  const result = [];
  for (let i = offset; i < Math.min(retained, offset + count); i++) {
    result.push(traceBuffer[(traceHead + i) % retained]);
  }
  return result;
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
    const offset = side * kSnapshotPlayerWords * 4;
    const word = n => buffer.add(offset + n * 4).readU32();
    const series = (start, count) => Array.from({length: count}, (_, i) => word(start+i));
    const handCount = word(1);
    return {
      side, lifePoints: word(0), handCount, deckCount: word(2),
      graveyardCount: word(3), banishedCount: word(4),
      terminalFlags: word(5),
      monsterIndices: series(6, 5),
      otherFieldIndices: series(11, 6),
      fieldPositionWords: series(17, 11),
      handIndices: series(28, Math.min(handCount, 80)),
      handTruncated: handCount > 80
    };
  });
  return {sides, note: 'Non-atomic read; internal card indices. Physical slots 5..10 have provisional UI mapping.'};
}

function status() {
  // During pre-resume observation the callback table may be uninitialized.
  const entries = observerOnly ? [] : cards.map(card => ({
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
    observerOnly,
    trace: traceStatus(),
    modulePath: main.path,
    processId: Process.id,
    dllBase: dllBase === null ? null : dllBase.toString(),
    console: {
      batchesQueued: getManualDrawBatches === null ? 0 : getManualDrawBatches(),
      damageCommandsQueued: getManualDamageCommands === null ? 0 : getManualDamageCommands(),
      destroyAttempts: getManualDestroyAttempts === null ? 0 : getManualDestroyAttempts(),
      pending: pendingConsoleAction,
      last: lastConsoleAction,
      gate: installed && !observerOnly ? queueGate() : null
    },
    entries,
  };
}

// The restore is idempotent and also handles a failed install that only
// managed to patch the first card. It checks both slots BEFORE changing any.
function restore() {
  stopCommandTrace();
  stopSpeedyOverride();
  stopMultiInstance();
  stopConsoleHook(); // also drops any unserviced command before unpatching
  if (observerOnly) {
    // Observer mode never modifies callbacks. Avoid reading early card records.
    installed = false;
    observerOnly = false;
    return {installed: false, entries: [], trace: traceStatus()};
  }
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
  observerOnly = false;
  if (errors.length > 0) throw new Error('Partial restore: ' + errors.join('; '));
  return status();
}

rpc.exports = {
  status,
  speedstatus: speedStatus,
  multistatus: multiInstanceStatus,
  armmultiinstance: armMultiInstance,
  stopmultiinstance: stopMultiInstance,
  armspeedoverride: armSpeedyOverride,
  stopspeedoverride: stopSpeedyOverride,
  bootstrapinfo() {
    // Safe before the original executable has initialized its card tables.
    return {modulePath: main.path, processId: Process.id};
  },
  armstartupobserver() {
    if (installed) throw new Error('Already installed');
    // Pre-resume: individual trace hooks validate their executable targets.
    // No DLL load, callback patch, or native mutating call is performed.
    observerOnly = true;
    installed = true;
    return {installed: true, observerOnly: true};
  },
  validateruntime() {
    if (!installed || !observerOnly) {
      throw new Error('Runtime validation requires an active observer');
    }
    verifyBeforeLoading();
    return {ok: true};
  },
  duelstate: duelState,
  tracestatus: traceStatus,
  tracestart: startCommandTrace,
  tracestop: stopCommandTrace,
  traceprepstart: startSummonPrepProbe,
  traceaistart: startAiProbe,
  traceaistop: stopAiProbe,
  tracedemostart: startDemoProbe,
  tracedemostop: stopDemoProbe,
  traceprepstop: stopSummonPrepProbe,
  tracemark: markCommandTrace,
  traceevents: commandTraceEvents,
  traceacknowledge: acknowledgeTraceExport,
  queuedraw: queueConsoleDraw,
  queuedamage: queueConsoleDamage,
  queuedestroy: queueConsoleDestroy,
  queuesummon: queueConsoleSummon,
  queueset: queueConsoleSet,
  queueaidecision: queueAiDecision,
  canceldraw: cancelPendingConsoleAction,
  // No DLL load, no card callback patch, no duel-update hook.
  // Only opt-in passive trace hooks are permitted.
  installobserver() {
    if (installed) throw new Error('Already installed');
    verifyBeforeLoading();
    observerOnly = true;
    installed = true;
    return status();
  },
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

    // Bind native routines; never invoke them from the RPC thread.
    for (const addr of [kSummonEligible, kFindMonsterSlot, kSlotLegal,
        kMonsterIdentity, kMonsterLevelClass, kNormalSummonBuilder,
        kPlayerFlagEmitter]) {
      const range = Process.findRangeByAddress(addr);
      if (range === null || !range.protection.includes('x')) {
        throw new Error('Summon native routine is not executable: ' + addr);
      }
    }
    nativeSummonEligible = new NativeFunction(kSummonEligible,
      'uint32', ['uint32', 'uint32'], 'mscdecl');
    nativeFindMonsterSlot = new NativeFunction(kFindMonsterSlot,
      'uint32', ['uint32'], 'mscdecl');
    nativeSlotLegal = new NativeFunction(kSlotLegal,
      'uint32', ['uint32','uint32'], 'mscdecl');
    nativeMonsterIdentity = new NativeFunction(kMonsterIdentity,
      'uint32', ['uint32'], 'mscdecl');
    nativeMonsterLevelClass = new NativeFunction(kMonsterLevelClass,
      'uint32', ['uint32'], 'mscdecl');
    nativeSummonBuilder = new NativeFunction(kNormalSummonBuilder,
      'void', ['uint32','uint32','uint32','uint32','uint32'], 'mscdecl');
    nativePlayerFlagEmitter = new NativeFunction(kPlayerFlagEmitter,
      'void', ['uint32','uint32','uint32'], 'mscdecl');

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
