import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.symbol.SourceType;
import java.io.File;
import java.io.PrintWriter;

// Explicitly persistent annotation step. Run with -noanalysis, without -readOnly.
// Only names/comments are changed; target bytes and existing custom primary names survive.
public class RenameCpuDeckSelection extends GhidraScript {
    public void run() throws Exception {
        String hash = currentProgram.getExecutableSHA256();
        if (!"db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de".equalsIgnoreCase(hash))
            throw new IllegalArgumentException("Unsupported original fingerprint");
        String[][] names = {
            {"0044bbe0", "SelectCpuDeckResourcePath", "CPU/FUL resource table lookup from A54DB3/DB4; uses shared MT, not CRT shuffle RNG."},
            {"0044a920", "LoadDeckResourceIntoSource", "Type0 calls player parser; type1 calls CPU parser; resource path already selected upstream."},
            {"0044a690", "ReadCpuSourceDeck", "Read YDC into A54F42; filter additional lists via 005BE190."},
            {"0055c140", "NextUiDeckMtUInt32", "624-word MT twist/tempering; shared object normally at 005F25E8."},
            {"0055c0e0", "SeedUiDeckMtLegacy", "69069 LCG twice per word, 624 words; lazy fallback seed 4357."},
            {"0040f150", "InitializeUiDeckMtObject", "Set vtable and index625 for lazy seeding; does not consume a draw."},
            {"0040f180", "ReadUiDeckMtSnapshot", "Deserialize 624 words and index from stream; indirect consumer boundary remains under investigation."},
            {"005bedd0", "ProfileHasPriorEditionCards", "Owned card in accumulated edition mask but outside current edition mask."},
            {"005bee30", "InitializeDifficultyProfile", "Initialize DB1..DB4 when DB1==0, preserving observed assignment order."},
            {"00446df0", "ApplyProfileDuelResult", "Profile statistics/streak/difficulty update; mode3, flag40 and match-result gates."},
            {"0044baf0", "UpdateMatchResultSummary", "Scene0/3 single result; scene1 up to three duel results with signed win/loss difference."},
            {"005bbf60", "GetNativeTerminalResultCode", "Return byte A577FA >>6; raw result code0..3."}
        };
        int transaction = currentProgram.startTransaction("CPU deck research names");
        boolean success = false;
        try (PrintWriter out = new PrintWriter(new File(getScriptArgs()[0]))) {
            out.println("SHA256: " + hash);
            out.println("Scope: persistent function names/comments only, no byte patch or autoanalysis.");
            for (String[] item : names) {
                Address at = toAddr(item[0]);
                Function function = getFunctionAt(at);
                if (function == null) {
                    if (getFunctionContaining(at) != null)
                        throw new IllegalArgumentException("Entry lies in another function: " + at);
                    function = createFunction(at, null);
                }
                if (function == null) throw new IllegalStateException("Cannot define function " + at);
                String desired = item[1] + "_" + item[0].toUpperCase();
                String old = function.getName();
                if (old.startsWith("FUN_") || old.equals(desired)) {
                    function.setName(desired, SourceType.USER_DEFINED);
                    out.println("FUNCTION " + at + " " + old + " -> " + desired);
                } else {
                    createLabel(at, desired, false, SourceType.USER_DEFINED);
                    out.println("ALIAS " + at + " " + desired + "; preserved primary " + old);
                }
                String note = "CPU deck research: " + item[2] + " See docs/research/cpu-deck-selection.md; static evidence only.";
                String existing = function.getComment();
                if (existing == null || !existing.contains(note))
                    function.setComment(existing == null ? note : existing + "\n" + note);
            }
            out.println("Annotated " + names.length + " verified function entries.");
            success = true;
        } finally {
            currentProgram.endTransaction(transaction, success);
        }
    }
}
