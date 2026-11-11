// Read-only Ghidra script: reachability over the static call graph from one or
// more roots, classifying every reachable function by which Win32 imports it
// (or anything it calls) can reach. Answers "how much of the duel core is pure
// and where is the stub boundary?" before deciding on emulation / recompilation.
//
// Arguments: <report.txt> [VA | root:VA | stop:VA | table:VA[:count] | depth:N | addr:follow] ...
//   VA, root:VA     extra root function (hex, with or without 0x)
//   stop:VA         include the function but do not descend into it (leaf)
//   table:VA[:n]    read n DWORD code pointers at VA (omit n: read until the
//                   first non-code pointer, max 512) and use each as a root
//   depth:N         do not expand functions deeper than N calls from a root
//   addr:follow     also follow functions whose address is taken by code
//                   (push offset / mov [x], offset); default: list only
// Without roots or tables the defaults are used: 005BC700 (duel state
// dispatcher), 005B7B50 (CPU stage dispatcher) and the dispatcher state table 005EFBB0.
//
// Output: <report.txt> and <report.txt>.functions.csv. Functions that Ghidra has
// not defined yet (LAB_ targets) are disassembled and created inside a transient
// transaction that is rolled back; nothing is saved to the project.
// Not resolved statically: indirect calls (reported), APIs resolved at runtime
// through GetProcAddress (look for the "dynload" category), vtables.
// @category YugiRE
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.address.AddressSetView;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.Instruction;
import ghidra.program.model.listing.InstructionIterator;
import ghidra.program.model.mem.Memory;
import ghidra.program.model.mem.MemoryBlock;
import ghidra.program.model.symbol.ExternalLocation;
import ghidra.program.model.symbol.ExternalReference;
import ghidra.program.model.symbol.FlowType;
import ghidra.program.model.symbol.RefType;
import ghidra.program.model.symbol.Reference;
import java.io.File;
import java.io.FileOutputStream;
import java.io.OutputStreamWriter;
import java.io.PrintWriter;
import java.nio.charset.StandardCharsets;
import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.Deque;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Set;
import java.util.TreeMap;
import java.util.TreeSet;

public class CoreBoundaryReach extends GhidraScript {
    private static final String EXPECTED_SHA =
        "db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de";
    private static final long[] DEFAULT_ROOTS = {0x005BC700L, 0x005B7B50L};
    private static final long DEFAULT_TABLE = 0x005EFBB0L;
    private static final int TABLE_AUTO_MAX = 512;
    private static final int TOP_BOUNDARY = 80;
    private static final int TOP_INDIRECT = 60;
    private static final int TOP_ADDRESS_TAKEN = 80;

    /** Categories whose imports need a stub (or a virtual clock) outside Windows. */
    private static final Set<String> STUB_CATEGORIES = new HashSet<>(Arrays.asList(
        "ui", "gdi", "audio", "com", "file", "time", "net", "registry", "dynload"));

    private static final class Node {
        final Function fn;
        final Address entry;
        final long key;
        final String name;
        String origin = "call";
        int depth = -1;
        Node parent;
        boolean stop;
        boolean limited;
        int instructions;
        final Set<Long> callees = new LinkedHashSet<>();
        final Set<Long> callers = new LinkedHashSet<>();
        final Set<String> directImports = new TreeSet<>();
        final List<String> indirectSites = new ArrayList<>();
        Set<String> transImports = new TreeSet<>();
        boolean transIndirect;
        boolean transOpen; // reaches a STOP/LIMIT node, so "nothing found" is not proof

        Node(Function fn) {
            this.fn = fn;
            this.entry = fn.getEntryPoint();
            this.key = entry.getOffset();
            this.name = fn.getName();
        }

        String label() {
            String hex = entry.toString();
            return name.startsWith("FUN_") ? name : name + "@" + hex;
        }
    }

