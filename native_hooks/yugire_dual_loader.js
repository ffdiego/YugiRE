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
// 005BFEB8: CALL FUN_005C3BF5, returns to 005BFEBD.
// Only this call sets the seed for the upcoming duel; other callers may
// reseed when resolving card effects and MUST NOT be overridden.
const kDemoDuelSeedCallerReturn = va('0x005BFEBD');
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
  {name: 'deck_shuffle', va: '0x00568D80', address: va('0x00568D80')},
  // Ghidra confirms 005C3BF5 writes RNG state at TLS+0x14, used by _rand.
  {name: 'rng_seed_candidate', va: '0x005C3BF5', address: va('0x005C3BF5')}
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
// Additional FUNCTION-ENTRY listeners only, enabled explicitly by
// 'trace ai on detail'. No mid-instruction probes or native calls.
const kAiDetailTargets = [
  {name:'baseline', va:'0x005B08B0', address:va('0x005B08B0')},
  {name:'simulate_card', va:'0x005B1610', address:va('0x005B1610')},
  {name:'aggregate_score', va:'0x005B0A50', address:va('0x005B0A50')},
  {name:'select_combat_event', va:'0x005B0B90', address:va('0x005B0B90')},
  {name:'restore_field', va:'0x005B0950', address:va('0x005B0950')}
];
const kAiSummary = va('0x00A579AA');
const kAiActionMaskOutput = va('0x00A579AC');
const kAiChosenEvent = va('0x00A579A6');
const kAiActionMaskBySide = va('0x00A55D70');
const kAiOuterBaselineCaller = va('0x005B738A');
// Direct CALL sites in FUN_005B1360 (near CALL E8 occupies five bytes).
// The return address filter excludes effects/callbacks invoking the same
// functions inside hypothetical combat or unrelated UI paths.
const kAiGateTargets = [
  {name:'eligibility_mask', va:'0x005AAB90', address:va('0x005AAB90'),
    returnVa:'0x005B13E5', returnAddress:va('0x005B13E5')},
  {name:'resource_policy', va:'0x005AE3E0', address:va('0x005AE3E0'),
    returnVa:'0x005B13FF', returnAddress:va('0x005B13FF')},
  {name:'special_policy', va:'0x005AE220', address:va('0x005AE220'),
    returnVa:'0x005B1418', returnAddress:va('0x005B1418')}
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
let traceDropped = 0;
let traceDrained = 0;
let traceCompletedDuels = 0;
let traceSession = 0;
let traceExportedSession = 0;
let traceExportedSeen = -1;
let summonPrepHooks = [];
let summonPrepCallSequence = 0;
let nativeAiSelectNormal = null;
let nativeAiForceSet = null;
let aiProbeHooks = [];
let aiDetailMode = false;
let aiGateMode = false;
let combatContextMode = false;
let combatContextCount = 0;
let combatContextLimitReported = false;
let combatContextGeneration = 0;
let combatContextSchemaVersion = 1;
let combatReadPointsArmed = [];
let combatContextStacks = new Map();
const kCombatContextLimit = 64;
// Function entries only. Return VAs exclude calls nested inside stat producers.
const kCombatContextTargets = [
  ['own_stats', '0x005ADD60', 3, ['0x005B0F97'], 12],
  ['opponent_stats', '0x005ADDC0', 3, ['0x005B0FAE'], 12],
  ['equipment_count', '0x0056D790', 3, ['0x005B1060']],
  ['context_count', '0x0056D8D0', 4, ['0x005B1117','0x005B112F','0x005B1153','0x005B116F']],
  ['field_query', '0x005699A0', 2, ['0x005B11F4','0x005B1227']],
  ['phase_query', '0x0056B220', 1, ['0x005B1205','0x005B1238']],
  ['resource', '0x004022E0', 1, ['0x005B0FB8','0x005B1009','0x005B10FD','0x005B1251']]
].map(([name, address, argCount, returns, outputSize]) => ({
  name, va:address, address:va(address), argCount, outputSize,
  returns:returns.map(address => va(address))
}));
// Interior hooks are NOT armed by default after the Windows access violation
// at 005B1195. Each can be explicitly selected for supervised diagnostics.
// A combined mode exists only for offline synthetic tests; do not use it in Joey.
// Minimum complete instruction spans (>=5 bytes). Interior listeners MUST use
// the bare function probe overload: a JS {onEnter} call listener can still trap
// returns internally and replace [ESP], which here may hold a saved register.
const kCombatReadPoints = [
  ['own_visibility_consumed', '0x005ADD8D', 'A802751B0C02'],
  ['opponent_visible_taken', '0x005ADE2C', '8B4C240C51'],
  ['opponent_hidden_taken', '0x005ADDF0', '668B91745DA500'],
  ['target_position_consumed', '0x005B0F7B', '33C089442440'],
  ['descriptor_seed_consumed', '0x005B108A', '0BD9C1E304'],
  ['defender_flags_consumed', '0x005B119B', 'BA00004000'],
  ['attacker_flags_sample', '0x005B11C6', '85148D3064A500'],
  ['attacker_protection_taken', '0x005B11CF', '66816500FFFB'],
  ['attacker_flags_consumed', '0x005B11DD', '8B44243485C0']
].map(([name,address,bytes]) => ({name, va:address, address:va(address), bytes}));
let aiProbeCallSequence = 0;
let aiSelectorDepth = new Map();
let aiSelectorStack = new Map();
let demoExecutorHook = null;
let demoExecutorCalls = 0;
let demoSetupHooks = [];
let demoSetupCalls = 0;
let demoSeedBase = null;
let demoSeedStartIndex = 0;
let demoSeedApplied = 0;
// Passive summon observations for offline differential fixtures.
let demoSummonTickHook = null;
let demoSummonCandidates = new Map();
const kDemoSummonMaxTicks = 1500;

// Bijective 32-bit avalanche mixer. Identical base+global index reproduces
// the same seed; distinct indices cannot produce duplicate seeds modulo 2^32.
function mixDuelSeed(base, index) {
  let value = (base + index) >>> 0;
  value ^= value >>> 16;
  value = Math.imul(value, 0x7feb352d) >>> 0;
  value ^= value >>> 15;
  value = Math.imul(value, 0x846ca68b) >>> 0;
  value ^= value >>> 16;
  return value >>> 0;
}

function setDemoRngSeedPlan(base, startIndex = 0) {
  if (!installed || !observerOnly || commandTraceHook === null ||
      demoExecutorHook === null) {
    throw new Error('Seed control requires observer-only demo trace hooks');
  }
  if (!Number.isSafeInteger(base) || base < 0 || base > 0xFFFFFFFF ||
      !Number.isSafeInteger(startIndex) || startIndex < 0 ||
      startIndex > 0xFFFFFFFF) {
    throw new Error('Seed and global duel offset must be unsigned 32-bit integers');
  }
  demoSeedBase = base >>> 0;
  demoSeedStartIndex = startIndex;
  demoSeedApplied = 0;
  return {enabled: true, baseSeed: demoSeedBase,
    startIndex: demoSeedStartIndex, nextGlobalIndex: demoSeedStartIndex};
}
// Optional, source-level deck substitution. Plans address duel SIDES,
// not source slots; mode 7 may swap source mapping via deckSideBit.
let demoDeckPlans = [null, null];
let demoDeckApplied = [0, 0];
let demoDeckVerified = [0, 0];
let demoDeckFailed = [0, 0];

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
  if (combatContextMode) throw new Error('Stop combat-context before enabling overrides');
  if (deckObserver !== null && deckObserver.status().deckActive)
    throw new Error('Stop trace deck before enabling overrides');
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
  if (combatContextMode) throw new Error('Stop combat-context before enabling overrides');
  if (deckObserver !== null && deckObserver.status().deckActive)
    throw new Error('Stop trace deck before enabling overrides');
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
let deckObserver = null;
let deckFingerprint = null;
let lastTraceLifePoints = null;

// Every observation is taken on the original producer's onEnter, before
// that command is queued, NOT after the command's effects have resolved.
// Compare successive producer entries to detect native LP changes without
// adding another hook or predicting damage from command arguments.
function sampleTraceLifePoints(event, opcode) {
  const forced = opcode === 0x0043 || opcode === 0x0004;
  try {
    const duelRunningWord = kDuelRunning.readU16();
    if (duelRunningWord === 0 && !forced) {
      lastTraceLifePoints = null;
      return;
    }
    const lifePoints = [
      va('0x00A55D64').readU16(),
      va('0x00A55D64').add(kPlayerStride).readU16()
    ];
    const first = lastTraceLifePoints === null;
    const changed = !first && lifePoints.some((lp, side) =>
      lp !== lastTraceLifePoints[side]);
    if (first || changed || forced) {
      event.lifePointsObserved = {
        lifePoints, duelRunningWord, samplePoint: 'command_producer_onEnter',
        reason: opcode === 0x0004 ? 'terminal' :
          opcode === 0x0043 ? 'lp_command' :
          first ? 'first_active_sample' : 'changed'
      };
    }
    // Cache every valid sampling point, not just the ones emitted.
    lastTraceLifePoints = duelRunningWord === 0 ? null : lifePoints;
    if (opcode === 0x0004) lastTraceLifePoints = null;
  } catch (error) {
    lastTraceLifePoints = null;
    if (forced) event.lifePointsReadError = String(error);
  }
}
function startDeckProbe(captureMt = false) {
  if (!installed || !observerOnly || commandTraceHook === null)
    throw new Error('Deck probe requires --observe-only and trace on');
  if (demoExecutorHook !== null || demoSetupHooks.length || demoSeedBase !== null ||
      demoDeckPlans.some(x => x !== null) || speedyOverrideValue !== null || singletonMutexHook !== null)
    throw new Error('Deck probe cannot run with Demo hooks or state overrides');
  if (typeof createCpuDeckObserver !== 'function') throw new Error('Missing deck observer module');
  if (deckObserver === null) deckObserver = createCpuDeckObserver({
    va, main, record:recordTraceEvent, session:() => traceSession});
  deckObserver.start(captureMt, deckFingerprint);
  return traceStatus();
}
function stopDeckProbe() {
  if (deckObserver !== null) deckObserver.stop();
  return traceStatus();
}

function traceStatus() {
  return {
    active: commandTraceHook !== null,
    // Host attestation exists independently of whether deck tracing was armed.
    hostAttestedExeSha256: deckFingerprint,
    processId: Process.id, modulePath: main.path,
    ...(deckObserver === null ? {deckActive:false} : deckObserver.status()),
    prepActive: summonPrepHooks.length > 0,
    prepHooks: summonPrepHooks.length,
    aiActive: aiProbeHooks.length > 0,
    aiDetailActive: aiDetailMode,
    aiGateActive: aiGateMode,
    combatContextActive: combatContextMode,
    combatContextCount,
    combatContextLimit: kCombatContextLimit,
    combatContextSchemaVersion,
    combatReadPointsArmed: combatReadPointsArmed.slice(),
    aiHooks: aiProbeHooks.length,
    demoActive: demoExecutorHook !== null,
    demoHooks: (demoExecutorHook === null ? 0 : 1) + demoSetupHooks.length,
    session: traceSession,
    producer: kCommandProducer.toString(),
    capacity: traceCapacity,
    seen: traceSeen,
    retained: traceBuffer.length,
    overwritten: traceDropped,
    drained: traceDrained,
    completedDuels: traceCompletedDuels,
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
    traceDropped++;
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
  if (combatContextMode) throw new Error('Stop combat-context before trace prep on');
  if (!installed || commandTraceHook === null) {
    throw new Error('Start trace on before enabling the summon prep probe');
  }
  if (summonPrepHooks.length > 0) throw new Error('Summon prep probe is already active');
  // AAB90 is also observed by the gate pilot; never stack two listeners
  // from independent modes at that native function.
  if (aiGateMode) throw new Error('Stop AI gate trace before trace prep on');
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

// Read the ORIGINAL hand at selector entry on the intercepted game thread.
// This captures options present in hand, not all legal or evaluated actions.
// Sample only while a duel is running; menu buffers can contain stale data.
// All fields are raw observations; the snapshot is not an atomic game state.
function readSelectorHandSnapshot(side) {
  const running = kDuelRunning.readU16() !== 0;
  if (!running) return {duelRunning: false};
  if (!Number.isInteger(side) || side < 0 || side > 1) {
    throw new Error('AI selector side is not 0 or 1');
  }
  const count = handCount(side);
  if (count > 80) {
    throw new Error('AI selector hand count out of range: ' + count);
  }
  const words = [];
  for (let i = 0; i < count; i++) words.push(handWord(side, i));
  return {
    duelRunning: true, count, words,
    indices: words.map(word => word & 0xFFF)
  };
}

// This is a *passive* probe: never invokes FUN_005B0F40 or FUN_0056DD30.
// Only events nested inside an observed selector are kept to avoid renderer noise.
function combatContextIdentity(frame) {
  return {kind:'combat_context', schemaVersion:combatContextSchemaVersion, session:frame.session,
    threadId:frame.threadId, combatCallId:frame.callId,
    selectorCallId:frame.selectorCallId, stateKind:'selector-window'};
}

function enterCombatContext(invocation, args) {
  const threadId = invocation.aiThread;
  const stack = combatContextStacks.get(threadId) || [];
  const frame = {threadId, callId:invocation.aiCallId, children:0,
    session:traceSession, generation:combatContextGeneration, argsRaw:invocation.aiArguments};
  stack.push(frame);
  combatContextStacks.set(threadId, stack);
  // Push a sentinel even after the limit, so nested calls cannot be attributed
  // to an outer observed combat. No game memory is read after the limit.
  if (combatContextCount >= kCombatContextLimit) {
    frame.suppressed = true;
    if (!combatContextLimitReported) {
      combatContextLimitReported = true;
      recordTraceEvent({kind:'combat_context_limit', schemaVersion:combatContextSchemaVersion,
        session:traceSession, limit:kCombatContextLimit});
    }
    return frame;
  }
  combatContextCount++;
  const selectors = aiSelectorStack.get(threadId) || [];
  frame.selectorCallId = selectors.length ? selectors[selectors.length - 1].id : null;
  const event = {...combatContextIdentity(frame), phase:'enter',
    function:'combat_event', functionVa:'0x005B0F40', callId:frame.callId,
    caller:invocation.returnAddress.toString(), argsRaw:invocation.aiArguments,
    seedHex:null, attacker:null, defender:null, readErrors:[]};
  if (combatContextSchemaVersion === 3) {
    // V3 samples are not witnesses of consumption by interior instructions.
    event.provenance = 'F40-entry-snapshot';
    event.consumedPositionRaw = null;
    event.consumedDescriptorSeedRaw = null;
    event.consumedFlagsRaw = null;
  }
  function read(name, action) {
    try { return action(); }
    catch (error) { event.readErrors.push({field:name, error:String(error)}); return null; }
  }
  const [side, attackerSlot, targetSlot] = invocation.aiArguments;
  event.seedHex = read('seedHex', () => readAiProbeHex(args[3], 4));
  if ((side !== 0 && side !== 1) || attackerSlot > 4 || targetSlot > 4) {
    event.readErrors.push({field:'argsRaw', error:'Unsupported side or monster slot'});
  } else {
    for (const [role, fieldSide, slot] of [
      ['attacker',side,attackerSlot], ['defender',1-side,targetSlot]]) {
      const record = kMonsterSlotBase.add(fieldSide*kPlayerStride + slot*kFieldSlotStride);
      const identityRaw = read(role+'.identityRaw', () => record.readU32());
      const positionRaw = read(role+'.positionRaw', () => record.add(6).readU16());
      event[role] = {side:fieldSide, slot, identityRaw,
        internalCardIndex:identityRaw === null ? null : identityRaw & 0xFFF,
        resourceId:null, positionRaw,
        defensePosition:positionRaw === null ? null : (positionRaw & 1) !== 0,
        faceUp:positionRaw === null ? null : (positionRaw & 2) !== 0,
        monsterFlagsRaw:read(role+'.monsterFlagsRaw', () => record.add(0x8C).readU32()),
        playerFlagsRaw:read(role+'.playerFlagsRaw', () => kSummonPlayerFlags.add(fieldSide*kPlayerStride).readU32()),
        linkCountRaw:read(role+'.linkCountRaw', () => record.add(0xA).readU16()),
        lifePoints:read(role+'.lifePoints', () => va('0x00A55D64').add(fieldSide*kPlayerStride).readU16())};
    }
  }
  recordTraceEvent(event);
  return frame;
}

function leaveCombatContext(frame, output) {
  if (!combatContextMode || frame.generation !== combatContextGeneration) return;
  const stack = combatContextStacks.get(frame.threadId) || [];
  const top = stack.pop();
  if (!stack.length) combatContextStacks.delete(frame.threadId);
  if (frame.suppressed) return;
  const event = {...combatContextIdentity(frame), phase:'leave',
    function:'combat_event', functionVa:'0x005B0F40', callId:frame.callId,
    outputHex:null, childCount:frame.children, childOverflow:!!frame.childOverflow};
  if (top !== frame) event.correlationError = 'Combat stack mismatch';
  try { event.outputHex = readAiProbeHex(output, 4); }
  catch (error) { event.readError = String(error); }
  recordTraceEvent(event);
}

function stopAiProbe() {
  const hooks = aiProbeHooks;
  aiProbeHooks = [];
  aiDetailMode = false;
  aiGateMode = false;
  combatContextMode = false;
  combatContextSchemaVersion = 1;
  combatReadPointsArmed = [];
  combatContextGeneration++;
  combatContextStacks = new Map();
  aiSelectorDepth = new Map();
  aiSelectorStack = new Map();
  let failure = null;
  for (let i = hooks.length - 1; i >= 0; i--) {
    try { hooks[i].detach(); } catch (error) { failure = error; }
  }
  if (failure !== null) throw new Error('AI probe detach failed: ' + failure);
  return traceStatus();
}

function startAiProbe(mode = false) {
  // Fail closed after Windows AV at 0x005B1195: the regular combat-context
  // mode enables function-entry hooks only. Request one interior point by its
  // exact name for supervised diagnosis. Never use the all-points mode in Joey.
  const pointPrefix = 'combat-context-point:';
  const pointName = typeof mode === 'string' && mode.startsWith(pointPrefix)
    ? mode.slice(pointPrefix.length) : null;
  const allUnsafe = mode === 'combat-context-all-unsafe';
  // The nine-point combination coincided with a live Windows register-corruption
  // crash; block it even if a caller bypasses the Python console and calls RPC.
  if (allUnsafe && Process.platform === 'windows')
    throw new Error('Combined interior combat-context hooks disabled on Windows after access violation');
  const boundariesV3 = mode === 'combat-context-v3';
  const withCombat = boundariesV3 || mode === 'combat-context' || allUnsafe || pointName !== null;
  if (pointName !== null && !kCombatReadPoints.some(point => point.name === pointName))
    throw new Error('Unknown combat-context observation point: ' + pointName);
  if (mode !== false && mode !== true && mode !== 'gates' && !withCombat)
    throw new Error('AI trace mode must be false, true, gates, combat-context, or combat-context-v3');
  const readPoints = allUnsafe ? kCombatReadPoints :
    pointName !== null ? kCombatReadPoints.filter(point => point.name === pointName) : [];
  const detailed = mode === true || mode === 'gates';
  const withGates = mode === 'gates';
  if (!installed || commandTraceHook === null) {
    throw new Error('Start trace on before trace ai on');
  }
  if (aiProbeHooks.length > 0) throw new Error('AI probe is already active');
  if (withGates && (!observerOnly || summonPrepHooks.length > 0))
    throw new Error('AI gate trace requires --observe-only and trace prep off');
  if (withCombat && (!observerOnly || summonPrepHooks.length > 0 ||
      demoExecutorHook !== null || demoSetupHooks.length || demoSeedBase !== null ||
      demoDeckPlans.some(x => x !== null) || speedyOverrideValue !== null || singletonMutexHook !== null))
    throw new Error('combat-context requires --observe-only, trace prep/demo off and no overrides');
  // v3 is strictly FIVE function-boundary listeners: two selectors, F40,
  // ADD60 and ADDC0. No instruction interception or DD30/helper hooks.
  // Preserve legacy v1/v2 target selection exactly.
  const aiTargets = kAiProbeTargets.filter(t => !withCombat || t.name !== 'computed_stats');
  const combatTargets = boundariesV3 ?
    kCombatContextTargets.filter(t => t.name === 'own_stats' || t.name === 'opponent_stats') :
    kCombatContextTargets;
  const targets = aiTargets.concat(
    detailed ? kAiDetailTargets : [], withGates ? kAiGateTargets : [],
    withCombat ? combatTargets.concat(readPoints) : []);
  for (const target of targets) {
    const range = Process.findRangeByAddress(target.address);
    if (range === null || !range.protection.includes('x')) {
      throw new Error('AI probe function not executable: ' + target.va);
    }
  }
  if (withCombat) {
    for (const point of readPoints) {
      if (readAiProbeHex(point.address, point.bytes.length / 2) !== point.bytes)
        throw new Error('Combat instruction signature mismatch: ' + point.va);
    }
  }
  const hooks = [];
  try {
    for (const target of aiTargets) {
      const hook = Interceptor.attach(target.address, {
        onEnter(args) {
          this.aiThread = Process.getCurrentThreadId();
          this.aiCallId = ++aiProbeCallSequence;
          this.aiSideRaw = args[0].toUInt32();
          this.aiSide = this.aiSideRaw & 1;
          this.aiIsSelector = target.kind === 'selector';
          if (this.aiIsSelector) {
            aiSelectorDepth.set(this.aiThread,
              (aiSelectorDepth.get(this.aiThread) || 0) + 1);
            const stack = aiSelectorStack.get(this.aiThread) || [];
            stack.push({id:this.aiCallId, name:target.name, side:this.aiSide});
            aiSelectorStack.set(this.aiThread, stack);
            const event = {
              kind: 'ai_probe', phase: 'enter', function: target.name,
              functionVa: target.va, callId: this.aiCallId,
              threadId: this.aiThread, side: this.aiSide,
              sideRaw: this.aiSideRaw,
              caller: this.returnAddress.toString(),
              pass: target.name === 'select_normal' ? args[1].toInt32() : null
            };
            this.aiHandSnapshot = null;
            try {
              const snapshot = readSelectorHandSnapshot(this.aiSideRaw);
              event.duelRunning = snapshot.duelRunning;
              if (snapshot.duelRunning) {
                this.aiHandSnapshot = snapshot;
                stack[stack.length - 1].handIndices = snapshot.indices;
                event.handCount = snapshot.count;
                event.handWords = snapshot.words;
                event.handIndices = snapshot.indices;
              }
            } catch (error) {
              event.handReadError = String(error);
            }
            recordTraceEvent(event);
          } else {
            this.aiInsideSelection = (aiSelectorDepth.get(this.aiThread) || 0) > 0;
            if (this.aiInsideSelection) {
              this.aiBuffer = args[target.outputIndex];
              this.aiArguments = target.name === 'combat_event' ?
                [args[0].toUInt32(), args[1].toUInt32(), args[2].toUInt32()] :
                [args[0].toUInt32(), args[1].toUInt32()];
              if (withCombat && target.name === 'combat_event')
                this.combatFrame = enterCombatContext(this, args);
            }
          }
        },
        onLeave(retval) {
          if (this.aiIsSelector) {
            const previous = aiSelectorDepth.get(this.aiThread) || 0;
            if (previous <= 1) aiSelectorDepth.delete(this.aiThread);
            else aiSelectorDepth.set(this.aiThread, previous - 1);
            const stack = aiSelectorStack.get(this.aiThread) || [];
            stack.pop();
            if (stack.length === 0) aiSelectorStack.delete(this.aiThread);
            const selectedHandIndex = retval.toInt32();
            const event = {
              kind: 'ai_probe', phase: 'leave', function: target.name,
              functionVa: target.va, callId: this.aiCallId,
              threadId: this.aiThread, side: this.aiSide,
              returnRaw: retval.toUInt32(), selectedHandIndex
            };
            // Resolve against the ENTRY snapshot, not potentially mutated
            // hand memory at return. A negative index means no match here.
            if (this.aiHandSnapshot !== null) {
              event.handCountAtEntry = this.aiHandSnapshot.count;
              if (selectedHandIndex >= 0 &&
                  selectedHandIndex < this.aiHandSnapshot.count) {
                event.selectedCardWordAtEntry =
                  this.aiHandSnapshot.words[selectedHandIndex];
                event.selectedCardIndexAtEntry =
                  this.aiHandSnapshot.indices[selectedHandIndex];
              }
            }
            recordTraceEvent(event);
            return;
          }
          if (!this.aiInsideSelection) return;
          if (this.combatFrame) {
            leaveCombatContext(this.combatFrame, this.aiBuffer);
            return;
          }
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
    if (withCombat) {
      for (const point of readPoints) {
        hooks.push(Interceptor.attach(point.address, function () {
          const stack = combatContextStacks.get(Process.getCurrentThreadId()) || [];
          const frame = stack.length ? stack[stack.length - 1] : null;
          if (!frame || frame.suppressed) return;
          if ((point.name.startsWith('own_visibility') || point.name.startsWith('opponent_')) && !frame.statsArgs) return;
          if (++frame.children > 24) { frame.childOverflow = true; return; }
          const event = {...combatContextIdentity(frame), phase:'leave',
            function:point.name, functionVa:point.va, callId:++aiProbeCallSequence,
            valueRaw:null};
          try {
            const c = this.context;
            if (point.name === 'own_visibility_consumed') event.valueRaw = c.eax.toUInt32() & 0xFFFF;
            if (point.name.startsWith('opponent_')) event.faceUpConsumed = point.name === 'opponent_visible_taken';
            if (frame.statsArgs) event.wrapperArgsRaw = frame.statsArgs;
            if (point.name === 'target_position_consumed') event.valueRaw = c.ecx.toUInt32() & 0xFF;
            if (point.name === 'descriptor_seed_consumed') {
              event.valueRaw = c.edx.toUInt32() & 0xFFFF;
              event.preservedMaskRaw = event.valueRaw & 0xF005;
            }
            if (point.name === 'defender_flags_consumed') {
              event.valueRaw = c.eax.toUInt32();
              event.playerOffsetRaw = c.esi.toUInt32();
              if (frame.argsRaw[0] > 1 || event.playerOffsetRaw !== (1-frame.argsRaw[0])*kPlayerStride)
                throw new Error('Unexpected defender flag offset');
              event.protectionConsumed = (event.valueRaw & 0x400000) !== 0;
            }
            if (point.name === 'attacker_flags_sample') {
              frame.attackerProtectionTaken = false;
              frame.attackerFlagsSampled = true;
              event.playerOffsetRaw = c.ecx.toUInt32() * 4;
              if (frame.argsRaw[0] > 1 || event.playerOffsetRaw !== frame.argsRaw[0]*kPlayerStride)
                throw new Error('Unexpected player flag offset');
              event.valueRaw = va('0x00A56430').add(event.playerOffsetRaw).readU32();
              // This memory sample precedes TEST. The branch witnesses below
              // establish the consumed bit, even if another thread writes it.
            }
            if (point.name === 'attacker_protection_taken') frame.attackerProtectionTaken = true;
            if (point.name === 'attacker_flags_consumed') {
              event.protectionConsumed = frame.attackerFlagsSampled ? frame.attackerProtectionTaken : null;
              if (!frame.attackerFlagsSampled) event.correlationError = 'Missing attacker TEST sample';
            }
          } catch (error) { event.readError = String(error); }
          recordTraceEvent(event);
        }));
      }
      for (const target of combatTargets) {
        hooks.push(Interceptor.attach(target.address, {
          onEnter(args) {
            const threadId = Process.getCurrentThreadId();
            const stack = combatContextStacks.get(threadId) || [];
            const frame = stack.length ? stack[stack.length - 1] : null;
            if (frame === null || frame.suppressed || !target.returns.some(p => p.equals(this.returnAddress))) return;
            if (++frame.children > 24) {
              frame.childOverflow = true;
              return;
            }
            this.combatChild = {
              ...combatContextIdentity(frame), phase:'leave', function:target.name,
              functionVa:target.va, callId:++aiProbeCallSequence,
              caller:this.returnAddress.toString(),
              argsRaw:Array.from({length:target.argCount}, (_, i) => args[i].toUInt32())
            };
            this.combatGeneration = combatContextGeneration;
            if (boundariesV3 && target.outputSize) {
              // A wrapper is entered by a direct CALL from F40, with the
              // rival-side ESI offset alive. F40's own entry/leave ESI is
              // caller-owned and must NOT be compared to this offset.
              const side = frame.argsRaw[0];
              const expected = (side === 0 || side === 1) ?
                (1 - side) * kPlayerStride : null;
              this.combatChild.esiExpectedRaw = expected;
              this.combatChild.esiAtEnterRaw = null;
              this.combatChild.espAtEnterRaw = null;
              this.combatChild.esiAtLeaveRaw = null;
              this.combatChild.espAtLeaveRaw = null;
              this.combatChild.esiPreserved = null;
              this.combatChild.provenance = 'wrapper-boundary-snapshot';
              try {
                this.combatChild.esiAtEnterRaw = this.context.esi.toUInt32();
                this.combatChild.espAtEnterRaw = this.context.esp.toUInt32();
                if (expected === null || this.combatChild.esiAtEnterRaw !== expected)
                  this.combatChild.correlationError = 'Unexpected ESI at wrapper entry';
              } catch (error) {
                this.combatChild.readError = 'Wrapper entry register read: ' + error;
              }
            }
            if (target.outputSize) {
              this.combatOutput = args[2];
              frame.statsArgs = this.combatChild.argsRaw;
              this.combatParentFrame = frame;
              const [side,slot] = this.combatChild.argsRaw;
              this.combatPosition = (side <= 1 && slot <= 4) ?
                kMonsterSlotBase.add(side*kPlayerStride + slot*kFieldSlotStride + 6) : null;
              this.combatChild.positionBeforeRaw = null;
              try {
                if (this.combatPosition === null) throw new Error('Unsupported wrapper slot');
                this.combatChild.positionBeforeRaw = this.combatPosition.readU16();
              } catch (error) { this.combatChild.readError = String(error); }
            }
          },
          onLeave(retval) {
            if (!this.combatChild || !combatContextMode ||
                this.combatGeneration !== combatContextGeneration) return;
            const event = this.combatChild;
            if (target.outputSize) {
              event.outputHex = null;
              this.combatParentFrame.statsArgs = null;
              try {
                event.outputHex = readAiProbeHex(this.combatOutput, target.outputSize);
                event.positionAfterRaw = this.combatPosition.readU16();
              }
              catch (error) { event.readError = String(error); }
              if (boundariesV3) {
                try {
                  event.esiAtLeaveRaw = this.context.esi.toUInt32();
                  event.espAtLeaveRaw = this.context.esp.toUInt32();
                  if (event.esiAtEnterRaw !== null) {
                    event.esiPreserved = event.esiAtEnterRaw === event.esiAtLeaveRaw;
                  }
                  if (event.esiExpectedRaw === null ||
                      event.esiAtLeaveRaw !== event.esiExpectedRaw ||
                      event.esiPreserved !== true) {
                    event.correlationError = event.correlationError ?
                      event.correlationError + '; Unexpected ESI at wrapper leave' :
                      'Unexpected ESI at wrapper leave';
                  }
                } catch (error) {
                  event.readError = event.readError ?
                    event.readError + '; Wrapper leave register read: ' + error :
                    'Wrapper leave register read: ' + error;
                }
              }
            } else {
              event.returnRaw = retval.toUInt32();
              event.returnSigned = retval.toInt32();
              if (target.name === 'resource') event.resourceId = event.returnRaw & 0xFFFF;
            }
            recordTraceEvent(event);
          }
        }));
      }
    }
    if (detailed) {
      for (const target of kAiDetailTargets) {
        const hook = Interceptor.attach(target.address, {
          onEnter(args) {
            this.detailThread = Process.getCurrentThreadId();
            this.detailCaller = this.returnAddress.toString();
            this.detailSideRaw = args[0].toUInt32();
            this.detailSide = this.detailSideRaw & 1;
            const stack = aiSelectorStack.get(this.detailThread) || [];
            const active = stack.length > 0 ? stack[stack.length - 1] : null;
            // Only CPU decisions. Capture the baseline invoked by the
            // caller before the selector, plus helpers INSIDE a normal selector.
            const insideNormal = active !== null &&
              active.name === 'select_normal' && active.side === 0;
            const outerBaseline = target.name === 'baseline' &&
              this.returnAddress.equals(kAiOuterBaselineCaller);
            this.detailObserve = this.detailSideRaw === 0 &&
              (insideNormal || outerBaseline) && kDuelRunning.readU16() !== 0;
            if (!this.detailObserve) return;
            this.detailSelectorId = insideNormal ? active.id : null;
            this.detailInput = {};
            try {
              if (target.name === 'simulate_card') {
                this.detailInput = {
                  internalCardIndex:args[1].toUInt32() & 0xFFF,
                  tributeCount:args[2].toUInt32()
                  // Do NOT dereference args[3] until tribute ABI is verified.
                };
              }
              if (target.name === 'restore_field') {
                this.detailInput.actionMaskBeforeRestore =
                  kAiActionMaskBySide.readU16();
              }
            } catch (error) {
              this.detailInput.readError = String(error);
            }
          },
          onLeave(retval) {
            if (!this.detailObserve) return;
            const event = {
              kind:'ai_detail', phase:'leave', function:target.name,
              functionVa:target.va, threadId:this.detailThread,
              side:this.detailSide, caller:this.detailCaller,
              selectorCallId:this.detailSelectorId,
              returnRaw:retval.toUInt32(), returnSigned:retval.toInt32(),
              ...this.detailInput
            };
            try {
              if (target.name === 'baseline' ||
                  target.name === 'aggregate_score' ||
                  target.name === 'restore_field') {
                event.summaryRaw = kAiSummary.readU16();
                event.actionMaskOutput = kAiActionMaskOutput.readU16();
              }
              if (target.name === 'aggregate_score' ||
                  target.name === 'restore_field') {
                event.actionMaskCurrent = kAiActionMaskBySide.readU16();
              }
              if (target.name === 'select_combat_event' && retval.toInt32() !== 0) {
                event.selectedEventHex = readAiProbeHex(kAiChosenEvent, 4);
              }
              // Every entry, including B0B90 returning zero, is logged:
              // that final failure may clear scratch and end the score loop.
            } catch (error) { event.readError = String(error); }
            recordTraceEvent(event);
          }
        });
        hooks.push(hook);
      }
    }
    if (withGates) {
      for (const target of kAiGateTargets) {
        const hook = Interceptor.attach(target.address, {
          onEnter(args) {
            this.gateObserve = false;
            const threadId = Process.getCurrentThreadId();
            // Do not even examine arguments for unrelated callers.
            if (!this.returnAddress.equals(target.returnAddress)) return;
            const stack = aiSelectorStack.get(threadId) || [];
            const active = stack.length ? stack[stack.length - 1] : null;
            if (active === null || active.name !== 'select_normal' ||
                active.side !== 0) return;
            try {
              if (kDuelRunning.readU16() === 0) return;
              const rawArgs = target.name === 'special_policy'
                ? [args[0].toUInt32()]
                : target.name === 'resource_policy'
                  ? [args[0].toUInt32(),args[1].toUInt32(),args[2].toUInt32()]
                  : [args[0].toUInt32(),args[1].toUInt32()];
              if (target.name !== 'special_policy' && rawArgs[0] !== 0)
                return;
              const cardIndex = target.name === 'eligibility_mask' ?
                rawArgs[1] & 0xFFF :
                target.name === 'special_policy' ? rawArgs[0] & 0xFFF :
                active.lastGateCardIndex ?? null;
              if (target.name === 'eligibility_mask')
                active.lastGateCardIndex = cardIndex;
              this.gateDetails = {
                kind:'ai_gate', phase:'leave', function:target.name,
                functionVa:target.va, callsiteReturn:target.returnVa,
                threadId, selectorCallId:active.id, side:0,
                caller:this.returnAddress.toString(), argsRaw:rawArgs,
                candidateCardIndex:cardIndex,
                handPositions:cardIndex === null ? [] :
                  (active.handIndices || []).flatMap((v,i) =>
                    v === cardIndex ? [i] : [])
              };
              this.gateObserve = true;
            } catch (error) {
              // Invalid/unknown ABI never affects native execution.
              this.gateDetails = {
                kind:'ai_gate', phase:'leave', function:target.name,
                functionVa:target.va, callsiteReturn:target.returnVa,
                threadId, selectorCallId:active.id, side:0,
                readError:String(error)
              };
              this.gateObserve = true;
            }
          },
          onLeave(retval) {
            if (!this.gateObserve) return;
            const result = {...this.gateDetails,
              returnRaw:retval.toUInt32(), returnSigned:retval.toInt32()};
            if (result.readError === undefined) {
              const passed = target.name === 'eligibility_mask' ?
                result.returnRaw !== 0 : result.returnRaw === 0;
              result.gatePassed = passed;
              result.gateVerdict = passed ? 'continue' : 'reject';
            }
            recordTraceEvent(result);
          }
        });
        hooks.push(hook);
      }
    }
  } catch (error) {
    for (let i = hooks.length - 1; i >= 0; i--) {
      try { hooks[i].detach(); } catch (_) { /* best effort rollback */ }
    }
    throw new Error('AI probe installation failed; rolled back: ' + error);
  }
  aiProbeHooks = hooks;
  aiDetailMode = detailed;
  aiGateMode = withGates;
  combatContextMode = withCombat;
  combatContextSchemaVersion = boundariesV3 ? 3 : readPoints.length ? 2 : 1;
  combatReadPointsArmed = readPoints.map(point => point.name);
  combatContextGeneration++;
  combatContextCount = 0;
  combatContextLimitReported = false;
  combatContextStacks = new Map();
  aiSelectorDepth = new Map();
  aiSelectorStack = new Map();
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

// Observations only. Hand counts / field slots are bounds-checked; this is
// not a full native save-state and never reads the game's hidden card effects.
function readDemoSummonSnapshot(side) {
  if (side !== 0 && side !== 1) throw new Error('Invalid summon snapshot side');
  if (kDuelRunning.readU16() === 0) throw new Error('Duel is not running');
  const count = handCount(side);
  if (count > 80) throw new Error('Observed hand exceeds 80 cards');
  const handWords = [];
  for (let i = 0; i < count; i++) handWords.push(handWord(side, i));
  const monsters = player => Array.from({length: 5},
    (_, slot) => monsterWord(player, slot) & 0xFFF);
  return {
    side, handIndices: handWords.map(word => word & 0xFFF),
    handWords, monsters: monsters(side), opponentMonsters: monsters(1 - side),
    lifePoints: va('0x00A55D64').add(side * kPlayerStride).readU16(),
    playerFlags: kSummonPlayerFlags.add(side * kPlayerStride).readU32(),
    duelPhaseRaw: kSummonDuelPhase.readU8()
  };
}
function sameIndexArray(a, b) {
  return Array.isArray(a) && Array.isArray(b) &&
    a.length === b.length && a.every((v, i) => v === b[i]);
}
// Only a fully observed, unambiguous transition is exported. The after state
// is sampled on the original duel-update thread, never on a Frida RPC thread.
function sampleDemoSummonTransitions() {
  if (demoSummonCandidates.size === 0) return;
  for (const [side, candidate] of demoSummonCandidates) {
    // The normal-placement emitter and the state reader must run on the
    // same native duel thread, not an unrelated UI/worker thread.
    if (Process.getCurrentThreadId() !== candidate.threadId) continue;
    candidate.ticks++;
    if (candidate.ticks > kDemoSummonMaxTicks) {
      demoSummonCandidates.delete(side);
      continue;
    }
    if (candidate.ce === null) continue;
    try {
      const after = readDemoSummonSnapshot(side);
      const before = candidate.before;
      const expectedHand = before.handIndices.slice();
      expectedHand.splice(candidate.handIndex, 1);
      const expectedField = before.monsters.slice();
      expectedField[candidate.ce.fieldSlot] = candidate.source;
      const afterMatches = sameIndexArray(after.handIndices, expectedHand) &&
        sameIndexArray(after.monsters, expectedField) &&
        sameIndexArray(after.opponentMonsters, before.opponentMonsters) &&
        after.lifePoints === before.lifePoints &&
        (before.playerFlags & 0x20000) === 0 &&
        (after.playerFlags & 0x20000) !== 0;
      if (afterMatches) {
        recordTraceEvent({
          kind: 'summon_transition', phase: 'confirmed',
          evidenceMethod: 'executor_enter_ce_producer_update_leave',
          executorCallId: candidate.callId, executorEnterSeq: candidate.enterSeq,
          threadId: candidate.threadId,
          side, handIndex: candidate.handIndex,
          sourceCardIndex: candidate.source, fieldSlot: candidate.ce.fieldSlot,
          ceContext: candidate.ce.context, ceStage: candidate.ce.stage,
          ceResourceId: candidate.ce.resourceId, ceProducerSeq: candidate.ce.producerSeq,
          before, after, ticksAfterExecutor: candidate.ticks,
          // Only a projection of game state is observed here. No full
          // turn/stack/animation/cemetery or card-rules equivalence is implied.
          observationLimit: 'game-thread snapshots at separate instruction boundaries'
        });
        demoSummonCandidates.delete(side);
      } else if (!sameIndexArray(after.opponentMonsters, before.opponentMonsters) ||
                 after.lifePoints !== before.lifePoints ||
                 after.handIndices.length < before.handIndices.length - 1) {
        // Another action/effect was observed; never pretend single-action causality.
        demoSummonCandidates.delete(side);
      }
    } catch (_error) {
      // Read errors invalidate rather than create optimistic partial evidence.
      demoSummonCandidates.delete(side);
    }
  }
}
function rememberDemoSummonCe(event) {
  const native = event.ceNativeState;
  if (!native || !Number.isInteger(native.context) ||
      !Number.isInteger(native.source) || !Number.isInteger(native.stage)) return;
  const context = native.context >>> 0;
  const side = context & 1;
  const candidate = demoSummonCandidates.get(side);
  if (!candidate) return;
  const handIndex = (context >>> 6) & 0xFF;
  const fieldSlot = (context >>> 1) & 0x1F;
  // 0x4000: ordinary summon path observed in the original CE traces.
  // 0x8000: set path; 0x02000000: tribute path (excluded from pilot).
  if (handIndex !== candidate.handIndex ||
      (native.source & 0xFFF) !== candidate.source ||
      fieldSlot > 4 || candidate.before.monsters[fieldSlot] !== 0 ||
      (context & 0x4000) === 0 || (context & 0x8000) !== 0 ||
      (context & 0x02000000) !== 0 || native.stage !== 37 ||
      candidate.ce !== null || candidate.threadId !== event.threadId) {
    demoSummonCandidates.delete(side);
    return;
  }
  candidate.ce = {context, stage: native.stage, fieldSlot,
    resourceId: event.args[1] & 0xFFFF, producerSeq: traceSeen + 1};
}

// Configure before resume. Input cards are the game's internal 12-bit
// card indices, NOT external Yu-Gi-Oh! card IDs or resource IDs.
function validateDeckPlan(deck, side) {
  if (deck === null || typeof deck !== 'object' || Array.isArray(deck)) {
    throw new Error('Deck side ' + side + ': expected {main, second, third}');
  }
  const check = (label, min, max) => {
    const list = deck[label] ?? [];
    if (!Array.isArray(list) || list.length < min || list.length > max ||
        !list.every(x => Number.isInteger(x) && x >= 1 && x <= 0xFFF)) {
      throw new Error('Deck side ' + side + ': ' + label +
        ' must have ' + min + '..' + max + ' internal indices (1..4095)');
    }
    return list.slice();
  };
  for (const key of Object.keys(deck)) {
    if (!['main', 'second', 'third'].includes(key)) {
      throw new Error('Deck side ' + side + ': unknown key ' + key);
    }
  }
  if (!Object.prototype.hasOwnProperty.call(deck, 'main')) {
    throw new Error('Deck side ' + side + ': main is required');
  }
  return {main: check('main', 40, 80),
    second: check('second', 0, 15),
    third: check('third', 0, 15)};
}
function setDemoDeckPlans(plans) {
  if (!installed || !observerOnly || commandTraceHook === null ||
      demoExecutorHook === null) {
    throw new Error('Deck overrides require observer-only startup and trace demo on');
  }
  if (plans === null || typeof plans !== 'object' || Array.isArray(plans)) {
    throw new Error('Expected object with keys "0" and/or "1"');
  }
  if (Object.keys(plans).length === 0 ||
      Object.keys(plans).some(key => key !== '0' && key !== '1')) {
    throw new Error('Deck overrides require side key 0 and/or 1');
  }
  const next = [null, null];
  for (const side of [0, 1]) {
    if (Object.prototype.hasOwnProperty.call(plans, String(side))) {
      next[side] = validateDeckPlan(plans[String(side)], side);
    }
  }
  demoDeckPlans = next;
  demoDeckApplied = [0, 0];
  demoDeckVerified = [0, 0];
  demoDeckFailed = [0, 0];
  return demoDeckStatus();
}
function demoDeckStatus() {
  return {configured: demoDeckPlans.map(x => x !== null),
    applied: demoDeckApplied.slice(), verified: demoDeckVerified.slice(),
    failed: demoDeckFailed.slice()};
}
function applyDeckPlanToSource(side, slot) {
  const plan = demoDeckPlans[side];
  if (plan === null) return null;
  try {
    const config = demoSourceConfig();
    if (config.sceneMode !== 7 || slot !== (side ^ config.deckSideBit)) {
      throw new Error('Not mode 7 or source slot mapping changed');
    }
    const current = readDemoDeckSource(slot);
    if (current.error || current.mainCount < 40) {
      throw new Error('Source deck not yet populated with a valid main list');
    }
    const base = kDemoSourceDeck0.add(slot * kDemoSourceDeckStride);
    const range = Process.findRangeByAddress(base);
    if (range === null || !range.protection.includes('w') ||
        base.add(kDemoSourceDeckStride - 1).compare(range.base.add(range.size)) >= 0) {
      throw new Error('Source deck memory is not fully writable');
    }
    // Atomic in the context of this intercepted game-thread call:
    // copy current bytes for rollback if any of the writes throw.
    const previous = new Uint8Array(base.readByteArray(kDemoSourceDeckStride));
    try {
      const write = (offset, size, cards) => {
        for (let i = 0; i < size; i++) {
          base.add(offset + 2 * i).writeU16(i < cards.length ? cards[i] : 0);
        }
      };
      write(0, 80, plan.main);
      write(0xA0, 15, plan.second);
      write(0xBE, 15, plan.third);
      base.add(0xDC).writeU16(plan.main.length);
      base.add(0xDE).writeU16(plan.second.length);
      base.add(0xE0).writeU16(plan.third.length);
    } catch (error) {
      base.writeByteArray(previous);
      throw error;
    }
    const after = readDemoDeckSource(slot);
    const equal = (x, y) => x.length === y.length && x.every((v, i) => v === y[i]);
    if (!equal(after.mainIndices, plan.main) ||
        !equal(after.secondIndices, plan.second) ||
        !equal(after.thirdIndices, plan.third)) {
      base.writeByteArray(previous);
      throw new Error('Readback mismatch; restored original source deck');
    }
    demoDeckApplied[side]++;
    recordTraceEvent({kind: 'deck_override', phase: 'applied',
      side, sourceSlot: slot, mainIndices: plan.main,
      secondIndices: plan.second, thirdIndices: plan.third});
    return {applied: true, sourceSlot: slot};
  } catch (error) {
    demoDeckFailed[side]++;
    recordTraceEvent({kind: 'deck_override', phase: 'failed',
      side, sourceSlot: slot, error: String(error)});
    return {applied: false, sourceSlot: slot, error: String(error)};
  }
}

function stopDemoProbe() {
  const hooks = demoSetupHooks;
  demoSetupHooks = [];
  demoSummonCandidates.clear();
  const snapshotHook = demoSummonTickHook;
  demoSummonTickHook = null;
  const executor = demoExecutorHook;
  demoExecutorHook = null;
  let failure = null;
  for (let i = hooks.length - 1; i >= 0; i--) {
    try { hooks[i].detach(); } catch (error) { failure = error; }
  }
  if (snapshotHook !== null) {
    try { snapshotHook.detach(); } catch (error) { failure = error; }
  }
  if (executor !== null) {
    try { executor.detach(); } catch (error) { failure = error; }
  }
  if (failure !== null) throw new Error('Demo probe detach failed: ' + failure);
  return traceStatus();
}

function startDemoProbe() {
  if (combatContextMode) throw new Error('Stop combat-context before trace demo on');
  if (deckObserver !== null && deckObserver.status().deckActive)
    throw new Error('Stop trace deck before trace demo');
  if (!installed || commandTraceHook === null) {
    throw new Error('Start trace on before trace demo on');
  }
  if (demoExecutorHook !== null) throw new Error('Demo executor probe already active');
  for (const target of [
    {va: '0x005B6FF0', address: kDemoNormalExecutor},
    {va: '0x005BC700', address: kDuelUpdate}, ...kDemoSetupTargets
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
      if (event.duelRunning && sideRaw <= 1 &&
          event.handInternalIndex !== undefined) {
        try {
          const before = readDemoSummonSnapshot(sideRaw);
          if (before.handIndices[handIndex] === event.handInternalIndex &&
              demoSummonCandidates.size < 3) {
            // At most one pending observation per side. Ambiguous/incomplete
            // predecessors are silently discarded, not exported.
            demoSummonCandidates.set(sideRaw, {
              callId: this.demoCallId, enterSeq: traceSeen + 1,
              threadId: this.demoThreadId,
              handIndex, side: sideRaw, before,
              source: event.handInternalIndex, ce: null, ticks: 0
            });
          }
        } catch (error) { event.summonSnapshotReadError = String(error); }
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
          if (target.name === 'duel_entry' || target.name === 'state_reset') {
            demoSummonCandidates.clear();
          }
          this.setupId = ++demoSetupCalls;
          this.setupThreadId = Process.getCurrentThreadId();
          this.setupSide = null;
          this.setupSceneConfig = null;
          const event = {
            kind: 'demo_init', function: target.name, functionVa: target.va,
            phase: 'enter', callId: this.setupId,
            threadId: this.setupThreadId, caller: this.returnAddress.toString()
          };
          if (target.name === 'rng_seed_candidate') {
            event.candidateSeedRaw = args[0].toUInt32();
            event.candidateSeedHex = '0x' + event.candidateSeedRaw.toString(16).padStart(8, '0');
            event.duelEntrySeedCall =
              this.returnAddress.equals(kDemoDuelSeedCallerReturn);
            event.seedOverrideApplied = false;
            // A single verified callsite is allowed to replace the initial
            // demo seed. Mid-duel effect/command reseeds remain untouched.
            if (event.duelEntrySeedCall && demoSeedBase !== null &&
                kDemoSceneMode.readU32() === 7) {
              const globalIndex = demoSeedStartIndex + demoSeedApplied;
              if (globalIndex > 0xFFFFFFFF) {
                throw new Error('Global duel seed index exceeded uint32 range');
              }
              const effectiveSeed = mixDuelSeed(demoSeedBase, globalIndex);
              args[0] = ptr(effectiveSeed);
              event.seedOverrideApplied = true;
              event.seedBase = demoSeedBase;
              event.globalDuelIndex = globalIndex;
              event.effectiveSeedRaw = effectiveSeed;
              event.effectiveSeedHex = '0x' + effectiveSeed.toString(16).padStart(8, '0');
              demoSeedApplied++;
            }
          }
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
                  if (demoDeckPlans[sideRaw] !== null && config.sceneMode === 7) {
                    const outcome = applyDeckPlanToSource(sideRaw, sourceSlot);
                    event.deckOverride = outcome;
                    if (outcome?.applied) this.deckOverridePlan = demoDeckPlans[sideRaw];
                  }
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
            if (target.name === 'deck_prepare' && this.deckOverridePlan) {
              const expected = this.deckOverridePlan.main;
              const actual = event.deck.indices;
              const verified = Array.isArray(actual) &&
                actual.length === expected.length &&
                actual.every((value, index) => value === expected[index]);
              if (verified) demoDeckVerified[this.setupSide]++;
              else demoDeckFailed[this.setupSide]++;
              event.deckOverrideVerified = verified;
              recordTraceEvent({kind: 'deck_override',
                phase: verified ? 'verified' : 'mismatch',
                side: this.setupSide, expectedMainCount: expected.length,
                actualMainCount: event.deck.count,
                actualIndices: actual ?? null});
            }
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
  try {
    demoSummonTickHook = Interceptor.attach(kDuelUpdate, {
      onLeave() { sampleDemoSummonTransitions(); }
    });
  } catch (error) {
    for (let i = setup.length - 1; i >= 0; i--) setup[i].detach();
    hook.detach();
    demoSummonCandidates.clear();
    throw new Error('Passive summon tick hook failed; rolled back: ' + error);
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
      // A completed demo emits masked opcode 0x0004 / 0x8004.
      // Count producer calls independently of the bounded trace buffer.
      if ((raw & 0x7FFF) === 0x0004) {
        traceCompletedDuels++;
        // Never allow a partially observed placement to cross the terminal
        // boundary into another global duel index.
        demoSummonCandidates.clear();
      }
      const event = {
        kind: 'command',
        threadId: Process.getCurrentThreadId(),
        caller: this.returnAddress.toString(),
        args: [raw, args[1].toUInt32(),
               args[2].toUInt32(), args[3].toUInt32()]
      };
      sampleTraceLifePoints(event, raw & 0x7FFF);
      if ((raw & 0x7FFF) === 0x0086 &&
          this.returnAddress.equals(va('0x00574804'))) {
        // FUN_005747E0 (x86) pushes exactly four DWORD parameters,
        // then CALLs FUN_005B9170. At producer entry, [ESP] is
        // return 00574804, [ESP+4..16] are four parameters, and
        // [ESP+20] is the return address into the WRAPPER'S caller.
        // No second hook, stack unwinding, or mutation is required.
        try {
          const stack = this.context.esp || this.context.sp;
          event.wrapper86Caller = stack.add(20).readPointer().toString();
          event.wrapper86CallerSource = 'x86_stack_esp_plus_20';
        } catch (readError) {
          event.wrapper86CallerReadError = String(readError);
        }
      }
      if ((raw & 0x7FFF) === 0x0004 &&
          this.returnAddress.equals(va('0x005BC545'))) {
        // Raw, read-only snapshot at the verified terminal producer callsite.
        // These fields are NOT interpreted as the winning side yet.
        try {
          event.terminalNativeState = {
            duelRunningWord: kDuelRunning.readU16(),
            duelPhaseByte: kSummonDuelPhase.readU8(),
            stateWordAtA577FE: va('0x00A577FE').readU16(),
            duelModeWord: kDemoModeWord.readU32(),
            sceneMode: kDemoSceneMode.readU32(),
            // Same producer-entry instant as lifePointsObserved, not a
            // post-resolution or decoded winner state.
            lifePoints: event.lifePointsObserved?.lifePoints ?? null,
            playerFlags: [kSummonPlayerFlags.readU32(),
              kSummonPlayerFlags.add(kPlayerStride).readU32()],
            queueCount: kQueueCount.readU32()
          };
        } catch (readError) {
          event.terminalNativeReadError = String(readError);
        }
      }
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
          rememberDemoSummonCe(event);
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
  traceDropped = 0;
  traceDrained = 0;
  traceCompletedDuels = 0;
  lastTraceLifePoints = null;
  traceCapacity = capacity;
  traceSession++;
  commandTraceHook = hook;
  return traceStatus();
}

function stopCommandTrace() {
  // The probe must stop before anyone can export this bounded buffer.
  lastTraceLifePoints = null;
  stopDeckProbe();
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

// Live, loss-detecting export: peek oldest events, persist them to disk,
// then acknowledge the exact seq range to free ring capacity. Unlike
// traceevents(), these RPCs work with trace hooks still active.
function traceLivePeek(count = 256) {
  if (commandTraceHook === null) throw new Error('Live peek requires active command trace');
  if (!Number.isInteger(count) || count < 1 || count > 256) {
    throw new Error('Live peek count must be 1..256');
  }
  const length = Math.min(count, traceBuffer.length);
  const events = [];
  for (let i = 0; i < length; i++) {
    events.push(traceBuffer[(traceHead + i) % traceBuffer.length]);
  }
  return {session: traceSession, events};
}
function traceLiveAcknowledge(session, firstSeq, lastSeq) {
  if (commandTraceHook === null) throw new Error('Live ack requires active command trace');
  if (!Number.isSafeInteger(session) || session !== traceSession ||
      !Number.isSafeInteger(firstSeq) || !Number.isSafeInteger(lastSeq) ||
      firstSeq < 1 || lastSeq < firstSeq ||
      traceBuffer.length === 0) {
    throw new Error('Invalid live trace acknowledgment');
  }
  const head = traceBuffer[traceHead];
  if (head.seq !== firstSeq) {
    throw new Error('Oldest live trace sequence changed before acknowledgment');
  }
  const count = lastSeq - firstSeq + 1;
  if (count > traceBuffer.length) throw new Error('Live ack extends beyond retained events');
  for (let i = 0; i < count; i++) {
    const e = traceBuffer[(traceHead + i) % traceBuffer.length];
    if (e.seq !== firstSeq + i) {
      throw new Error('Live ack encountered non-contiguous events');
    }
  }
  const remaining = [];
  for (let i = count; i < traceBuffer.length; i++) {
    remaining.push(traceBuffer[(traceHead + i) % traceBuffer.length]);
  }
  traceBuffer = remaining;
  traceHead = 0;
  traceDrained += count;
  return {acknowledged: count, drained: traceDrained,
    retained: traceBuffer.length, session: traceSession};
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
  demoDeckPlans = [null, null];
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
  deckstatus: demoDeckStatus,
  setdemodecks: setDemoDeckPlans,
  setdemoseed: setDemoRngSeedPlan,
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
  authorizedeckobserver(fingerprint) {
    if (fingerprint !== 'db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de')
      throw new Error('Unexpected host fingerprint');
    deckFingerprint = fingerprint;
    return {verified:true};
  },
  tracedeckstart: startDeckProbe,
  tracedeckstop: stopDeckProbe,
  tracedemostart: startDemoProbe,
  tracedemostop: stopDemoProbe,
  traceprepstop: stopSummonPrepProbe,
  tracemark: markCommandTrace,
  traceevents: commandTraceEvents,
  tracelivepeek: traceLivePeek,
  traceliveack: traceLiveAcknowledge,
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
