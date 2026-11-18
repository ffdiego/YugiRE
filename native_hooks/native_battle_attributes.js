/* Read-only battle attribute snapshot for the verified Joey PE32 executable.
 * docs/22-battle-core.md: 0x005B7C50 prepares two 16-byte actor blocks at
 * 0x00A57844 / 0x00A57854; 0x005A2740 emits 0x31 after calculation.
 *
 * This module installs NO hooks, calls NO game functions and writes NO memory.
 * The caller supplies mapped-VA readers at command-producer onEnter (0x31).
 * Fields remain raw observations; they do not identify modifier causality.
 */
'use strict';
function createNativeBattleAttributeSampler(readU16, readS32, readU32) {
  const selector = 0x00A57840, firstBlock = 0x00A57844, blockSize = 0x10;
  function actor(index) {
    const base = firstBlock + index * blockSize;
    const descriptorWord = readU16(base);
    const cardIndexWord = readU16(base + 2);
    const calculatedAttack = readS32(base + 4);
    const calculatedDefense = readS32(base + 8);
    const damageWord = readU32(base + 12);
    if (!Number.isInteger(cardIndexWord) || cardIndexWord > 4095 ||
        !Number.isInteger(calculatedAttack) || !Number.isInteger(calculatedDefense) ||
        Math.abs(calculatedAttack) > 1000000 ||
        Math.abs(calculatedDefense) > 1000000) {
      throw new Error('Invalid battle attribute block (uninitialized or mismatched layout)');
    }
    return {blockIndex:index, descriptorWord, cardIndexWord,
            calculatedAttack, calculatedDefense, damageWord};
  }
  function capture() {
    const selectorWord = readU32(selector);
    return {
      schema:'joey-command-entry-battle-attributes-v1',
      samplePoint:'command_producer_onEnter_0x31',
      nonAtomic:true, selectorWord,
      blocks:[actor(0), actor(1)],
      interpretationLimit:
        'Unverified relation of slot/side selectors. Values read at 0x31 producer entry ' +
        'are not proof of the effective stats at battle application or of any modifier source.'
    };
  }
  return {capture};
}