    private final Map<Long, Node> nodes = new LinkedHashMap<>();
    private final Deque<Node> queue = new ArrayDeque<>();
    private final Set<Long> stopSet = new HashSet<>();
    private final Map<Long, String> addressTaken = new LinkedHashMap<>();
    private final List<String> rootNotes = new ArrayList<>();
    private final List<String> rootFailures = new ArrayList<>();
    private boolean followAddressTaken = false;
    private int maxDepth = Integer.MAX_VALUE;
    private int unresolvedTargets = 0;
    private int createdFunctions = 0;

    private static String csv(String value) {
        return "\"" + (value == null ? "" : value.replace("\"", "\"\"")
                                                   .replace("\r", " ")
                                                   .replace("\n", " ")) + "\"";
    }

    private static long parseVa(String text) {
        String s = text.trim().toLowerCase(Locale.ROOT);
        if (s.startsWith("0x")) s = s.substring(2);
        return Long.parseLong(s, 16) & 0xFFFFFFFFL;
    }

    private static String hex(long va) {
        return String.format("%08x", va);
    }

    @Override
    public void run() throws Exception {
        String sha = currentProgram.getExecutableSHA256();
        if (!EXPECTED_SHA.equalsIgnoreCase(sha)) {
            throw new IllegalStateException("Joey executable fingerprint differs: " + sha);
        }
        String[] args = getScriptArgs();
        File output = (args != null && args.length > 0)
            ? new File(args[0]) : askFile("Export core boundary report", "Save");
        if (output == null) return;
        File dir = output.getAbsoluteFile().getParentFile();
        if (dir != null && !dir.isDirectory() && !dir.mkdirs()) {
            throw new IllegalStateException("Cannot create output directory: " + dir);
        }

        List<Long> roots = new ArrayList<>();
        List<long[]> tables = new ArrayList<>();
        for (int i = 1; args != null && i < args.length; i++) {
            parseToken(args[i], roots, tables);
        }
        if (roots.isEmpty() && tables.isEmpty()) {
            for (long r : DEFAULT_ROOTS) roots.add(r);
            tables.add(new long[] {DEFAULT_TABLE, -1});
        }

        int tx = currentProgram.startTransaction("Transient core boundary reach");
        try {
            build(roots, tables);
            propagate();
        } finally {
            currentProgram.endTransaction(tx, false); // never persisted
        }
        write(output, sha);
    }

    private void parseToken(String token, List<Long> roots, List<long[]> tables) {
        String t = token.trim();
        String lower = t.toLowerCase(Locale.ROOT);
        try {
            if (lower.startsWith("stop:")) {
                stopSet.add(parseVa(t.substring(5)));
            } else if (lower.startsWith("table:")) {
                String[] p = t.split(":");
                tables.add(new long[] {parseVa(p[1]), p.length > 2 ? Integer.parseInt(p[2]) : -1});
            } else if (lower.startsWith("depth:")) {
                maxDepth = Integer.parseInt(t.substring(6));
            } else if (lower.equals("addr:follow")) {
                followAddressTaken = true;
            } else if (lower.startsWith("root:")) {
                roots.add(parseVa(t.substring(5)));
            } else {
                roots.add(parseVa(t));
            }
        } catch (RuntimeException e) {
            throw new IllegalArgumentException("Bad argument '" + token + "'. Usage: report.txt "
                + "[VA|root:VA|stop:VA|table:VA[:count]|depth:N|addr:follow]...", e);
        }
    }

    // ---------------------------------------------------------------- graph

    private void build(List<Long> roots, List<long[]> tables) {
        for (long va : roots) {
            rootNotes.add("root " + hex(va));
            seedRoot(toAddr(va), "root");
        }
        for (long[] t : tables) {
            Map<Integer, Address> entries = readTable(t[0], (int) t[1]);
            rootNotes.add(String.format("table %s: %d code pointer(s)%s", hex(t[0]), entries.size(),
                t[1] < 0 ? " (auto length)" : ""));
            for (Map.Entry<Integer, Address> e : entries.entrySet()) {
                seedRoot(e.getValue(), String.format("table:%s[%d]", hex(t[0]), e.getKey()));
            }
        }
        while (!queue.isEmpty() && !monitor.isCancelled()) {
            Node n = queue.poll();
            if (n.stop && n.depth > 0) continue;
            if (n.depth >= maxDepth) {
                n.limited = true;
                continue;
            }
            analyze(n);
        }
    }

