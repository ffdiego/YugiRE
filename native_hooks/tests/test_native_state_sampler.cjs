// Node-only pure tests. No Frida, PE, DLL or live process.
'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.resolve(__dirname, '../native_state_sampler.js'), 'utf8');
const context = {};
vm.runInNewContext(source, context, {filename:'native_state_sampler.js'});

const words = new Map();
const get = at => words.get(at) || 0;
const sampler = context.createNativeStateSampler(
  at => get(at) & 0xFF, at => get(at) & 0xFFFF, at => get(at) >>> 0);
const cpu = 0x00A55D64, opponent = cpu + 0xD44;
words.set(cpu, 8000);
words.set(opponent, 8000);
words.set(cpu+4, 2);
words.set(cpu+5, 27);
words.set(cpu+6, 0);
words.set(cpu+0x6D0, 969);
words.set(cpu+0x6D4, 414);
words.set(opponent+4, 1);
words.set(opponent+0x6D0, 1049);
// Side 0 Skull Invitation in physical spell/trap candidate zone 5.
const slot = cpu + 0x10 + 5*0x90;
words.set(slot, 414);
words.set(slot+4, 0x0501);
words.set(slot+6, 2);
words.set(slot+0x8C, 0);
words.set(slot+0x0A, 1);
words.set(slot+0x0C, 0x0105);
words.set(slot+0x4C, 0x0001);
const a = sampler.capture();
assert.equal(a.sides.length, 2);
assert.equal(a.sides[0].handCount, 2);
assert.equal(a.sides[0].graveyardCount, 0);
assert.equal(a.sides[0].fieldSlots[5].cardIndex, 414);
assert.equal(a.sides[0].fieldSlots[5].associationCount, 1);
assert.equal(a.sides[0].fieldSlots[5].associations[0].referenceWord, 0x0105);
assert.equal(a.sides[1].handIndices[0], 1049);
assert.equal(sampler.diff(a,a).length, 0);

words.set(cpu, 7700);
words.set(cpu+6, 1);
words.set(cpu+4, 1);
words.set(cpu+0x6D0, 414);
words.set(slot, 0);
words.set(slot+4, 0);
words.set(slot+6, 0);
words.set(slot+0x0A, 0);
const b = sampler.capture();
const diff = sampler.diff(a,b);
assert.equal(diff.length, 1);
const changed = diff[0];
assert.equal(changed.side, 0);
assert.equal(changed.lifePoints.before, 8000);
assert.equal(changed.lifePoints.after, 7700);
assert.equal(changed.graveyardCount.before, 0);
assert.equal(changed.graveyardCount.after, 1);
assert.equal(changed.handIndices.before[0], 969);
assert.equal(changed.handIndices.after[0], 414);
assert.equal(changed.fieldSlots.length, 1);
assert.equal(changed.fieldSlots[0].slot, 5);
assert.equal(changed.fieldSlots[0].before.cardIndex, 414);
assert.equal(changed.fieldSlots[0].after.cardIndex, 0);

// Invalid raw association count is not zero and is not interpreted as active.
words.set(slot+0x0A, 65535);
const invalid = sampler.capture();
assert.equal(invalid.sides[0].fieldSlots[5].associationCountValid, false);
assert.equal(invalid.sides[0].fieldSlots[5].associations, null);
assert.equal(sampler.diff(b,invalid)[0].fieldSlots.length,1);

// Fail closed if hand byte is outside the mapped 80-entry observation scope.
words.set(cpu+4, 81);
assert.throws(() => sampler.capture(), /handCount exceeds 80/);
console.log('PASS: state sampler exposes field/hand/graveyard changes and rejects unsafe values');
