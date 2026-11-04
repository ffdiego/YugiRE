/*
 * Generic primary-callback observer for Joey Power of Chaos (PE32 x86).
 * Run only after verifying the exact EXE hash with pe_preflight.py.
 * Hooks only 0x0059DEB0, not the per-card handlers.
 * Instrumentation patches memory transiently. No return/arg modifications.
 */
'use strict';

const DOCUMENTED_BASE = ptr('0x00400000');
const DISPATCHER_VA = ptr('0x0059DEB0');
const CARD_NAME_VA = ptr('0x005F25A8');
const MAX_CARD_INDEX = 1115; // Highest index per documented CARD_ID access.
const NAME_STRIDE = 64;
const MAX_DETAILS_PER_CARD = 12;
const SUMMARY_INTERVAL_MS = 15000;

if (Process.platform !== 'windows' || Process.arch !== 'ia32') {
  throw new Error('This probe requires a Windows PE32 x86 process');
}
const main = Process.mainModule;
if (main.name.toLowerCase() !== 'joey_pc.exe') {
  throw new Error('Unexpected process: ' + main.name);
}

function resolveVA(va) {
  return main.base.add(va.sub(DOCUMENTED_BASE));
}

const dispatcher = resolveVA(DISPATCHER_VA);
const dispatcherRegion = Process.findRangeByAddress(dispatcher);
if (dispatcherRegion === null || !dispatcherRegion.protection.includes('x')) {
  throw new Error('Dispatcher is not executable at ' + dispatcher);
}

function canRead(address, count) {
  if (address.isNull()) return false;
  const range = Process.findRangeByAddress(address);
  return range !== null && range.protection.includes('r') &&
    address.compare(range.base) >= 0 &&
    address.add(count).compare(range.base.add(range.size)) <= 0;
}

function readRequest20(pointerValue) {
  try {
    if (!canRead(pointerValue, 20)) return null;
    const raw = new Uint8Array(pointerValue.readByteArray(20));
    return {
      index: raw[0] | (raw[1] << 8),
      // This data is the native 20-byte request, not a parsed ABI.
      bytesHex: Array.from(raw, b => b.toString(16).padStart(2, '0')).join(''),
    };
  } catch (_error) {
    return null;
  }
}

function getNativeCardName(index) {
  if (index < 0 || index > MAX_CARD_INDEX) return null;
  try {
    const addr = resolveVA(CARD_NAME_VA).add(index * NAME_STRIDE);
    if (!canRead(addr, NAME_STRIDE)) return null;
    const value = addr.readCString(NAME_STRIDE);
    // CARD_Name index->64-byte strings is documented statically, but
    // this runtime decoder is experimental; reject implausible values.
    if (value === null || !/^[\x20-\x7e]{2,63}$/.test(value)) return null;
    return value;
  } catch (_error) {
    return null;
  }
}

const cards = new Map();
let totalCalls = 0;
let unreadableRequests = 0;
let lastReportedTotal = -1;

Interceptor.attach(dispatcher, {
  onEnter(args) {
    totalCalls++;
    const pointerValue = args[0];
    const request = readRequest20(pointerValue);
    this.requestPointer = pointerValue;
    this.beforeHex = request === null ? null : request.bytesHex;
    this.card = null;

    if (request === null) {
      unreadableRequests++;
      if (unreadableRequests <= 5) {
        send({kind: 'request_unreadable', call: totalCalls, pointer: pointerValue.toString()});
      }
      return;
    }

    let card = cards.get(request.index);
    if (card === undefined) {
      card = {
        index: request.index,
        name: getNativeCardName(request.index),
        calls: 0,
        detailCount: 0,
        lastLow16: null,
        returns: new Map(),
      };
      cards.set(request.index, card);
      send({kind: 'card_seen', index: card.index,
            name: card.name, source: card.name === null ? 'index_only' : 'runtime_CARD_Name'});
    }
    card.calls++;
    this.card = card;
    this.shouldReport = card.detailCount < MAX_DETAILS_PER_CARD;
    if (this.shouldReport) card.detailCount++;
    this.callNo = card.calls;
  },

  onLeave(retval) {
    if (this.card === null) return;
    const card = this.card;
    const raw = retval.toUInt32();
    const low16 = raw & 0xffff;
    const hex = '0x' + raw.toString(16);
    card.returns.set(hex, (card.returns.get(hex) || 0) + 1);
    const changedReturn = card.lastLow16 !== null && card.lastLow16 !== low16;
    card.lastLow16 = low16;

    if (this.shouldReport) {
      const after = readRequest20(this.requestPointer);
      send({
        kind: 'card_callback',
        name: card.name,
        index: card.index,
        cardCall: this.callNo,
        thread: this.threadId,
        rawEaxHex: hex,
        returnLow16Hex: '0x' + low16.toString(16),
        returnChangedFromPrevious: changedReturn,
        requestBefore20: this.beforeHex,
        requestAfter20: after === null ? null : after.bytesHex,
        requestBytesChanged: after !== null && this.beforeHex !== after.bytesHex,
      });
    }
  },
});

send({kind: 'ready_all_cards', pid: Process.id, arch: Process.arch,
      actualBase: main.base.toString(), dispatcher: dispatcher.toString(),
      maxDetailsPerCard: MAX_DETAILS_PER_CARD,
      note: 'Only primary callback invocations; cards without a primary callback are not tracked.'});

setInterval(() => {
  if (lastReportedTotal === totalCalls) return;
  lastReportedTotal = totalCalls;
  const top = Array.from(cards.values())
    .sort((a, b) => b.calls - a.calls)
    .map(c => ({index: c.index, name: c.name, calls: c.calls,
                returnValues: Array.from(c.returns.entries()).map(([value, count]) => ({value, count}))}));
  send({kind: 'summary', totalCalls, distinctCardIndexes: cards.size,
        unreadableRequests, cards: top});
}, SUMMARY_INTERVAL_MS);