    private Map<Integer, Address> readTable(long base, int count) {
        Map<Integer, Address> out = new LinkedHashMap<>();
        Memory mem = currentProgram.getMemory();
        int limit = count < 0 ? TABLE_AUTO_MAX : count;
        for (int i = 0; i < limit; i++) {
            try {
                Address slot = toAddr(base + 4L * i);
                if (!mem.contains(slot)) break;
                long v = mem.getInt(slot) & 0xFFFFFFFFL;
                Address target = toAddr(v);
                MemoryBlock block = mem.getBlock(target);
                boolean code = block != null && block.isExecute();
                if (!code) {
                    if (count < 0) break;
                    rootFailures.add(String.format("table %s[%d] = %s is not a code pointer",
                        hex(base), i, hex(v)));
                    continue;
                }
                out.put(i, target);
            } catch (Exception e) {
                if (count < 0) break;
                rootFailures.add(String.format("table %s[%d] unreadable: %s", hex(base), i, e));
            }
        }
        return out;
    }

    private void seedRoot(Address a, String origin) {
        Function f = ensureFunction(a);
        if (f == null) {
            rootFailures.add(a + " (" + origin + "): no function and could not create one");
            return;
        }
        Node n = nodeFor(f, origin);
        if (n.depth < 0) {
            n.depth = 0;
            queue.add(n);
        }
    }

    private Function ensureFunction(Address a) {
        try {
            Function f = getFunctionAt(a);
            if (f != null) return f;
            Function containing = getFunctionContaining(a);
            if (containing != null) return containing;
            if (getInstructionAt(a) == null && !disassemble(a)) return null;
            Function created = createFunction(a, null);
            if (created != null) createdFunctions++;
            return created;
        } catch (Exception e) {
            return null;
        }
    }

    private Node nodeFor(Function f, String origin) {
        long key = f.getEntryPoint().getOffset();
        Node n = nodes.get(key);
        if (n == null) {
            n = new Node(f);
            n.origin = origin;
            n.stop = stopSet.contains(key);
            nodes.put(key, n);
        }
        return n;
    }

    private void addEdge(Node from, Function target) {
        Node c = nodeFor(target, "call");
        from.callees.add(c.key);
        c.callers.add(from.key);
        if (c.depth < 0) {
            c.depth = from.depth + 1;
            c.parent = from;
            queue.add(c);
        }
    }

    private String fmt(ExternalLocation el) {
        if (el == null) return null;
        String lib = el.getLibraryName();
        String name = el.getLabel();
        if (name == null || name.isEmpty()) name = el.getOriginalImportedName();
        if (name == null || name.isEmpty()) name = "?";
        if (lib == null) lib = "?";
        lib = lib.replaceAll("(?i)\\.dll$", "").toUpperCase(Locale.ROOT);
        return lib + "!" + name;
    }

    private String fmtExternalFunction(Function ext) {
        String s = fmt(ext.getExternalLocation());
        if (s != null) return s;
        return ext.getParentNamespace().getName().replaceAll("(?i)\\.dll$", "")
            .toUpperCase(Locale.ROOT) + "!" + ext.getName();
    }

    /** Import referenced by this reference, directly or through an IAT slot; else null. */
    private String importFor(Reference ref) {
        try {
            if (ref.isExternalReference()) {
                return fmt(((ExternalReference) ref).getExternalLocation());
            }
            Address to = ref.getToAddress();
            if (to != null && to.isMemoryAddress()) {
                for (Reference r2 : currentProgram.getReferenceManager().getReferencesFrom(to)) {
                    if (r2.isExternalReference()) {
                        return fmt(((ExternalReference) r2).getExternalLocation());
                    }
                }
            }
        } catch (Exception e) {
            // fall through: treated as not an import
        }
        return null;
    }

