// Passive observer for the fingerprinted PE32 Joey. All addresses are VAs.
// ABI evidence: docs/evidence/cpu-deck-observer-abi.txt.
function createCpuDeckObserver(env) {
  const hash = 'db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de';
  const targets = [
    [0x44bbe0, '6aff6848305d0064a1000000'],
    [0x44bda5, '33d2b905000000f7f1'],
    [0x44bc84, '33d2f7f78d04f50000'],
    [0x44bee0, '6aff6868305d0064a1000000'],
    [0x44a920, '6aff68782f5d0064a1000000']
  ];
  const pairs = new Map([[0x48a3c5,0x48a3a8], [0x48ad71,0x48ad50],
    [0x48ae2d,0x48ae0a], [0x48a2e6,0x48a2c4], [0x48a555,0x48a533]]);
  let hooks = [], active = false, epoch = 0, serial = 0, captureMt = false;
  const selections = new Map(), scenes = new Map(), pending = new Map();
  const address = value => env.va('0x' + value.toString(16));
  const stack = (map, thread) => {
    if (!map.has(thread)) map.set(thread, []);
    return map.get(thread);
  };
  const top = (map, thread) => { const a = stack(map, thread); return a[a.length-1]; };
  const id = prefix => env.session() + ':' + prefix + ':' + (++serial);
  function checked(pointer, length, executable = false) {
    const range = Process.findRangeByAddress(pointer);
    if (range === null || !range.protection.includes('r') ||
        (executable && !range.protection.includes('x')) ||
        pointer.add(length).compare(range.base.add(range.size)) > 0)
      throw new Error('Unreadable range at ' + pointer + ' length ' + length);
    return pointer;
  }
  function bytes(pointer, length) {
    return new DataView(checked(pointer, length).readByteArray(length));
  }
  function stringObject(pointer) {
    const view = bytes(pointer, 16), length = view.getUint32(8, true);
    if (length < 1 || length > 240 || view.getUint32(12, true) < length)
      throw new Error('Invalid std::string length/capacity');
    const data = ptr(view.getUint32(4, true));
    const contents = bytes(data,length+1);
    if (contents.getUint8(length) !== 0) throw new Error('Unterminated std::string');
    return {resourcePath: data.readUtf8String(length),
      resourceDataPointer: data.toString()};
  }
  function cstring(pointer) {
    for (let length = 0; length <= 240; length++) {
      if (bytes(pointer.add(length),1).getUint8(0) === 0)
        return length === 0 ? '' : pointer.readUtf8String(length);
    }
    throw new Error('Unterminated resource path');
  }
  function profile() {
    const b = bytes(address(0xa54db1), 4);
    return {initialized:b.getUint8(0), previousDifficulty:b.getUint8(1),
      baseDifficulty:b.getUint8(2), pool:b.getUint8(3)};
  }
  function mt() {
    const b = bytes(address(0x5f25ec), 2500);
    const words = Array.from({length:624}, (_,i) => b.getUint32(i*4,true));
    const index = b.getUint32(2496,true);
    if (index > 625) throw new Error('Invalid MT index');
    return {words,index};
  }
  function source() {
    const b = bytes(address(0xa54f42), 226), result = {};
    for (const [name, offset, countOffset, limit] of [
      ['main',0,220,80],['second',160,222,15],['third',190,224,15]]) {
      const count = b.getUint16(countOffset,true);
      if (count > limit) throw new Error('Invalid source ' + name + ' count ' + count);
      result[name + 'Count'] = count;
      result[name] = Array.from({length:count},(_,i) => b.getUint16(offset+i*2,true));
      if (result[name].some(x => x < 1 || x > 1114)) throw new Error('Invalid internal card index');
    }
    return result;
  }
  function emit(kind, phase, details) { env.record({kind, phase, ...details}); }
  function safely(kind, phase, details, read) {
    try { emit(kind, phase, {...details, ...read()}); }
    catch (error) { emit(kind, phase, {...details, readError:String(error)}); }
  }
  function start(withMt, fingerprint) {
    if (active) throw new Error('Deck probe already active');
    if (fingerprint !== hash) throw new Error('Deck probe requires host EXE fingerprint verification');
    if (Process.platform !== 'windows' || Process.arch !== 'ia32' ||
        !env.main.base.equals(ptr(0x400000))) throw new Error('Unsupported Joey image');
    // Validate every target before installing the first hook. Never accept partially
    // matching code or relocate this relocation-stripped binary speculatively.
    for (const [va, hex] of targets) {
      const p = address(va), length = hex.length/2;
      if (p.compare(env.main.base) < 0 || p.add(length).compare(env.main.base.add(env.main.size)) > 0)
        throw new Error('Hook outside Joey image');
      const data = new Uint8Array(checked(p,length,true).readByteArray(length));
      if (Array.from(data,x=>x.toString(16).padStart(2,'0')).join('') !== hex)
        throw new Error('Deck hook byte mismatch at ' + p);
    }
    checked(address(0xa54db1),4); checked(address(0x5f3294),4);
    checked(address(0xa54f42),226);
    if (withMt) checked(address(0x5f25ec),2500);
    captureMt = !!withMt; const generation = ++epoch;
    const live = () => active && generation === epoch;
    const attach = (va, callbacks) => hooks.push(Interceptor.attach(address(va),callbacks));
    try {
      attach(0x44bbe0, {
        onEnter() {
          if (!live()) return;
          this.deckThread = Process.getCurrentThreadId();
          this.deckId = id('selection');
          const frame = {selectionId:this.deckId, threadId:this.deckThread,
            caller:this.returnAddress.toUInt32()};
          this.deckFrame = frame;
          stack(selections,this.deckThread).push(frame);
          safely('deck_selection','enter',frame,() => {
            this.deckOutput = checked(this.context.esp.add(4),4).readPointer();
            return {profile:profile(), sceneMode:checked(address(0x5f3294),4).readU32(),
              ...(captureMt ? {mtBefore:mt()} : {})};
          });
        },
        onLeave(retval) {
          if (!live() || !this.deckFrame) return;
          const frame = this.deckFrame;
          safely('deck_selection','leave',frame,() => {
            if (!this.deckOutput) throw new Error('Missing hidden result pointer');
            const value = stringObject(this.deckOutput);
            Object.assign(frame,value);
            const waiting = stack(pending,this.deckThread);
            if (waiting.length >= 32) {
              emit('deck_error','pending_overflow',{selectionId:waiting[0].selectionId});
              waiting.shift();
            }
            waiting.push(frame);
            const scene = top(scenes,this.deckThread);
            if (scene && scene.requestedScene === 7 && frame.caller === 0x44bf50)
              scene.internalSelectionId = frame.selectionId;
            return {...value, returnObjectMatches:retval.equals(this.deckOutput)};
          });
          const frames = stack(selections,this.deckThread);
          if (frames.pop() !== frame) emit('deck_error','selector_stack',{selectionId:frame.selectionId});
        }
      });
      for (const site of [0x44bda5,0x44bc84]) attach(site, {
        onEnter() {
          if (!live()) return;
          const threadId = Process.getCurrentThreadId(), frame = top(selections,threadId);
          emit('deck_selection','rng',{selectionId:frame ? frame.selectionId : null,
            threadId, site, rawRoll:this.context.eax.toUInt32()});
        }
      });
      attach(0x44bee0, {
        onEnter(args) {
          if (!live()) return;
          const threadId = Process.getCurrentThreadId();
          const frame = {sceneId:id('scene'),threadId,caller:this.returnAddress.toUInt32(),
            requestedScene:args[0].toInt32(), selectionId:null};
          this.deckScene = frame;
          stack(scenes,threadId).push(frame);
          safely('deck_scene','enter',frame,() => {
            const resourcePath = cstring(args[1]), argumentPointer = args[1].toString();
            const waiting = stack(pending,threadId);
            const matches = waiting.filter(x => x.caller === pairs.get(frame.caller) &&
              x.resourceDataPointer === argumentPointer && x.resourcePath === resourcePath);
            if (matches.length === 1) {
              frame.selectionId = matches[0].selectionId;
              waiting.splice(waiting.indexOf(matches[0]),1);
            }
            return {...frame,resourcePath,argumentPointer,
              correlationProof:matches.length === 1 ? 'caller+thread+pointer+path' : 'unbound'};
          });
        },
        onLeave() {
          if (!live() || !this.deckScene) return;
          const frame = this.deckScene;
          emit('deck_scene','leave',frame);
          if (stack(scenes,frame.threadId).pop() !== frame)
            emit('deck_error','scene_stack',{sceneId:frame.sceneId});
        }
      });
      attach(0x44a920, {
        onEnter() {
          if (!live()) return;
          const threadId = Process.getCurrentThreadId(), scene = top(scenes,threadId);
          const caller = this.returnAddress.toUInt32();
          let selectionId = null;
          if (scene) {
            if (caller === 0x44c0e8 && [0,1].includes(scene.requestedScene)) selectionId = scene.selectionId;
            if (caller === 0x44bf7a && scene.requestedScene === 7) selectionId = scene.internalSelectionId || null;
            if (caller === 0x44bfdd && scene.requestedScene === 7) selectionId = scene.selectionId;
          }
          this.deckLoad = {loadId:id('load'),threadId,caller,selectionId,
            sceneId:scene ? scene.sceneId : null};
          safely('deck_loader','enter',this.deckLoad,() => {
            this.deckLoad.loaderType = checked(this.context.esp.add(4),4).readU32();
            return {...this.deckLoad,...stringObject(this.context.esp.add(8))};
          });
        },
        onLeave(retval) {
          if (!live() || !this.deckLoad) return;
          safely('deck_loader','leave',this.deckLoad,() => ({returnCode:retval.toInt32(),
            ...(this.deckLoad.loaderType === 1 ? {sourceSlot1:source()} : {})}));
        }
      });
      active = true;
      emit('deck_probe','on',{schema:'joey-cpu-deck-v1',exeSha256:hash,captureMt});
    } catch (error) {
      stop();
      throw error;
    }
    return status();
  }
  function stop() {
    const wasActive = active;
    active = false; ++epoch;
    const old = hooks; hooks = [];
    const failures = [];
    for (const hook of old.reverse()) { try { hook.detach(); } catch (e) { failures.push(String(e)); } }
    selections.clear(); scenes.clear(); pending.clear();
    if (wasActive) emit('deck_probe','off',{schema:'joey-cpu-deck-v1'});
    if (failures.length) throw new Error('Deck detach failed: ' + failures.join('; '));
    return status();
  }
  function status() { return {deckActive:active,deckHooks:hooks.length,deckFingerprint:hash,captureMt}; }
  return {start,stop,status};
}
