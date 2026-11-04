// Read-only Ghidra script: list all explicit references to the 16-byte
// normal summon placement descriptor. Run after program analysis.
// @category YugiRE
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.Instruction;
import ghidra.program.model.symbol.Reference;
import ghidra.program.model.symbol.ReferenceIterator;
import ghidra.program.model.symbol.RefType;
import java.io.File;
import java.io.FileOutputStream;
import java.io.OutputStreamWriter;
import java.io.PrintWriter;
import java.nio.charset.StandardCharsets;
import java.util.LinkedHashMap;
import java.util.Map;

public class SummonDescriptorXrefs extends GhidraScript {
    private static final String EXPECTED_SHA =
        "db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de";
    private static final long BASE = 0x00A55080L;
    private static final int LENGTH = 0x10; // includes +8C flags and +8E stage

    private static String csv(String value) {
        return "\"" + (value == null ? "" : value.replace("\"", "\"\"")
                                                   .replace("\r", " ")
                                                   .replace("\n", " ")) + "\"";
    }

    @Override
    public void run() throws Exception {
        String sha = currentProgram.getExecutableSHA256();
        if (!EXPECTED_SHA.equalsIgnoreCase(sha)) {
            throw new IllegalStateException("Joey executable fingerprint differs: " + sha);
        }
        String[] args = getScriptArgs();
        File output = (args != null && args.length > 0)
            ? new File(args[0]) : askFile("Export summon descriptor references", "Save");
        if (output == null) return;
        File dir = output.getAbsoluteFile().getParentFile();
        if (dir != null && !dir.isDirectory() && !dir.mkdirs()) {
            throw new IllegalStateException("Cannot create output directory: " + dir);
        }
        Map<String, String> rows = new LinkedHashMap<>();
        int readRefs = 0, writeRefs = 0, otherRefs = 0;
        for (int n = 0; n < LENGTH && !monitor.isCancelled(); n++) {
            Address target = toAddr(BASE + n);
            ReferenceIterator it = currentProgram.getReferenceManager().getReferencesTo(target);
            while (it.hasNext()) {
                Reference ref = it.next();
                Address from = ref.getFromAddress();
                RefType kind = ref.getReferenceType();
                if (kind.isWrite()) writeRefs++;
                else if (kind.isRead()) readRefs++;
                else otherRefs++;
                Function owner = getFunctionContaining(from);
                Instruction ins = currentProgram.getListing().getInstructionContaining(from);
                String key = from.toString() + ":" + target.toString() +
                    ":" + ref.getOperandIndex();
                String row = csv(target.toString()) + "," + csv(from.toString()) + "," +
                    csv(kind.toString()) + "," + kind.isRead() + "," + kind.isWrite() + "," +
                    csv(owner == null ? "" : owner.getEntryPoint().toString()) + "," +
                    csv(owner == null ? "" : owner.getName()) + "," +
                    csv(ins == null ? "" : ins.toString());
                rows.put(key, row);
            }
        }
        try (PrintWriter out = new PrintWriter(new OutputStreamWriter(
                new FileOutputStream(output), StandardCharsets.UTF_8))) {
            out.println("to,from,reference_type,is_read,is_write,function_va,function_name,instruction");
            for (String row : rows.values()) out.println(row);
        }
        println("Descriptor references: " + rows.size() + " to " + output);
        println("Write: " + writeRefs + ", read: " + readRefs + ", other: " + otherRefs);
        println("Indirect computed stores may have no explicit xref; inspect writers manually.");
    }
}