    /** Resolves a call/jump target; thunks to imports become direct imports. */
    private boolean linkTarget(Node n, Address to, Instruction ins) {
        Function target = ensureFunction(to);
        if (target == null) {
            unresolvedTargets++;
            n.indirectSites.add(ins.getAddress() + " " + ins + " ; target " + to
                + " is not a function");
            return false;
        }
        if (target.isThunk()) {
            Function th = target.getThunkedFunction(true);
            if (th != null) {
                if (th.isExternal()) {
                    n.directImports.add(fmtExternalFunction(th));
                    return true;
                }
                target = th;
            }
        }
        if (target.isExternal()) {
            n.directImports.add(fmtExternalFunction(target));
            return true;
        }
        addEdge(n, target);
        return true;
    }

    private void analyze(Node n) {
        AddressSetView body = n.fn.getBody();
        List<Instruction> list = new ArrayList<>();
        InstructionIterator it = currentProgram.getListing().getInstructions(body, true);
        while (it.hasNext()) list.add(it.next()); // snapshot: creating functions may touch the listing
        n.instructions = list.size();
        for (Instruction ins : list) {
            FlowType ft = ins.getFlowType();
            boolean resolved = false;
            for (Reference ref : ins.getReferencesFrom()) {
                String imp = importFor(ref);
                if (imp != null) {
                    n.directImports.add(imp);
                    if (ft.isCall()) resolved = true;
                    continue;
                }
                Address to = ref.getToAddress();
                if (to == null || !to.isMemoryAddress()) continue;
                RefType rt = ref.getReferenceType();
                boolean callEdge = rt.isCall();
                boolean tailCall = rt.isJump() && ft.isJump() && ft.isUnConditional()
                    && !ft.isComputed() && !body.contains(to);
                if (callEdge || tailCall) {
                    if (linkTarget(n, to, ins) && ft.isCall()) resolved = true;
                } else if (rt.isData() && !body.contains(to)) {
                    noteAddressTaken(n, ins, to);
                }
            }
            if (ft.isCall() && ft.isComputed() && !resolved) {
                n.indirectSites.add(ins.getAddress() + " " + ins);
            }
        }
    }

    /** push offset / mov [x], offset that points at a plausible function entry. */
    private void noteAddressTaken(Node n, Instruction ins, Address to) {
        try {
            MemoryBlock block = currentProgram.getMemory().getBlock(to);
            if (block == null || !block.isExecute()) return;
            Function containing = getFunctionContaining(to);
            boolean isEntry = containing != null && containing.getEntryPoint().equals(to);
            if (containing != null && !isEntry) return; // middle of a function
            if (!isEntry && !looksLikeEntry(to)) return;
            addressTaken.putIfAbsent(to.getOffset(), n.label() + " @" + ins.getAddress()
                + (isEntry ? "" : " (not yet a Ghidra function)"));
            if (followAddressTaken) linkTarget(n, to, ins);
        } catch (Exception e) {
            // ignore: best-effort lead list
        }
    }

    private boolean looksLikeEntry(Address to) throws Exception {
        if (getInstructionAt(to) != null) return true;
        Memory mem = currentProgram.getMemory();
        int b0 = mem.getByte(to) & 0xFF;
        int b1 = mem.getByte(to.add(1)) & 0xFF;
        int b2 = mem.getByte(to.add(2)) & 0xFF;
        return (b0 == 0x55 && ((b1 == 0x8B && b2 == 0xEC) || (b1 == 0x89 && b2 == 0xE5)))
            || (b0 == 0x83 && b1 == 0xEC) || (b0 == 0x81 && b1 == 0xEC) || (b0 == 0x6A && b1 == 0xFF);
    }

    private void propagate() {
        Deque<Node> work = new ArrayDeque<>();
        for (Node n : nodes.values()) {
            n.transImports = new TreeSet<>(n.directImports);
            n.transIndirect = !n.indirectSites.isEmpty();
            n.transOpen = n.limited || (n.stop && n.depth > 0);
            work.add(n);
        }
        while (!work.isEmpty()) {
            Node n = work.poll();
            for (long ck : n.callers) {
                Node c = nodes.get(ck);
                boolean changed = c.transImports.addAll(n.transImports);
                if (n.transIndirect && !c.transIndirect) {
                    c.transIndirect = true;
                    changed = true;
                }
                if (n.transOpen && !c.transOpen) {
                    c.transOpen = true;
                    changed = true;
                }
                if (changed) work.add(c);
            }
        }
    }

