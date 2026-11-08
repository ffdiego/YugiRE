// Pure Node verification of the deterministic 32-bit seed derivation.
// No Frida, Joey, or process mutation. Run: node tests/test_demo_rng_seed.cjs
'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const source = fs.readFileSync(
  path.resolve(__dirname, '../yugire_dual_loader.js'), 'utf8'
);
const start = source.indexOf('function mixDuelSeed(');
const end = source.indexOf('function setDemoRngSeedPlan(', start);
assert.ok(start >= 0 && end > start, 'Seed mixer must be present');
const mixDuelSeed = vm.runInNewContext(source.slice(start, end) + '\nmixDuelSeed;');

// Known test vectors generated with JavaScript Math.imul (uint32 overflow).
for (const [base, index, expected] of [
  [0, 0, 0], [0, 1, 1753845952], [0, 2, 3507691905],
  [12345, 0, 2435775735], [12345, 1, 4228375765],
  [12345, 2, 1634509376], [0xFFFFFFFF, 0, 1734902346],
  [0xFFFFFFFF, 1, 0]
]) {
  assert.equal(mixDuelSeed(base, index), expected);
}

// All 20,000 distinct global indices must produce distinct 32-bit seeds.
const seen = new Set();
for (let index = 0; index < 20000; index++) {
  const value = mixDuelSeed(12345, index);
  assert.ok(!seen.has(value), 'Collision at global index ' + index);
  seen.add(value);
  assert.equal(mixDuelSeed(12345, index), value);
}
assert.equal(seen.size, 20000);
// Core invariants: native RNG set must remain scoped to the verified return
// address, demo scene 7, and deliberate RPC configuration.
assert.ok(source.includes("kDemoDuelSeedCallerReturn = va('0x005BFEBD')"));
assert.ok(source.includes('this.returnAddress.equals(kDemoDuelSeedCallerReturn)'));
assert.ok(source.includes('kDemoSceneMode.readU32() === 7'));
assert.ok(source.includes('demoSeedBase !== null'));
assert.ok(source.includes('args[0] = ptr(effectiveSeed)'));
assert.ok(source.includes('setdemoseed: setDemoRngSeedPlan'));
console.log('PASS: RNG seed mixing vectors, collision-free indices and guarded native callsite');
