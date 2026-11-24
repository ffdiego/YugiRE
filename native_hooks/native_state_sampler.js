/* Read-only native Joey state sampler for the verified PE32 image.
 * No Frida interceptors or game calls here: caller supplies VA readers.
 * Layout from joeytrace_core.py / native_hooks/src/duel_state.cpp.
 * The results are command-entry observations; they are NOT atomic transactions.
 */
'use strict';
function createNativeStateSampler(readU8, readU16, readU32) {
  const base = 0x00A55D64, playerStride = 0xD44;
  const firstSlot = 0x10, slotStride = 0x90, fieldSlots = 11;
  const handOffset = 0x6D0, handLimit = 80, associationsLimit = 32;
  const scalarKeys = ['lifePoints','handCount','deckCount','graveyardCount',
    'banishedCount','terminalFlags'];

  function player(side) {
    const p = base + side * playerStride;
    const handCount = readU8(p + 4);
    if (handCount > handLimit) throw new Error('Native handCount exceeds 80 for side ' + side);
    const result = {
      side,
      lifePoints: readU16(p), handCount,
      deckCount: readU8(p + 5),
      graveyardCount: readU8(p + 6),
      // Byte +8 overlaps the terminal word; preserve it as raw observed data.
      rawPlayerStatusByte8: readU8(p + 8),
      terminalFlags: readU16(p + 8) & 0x700,
      fieldSlots: [], handIndices: []
    };
    for (let slot = 0; slot < fieldSlots; slot++) {
      const address = p + firstSlot + slot * slotStride;
      const count = readU16(address + 0x0A);
      const valid = count <= associationsLimit;
      const associations = [];
      if (valid) {
        for (let i = 0; i < count; i++) {
          associations.push({
            referenceWord: readU16(address + 0x0C + 2 * i),
            metadataWord: readU16(address + 0x4C + 2 * i)
          });
        }
      }
      result.fieldSlots.push({
        slot,
        cardIndex: readU32(address) & 0xFFF,
        occurrenceWord: readU16(address + 4),
        positionWord: readU16(address + 6),
        flagsWord: readU32(address + 0x8C),
        associationCount: count,
        associationCountValid: valid,
        associations: valid ? associations : null
      });
    }
    for (let i = 0; i < handCount; i++) {
      result.handIndices.push(readU32(p + handOffset + i * 4) & 0xFFF);
    }
    return result;
  }

  function capture() {
    return {sides:[player(0),player(1)]};
  }

  function diff(previous, current) {
    const changes = [];
    for (let side = 0; side < 2; side++) {
      const a = previous.sides[side], b = current.sides[side];
      const change = {side};
      for (const key of scalarKeys.concat(['rawPlayerStatusByte8'])) {
        if (a[key] !== b[key])
          change[key] = {before:a[key],after:b[key]};
      }
      if (JSON.stringify(a.handIndices) !== JSON.stringify(b.handIndices)) {
        change.handIndices = {before:a.handIndices, after:b.handIndices};
      }
      const slots = [];
      for (let i = 0; i < fieldSlots; i++) {
        if (JSON.stringify(a.fieldSlots[i]) !== JSON.stringify(b.fieldSlots[i])) {
          slots.push({slot:i, before:a.fieldSlots[i], after:b.fieldSlots[i]});
        }
      }
      if (slots.length) change.fieldSlots = slots;
      if (Object.keys(change).length > 1) changes.push(change);
    }
    return changes;
  }
  return {capture,diff};
}