    // ------------------------------------------------------------ classification

    private static String category(String imp) {
        int bang = imp.indexOf('!');
        String lib = bang < 0 ? "" : imp.substring(0, bang);
        String name = bang < 0 ? imp : imp.substring(bang + 1);
        switch (lib) {
            case "USER32": return "ui";
            case "GDI32": return "gdi";
            case "WINMM": return name.toLowerCase(Locale.ROOT).startsWith("time") ? "time" : "audio";
            case "MSACM32": return "audio";
            case "OLE32": case "OLEAUT32": return "com";
            case "WS2_32": case "WSOCK32": return "net";
            case "ADVAPI32": return name.startsWith("Reg") ? "registry" : "misc";
            case "KERNEL32": return kernel32Category(name);
            default: return "misc";
        }
    }

    private static String kernel32Category(String name) {
        if (name.equals("LoadLibraryA") || name.equals("LoadLibraryW") || name.equals("LoadLibraryExA")
            || name.equals("LoadLibraryExW") || name.equals("GetProcAddress")
            || name.equals("FreeLibrary") || name.startsWith("GetModuleHandle")) return "dynload";
        if (name.equals("GetTickCount") || name.startsWith("GetSystemTime") || name.equals("GetLocalTime")
            || name.startsWith("QueryPerformance") || name.startsWith("Sleep")
            || name.equals("GetTickCount64")) return "time";
        if (name.startsWith("CreateFile") || name.equals("ReadFile") || name.equals("WriteFile")
            || name.equals("SetFilePointer") || name.startsWith("GetFile") || name.startsWith("SetFile")
            || name.startsWith("FindFirstFile") || name.startsWith("FindNextFile")
            || name.equals("FindClose") || name.startsWith("DeleteFile") || name.startsWith("MoveFile")
            || name.equals("FlushFileBuffers") || name.equals("GetFullPathNameA")
            || name.endsWith("CurrentDirectoryA") || name.startsWith("CreateDirectory")) return "file";
        if (name.startsWith("Tls") || name.contains("CriticalSection") || name.startsWith("Interlocked")
            || name.startsWith("WaitFor") || name.endsWith("Event") || name.endsWith("Mutex")
            || name.endsWith("Thread") || name.endsWith("ThreadId")) return "thread";
        if (name.startsWith("Heap") || name.startsWith("Virtual") || name.startsWith("Global")
            || name.startsWith("Local") || name.equals("GetProcessHeap")
            || name.startsWith("Rtl")) return "memory";
        return "misc";
    }

    private String classOf(Node n) {
        if (n.stop && n.depth > 0) return "STOP";
        if (n.limited) return "LIMIT";
        boolean any = false;
        for (String imp : n.transImports) {
            any = true;
            if (STUB_CATEGORIES.contains(category(imp))) return "NEEDS_STUB";
        }
        if (n.transIndirect) return "INDIRECT";
        if (n.transOpen) return "OPEN";
        return any ? "RUNTIME_ONLY" : "PURE";
    }

    private Set<String> stubCategories(Node n) {
        Set<String> cats = new TreeSet<>();
        for (String imp : n.transImports) {
            String c = category(imp);
            if (STUB_CATEGORIES.contains(c)) cats.add(c);
        }
        return cats;
    }

    private int ancestors(Node n) {
        Set<Long> seen = new HashSet<>();
        Deque<Long> dq = new ArrayDeque<>();
        seen.add(n.key);
        dq.add(n.key);
        while (!dq.isEmpty()) {
            Node x = nodes.get(dq.poll());
            for (long c : x.callers) if (seen.add(c)) dq.add(c);
        }
        return seen.size();
    }

