// Read-only native vtable extraction. No disassembly/patching or Ghidra database edits.
// @category YugiRE
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.mem.Memory;
import ghidra.program.model.mem.MemoryBlock;
import java.io.File;
import java.io.FileOutputStream;
import java.io.OutputStreamWriter;
import java.io.PrintWriter;
import java.nio.charset.StandardCharsets;
import java.util.Arrays;
import java.util.LinkedHashSet;
import java.util.Locale;
import java.util.Set;

public class ExportVisualVtables extends GhidraScript {
    private static final String SHA = "db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de";
    private static final String[] DEFAULTS = {"005DABD8", "005DACC4", "005DABA8", "005DB374", "005DA44C", "005DB3C4", "005DB3D4"};
    private static String hex(long value) { return String.format(Locale.ROOT,"%08X",value & 0xffffffffL); }

    public void run() throws Exception {
        String found = currentProgram.getExecutableSHA256();
        if (!SHA.equalsIgnoreCase(found)) throw new IllegalStateException("Executable SHA mismatch: " + found);
        if (currentProgram.getLanguage().isBigEndian()) throw new IllegalStateException("Expected little-endian executable");
        String[] args = getScriptArgs();
        if (args.length == 0) throw new IllegalArgumentException("Usage: OUTPUT.tsv [VTABLE_VA ...]");
        Set<String> tables = new LinkedHashSet<>();
        if (args.length == 1) tables.addAll(Arrays.asList(DEFAULTS));
        for (int i = 1; i < args.length; i++) {
            if (!args[i].matches("(?i)[0-9a-f]{8}")) throw new IllegalArgumentException("Invalid address: " + args[i]);
            tables.add(args[i].toUpperCase(Locale.ROOT));
        }
        if (tables.size() > 128) throw new IllegalArgumentException("Too many tables");
        Memory mem = currentProgram.getMemory();
        File output = new File(args[0]);
        File parent = output.getAbsoluteFile().getParentFile();
        if (!parent.isDirectory() && !parent.mkdirs()) throw new IllegalStateException("Cannot create " + parent);
        try (PrintWriter out = new PrintWriter(new OutputStreamWriter(new FileOutputStream(output),StandardCharsets.UTF_8))) {
            out.println("# schema=joey-visual-vtables-v1");
            out.println("# executable_sha256=" + SHA);
            out.println("# pointer_size=4");
            out.println("vtable_address\tslot_index\tslot_address\ttarget_address\tfunction_address\tresolution\texecutable");
            for (String table : tables) {
                monitor.checkCancelled();
                for (int index = 0; index < 4; index++) {
                    long slot = Long.parseLong(table,16) + index * 4L;
                    Address location = toAddr(hex(slot));
                    String target = "", owner = "", resolution = "unreadable", executable = "";
                    try {
                        byte[] bytes = new byte[4];
                        if (mem.getBytes(location,bytes) != 4) throw new IllegalStateException("Short read " + location);
                        long pointer = (bytes[0] & 255L) | ((bytes[1] & 255L) << 8)
                            | ((bytes[2] & 255L) << 16) | ((bytes[3] & 255L) << 24);
                        target = hex(pointer);
                        if (pointer == 0L) { resolution="null"; executable="false"; }
                        else {
                            Address at = toAddr(target);
                            MemoryBlock block = mem.getBlock(at);
                            boolean isExecutable = block != null && block.isExecute();
                            executable = Boolean.toString(isExecutable);
                            Function fn = getFunctionAt(at);
                            if (fn != null) { owner=hex(fn.getEntryPoint().getOffset()); resolution="function_entry"; }
                            else {
                                fn = getFunctionContaining(at);
                                if (fn != null) { owner=hex(fn.getEntryPoint().getOffset()); resolution="inside_function"; }
                                else resolution = block == null ? "unmapped" :
                                    (isExecutable ? "executable_unknown" : "nonexecutable");
                            }
                        }
                    } catch (Exception e) { target=""; owner=""; resolution="unreadable"; executable=""; }
                    out.println(table + "\t" + index + "\t" + hex(slot) + "\t" + target + "\t" + owner + "\t" + resolution + "\t" + executable);
                }
            }
            if (out.checkError()) throw new IllegalStateException("Output write failed");
        }
        println("Visual vtables saved to " + output.getAbsolutePath());
    }
}