    private String chain(Node n, String imp) {
        List<String> parts = new ArrayList<>();
        for (Node c = n; c != null; c = c.parent) parts.add(c.label());
        Collections.reverse(parts);
        if (parts.size() > 8) {
            List<String> shortened = new ArrayList<>(parts.subList(0, 3));
            shortened.add("...(" + (parts.size() - 7) + " more)...");
            shortened.addAll(parts.subList(parts.size() - 4, parts.size()));
            parts = shortened;
        }
        parts.add("[" + imp + "]");
        return String.join(" > ", parts);
    }

    // -------------------------------------------------------------------- output

    private void write(File output, String sha) throws Exception {
        Map<String, Integer> classCount = new TreeMap<>();
        int indirectSiteTotal = 0;
        for (Node n : nodes.values()) {
            classCount.merge(classOf(n), 1, Integer::sum);
            indirectSiteTotal += n.indirectSites.size();
        }

        // imports: who references directly, who is affected transitively
        Map<String, List<Node>> direct = new TreeMap<>();
        Map<String, Integer> affected = new TreeMap<>();
        for (Node n : nodes.values()) {
            for (String imp : n.directImports) direct.computeIfAbsent(imp, k -> new ArrayList<>()).add(n);
            for (String imp : n.transImports) affected.merge(imp, 1, Integer::sum);
        }

        // boundary functions
        List<Node> boundary = new ArrayList<>();
        Map<Long, Integer> ancestorCount = new LinkedHashMap<>();
        for (Node n : nodes.values()) {
            boolean stub = false;
            for (String imp : n.directImports) {
                if (STUB_CATEGORIES.contains(category(imp))) stub = true;
            }
            if (stub) {
                boundary.add(n);
                ancestorCount.put(n.key, ancestors(n));
            }
        }
        boundary.sort((a, b) -> {
            int c = Integer.compare(ancestorCount.get(b.key), ancestorCount.get(a.key));
            return c != 0 ? c : Integer.compare(a.depth, b.depth);
        });

        try (PrintWriter out = new PrintWriter(new OutputStreamWriter(
                new FileOutputStream(output), StandardCharsets.UTF_8))) {
            out.println("SHA256: " + sha);
            out.println("Core boundary reach (static call graph, read-only; nothing saved)");
            for (String r : rootNotes) out.println("  " + r);
            for (long s : stopSet) out.println("  stop " + hex(s));
            out.println("  max depth: " + (maxDepth == Integer.MAX_VALUE ? "unlimited" : maxDepth)
                + "; follow address-taken: " + followAddressTaken);
            for (String f : rootFailures) out.println("  ROOT PROBLEM: " + f);
            out.println();

            out.println("== SUMMARY ==");
            out.println("reachable functions: " + nodes.size()
                + "  (functions created transiently: " + createdFunctions + ")");
            for (Map.Entry<String, Integer> e : classCount.entrySet()) {
                out.println(String.format("  %-13s %5d  %5.1f%%", e.getKey(), e.getValue(),
                    100.0 * e.getValue() / Math.max(1, nodes.size())));
            }
            out.println("unresolved indirect call sites (reachable code): " + indirectSiteTotal
                + "; call/jump targets that are not functions: " + unresolvedTargets);
            out.println("address-taken functions not in the graph: " + countOutside(addressTaken.keySet()));
            out.println("classes: PURE = no imports and no unresolved indirect calls below; "
                + "RUNTIME_ONLY = only memory/thread/misc imports; INDIRECT = unresolved indirect calls "
                + "below, no stub imports seen; OPEN = reaches a STOP/LIMIT node, so absence of imports is not proof; "
                + "NEEDS_STUB = reaches ui/gdi/audio/com/file/time/net/registry/dynload; "
                + "STOP/LIMIT = not expanded.");
            out.println();

            out.println("== IMPORTS REACHED (by category) ==");
            out.println("category  import  directFrom  affectedFunctions  shortest chain");
            List<String> imports = new ArrayList<>(affected.keySet());
            imports.sort((a, b) -> {
                int c = category(a).compareTo(category(b));
                return c != 0 ? c : Integer.compare(affected.get(b), affected.get(a));
            });
            for (String imp : imports) {
                List<Node> callers = direct.getOrDefault(imp, Collections.emptyList());
                Node best = null;
                for (Node c : callers) if (best == null || c.depth < best.depth) best = c;
                out.println(String.format("%-9s %s  direct=%d  affected=%d%s", category(imp), imp,
                    callers.size(), affected.get(imp), best == null ? "" : "\n          " + chain(best, imp)));
            }
            out.println();

            out.println("== STUB BOUNDARY (functions calling stub-category imports directly) ==");
            out.println("sorted by how many reachable functions depend on them (ancestors, itself included)");
            int shown = 0;
            for (Node n : boundary) {
                if (shown++ >= TOP_BOUNDARY) {
                    out.println("... " + (boundary.size() - TOP_BOUNDARY) + " more in the CSV");
                    break;
                }
                List<String> stubImports = new ArrayList<>();
                for (String imp : n.directImports) {
                    if (STUB_CATEGORIES.contains(category(imp))) stubImports.add(imp);
                }
                String list = stubImports.size() > 6
                    ? String.join(", ", stubImports.subList(0, 6)) + ", +" + (stubImports.size() - 6)
                    : String.join(", ", stubImports);
                out.println(String.format("%s  %-22s depth=%-3d ancestors=%-5d %s", hex(n.key),
                    n.name, n.depth, ancestorCount.get(n.key), list));
            }
            out.println();

            out.println("== UNRESOLVED INDIRECT CALLS (reachable functions) ==");
            List<Node> withIndirect = new ArrayList<>();
            for (Node n : nodes.values()) if (!n.indirectSites.isEmpty()) withIndirect.add(n);
            withIndirect.sort((a, b) -> Integer.compare(b.indirectSites.size(), a.indirectSites.size()));
            shown = 0;
            for (Node n : withIndirect) {
                if (shown++ >= TOP_INDIRECT) {
                    out.println("... " + (withIndirect.size() - TOP_INDIRECT) + " more functions in the CSV");
                    break;
                }
                out.println(String.format("%s  %-22s sites=%d", hex(n.key), n.name, n.indirectSites.size()));
                for (int i = 0; i < Math.min(3, n.indirectSites.size()); i++) {
                    out.println("    " + n.indirectSites.get(i));
                }
            }
            out.println();

            out.println("== ADDRESS-TAKEN FUNCTIONS NOT IN THE GRAPH (possible callbacks) ==");
            shown = 0;
            for (Map.Entry<Long, String> e : addressTaken.entrySet()) {
                if (nodes.containsKey(e.getKey())) continue;
                if (shown++ >= TOP_ADDRESS_TAKEN) {
                    out.println("... more");
                    break;
                }
                out.println(hex(e.getKey()) + "  taken by " + e.getValue());
            }
            out.println();
            out.println("Note: APIs resolved at runtime (GetProcAddress/COM) and vtable calls are invisible "
                + "to this graph; use the dynload category and the indirect-call list as leads.");
        }

        File csvFile = new File(output.getPath() + ".functions.csv");
        try (PrintWriter out = new PrintWriter(new OutputStreamWriter(
                new FileOutputStream(csvFile), StandardCharsets.UTF_8))) {
            out.println("va,name,depth,parent_va,origin,instructions,callee_count,class,"
                + "direct_imports,trans_stub_categories,indirect_sites,stop");
            for (Node n : nodes.values()) {
                out.println(csv(hex(n.key)) + "," + csv(n.name) + "," + n.depth + ","
                    + csv(n.parent == null ? "" : hex(n.parent.key)) + "," + csv(n.origin) + ","
                    + n.instructions + "," + n.callees.size() + "," + csv(classOf(n)) + ","
                    + csv(String.join("|", n.directImports)) + ","
                    + csv(String.join("|", stubCategories(n))) + ","
                    + csv(String.join(" ; ", n.indirectSites)) + "," + n.stop);
            }
        }
        println("Reachable functions: " + nodes.size() + " -> " + output);
        println("Classes: " + classCount);
        println("CSV: " + csvFile);
    }

    private int countOutside(Set<Long> keys) {
        int c = 0;
        for (long k : keys) if (!nodes.containsKey(k)) c++;
        return c;
    }
}
