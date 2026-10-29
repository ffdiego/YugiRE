import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.symbol.*;
import java.io.*;
import java.util.*;

public class InitialRecon extends GhidraScript {
    public void run() throws Exception {
        PrintWriter out = new PrintWriter(new File(getScriptArgs()[0]));
        out.println("Program: " + currentProgram.getName());
        out.println("SHA256: " + currentProgram.getExecutableSHA256());
        out.println("Language: " + currentProgram.getLanguageID());
        out.println("Compiler spec: " + currentProgram.getCompilerSpec().getCompilerSpecID());
        out.println("Functions: " + currentProgram.getFunctionManager().getFunctionCount());
        Set<Function> selected = new LinkedHashSet<>();
        Function entry = getFunctionAt(toAddr(0x5c5e58L));
        if (entry != null) selected.add(entry);
        DataIterator data = currentProgram.getListing().getDefinedData(true);
        while (data.hasNext() && !monitor.isCancelled()) {
            Data d = data.next();
            if (!(d.getValue() instanceof String)) continue;
            String s = (String)d.getValue();
            if (!s.toLowerCase().matches(".*(data\\.dat|voice\\.dat|deck\\.ydc|region\\.dat|ddraw|dsound|dinput|directdraw|directsound).*")) continue;
            out.println("STRING " + d.getAddress() + " " + s);
            ReferenceIterator refs = currentProgram.getReferenceManager().getReferencesTo(d.getAddress());
            int n=0;
            while (refs.hasNext() && n++ < 20) {
                Reference r = refs.next();
                Function f = getFunctionContaining(r.getFromAddress());
                out.println(" XREF " + r.getFromAddress() + " " + (f == null ? "no function" : f.getName()+" @"+f.getEntryPoint()));
                if (f != null && selected.size() < 7 && s.toLowerCase().contains(".dat")) selected.add(f);
            }
        }
        DecompInterface decomp = new DecompInterface();
        decomp.openProgram(currentProgram);
        for (Function f : selected) {
            out.println("FUNCTION " + f.getName()+" @"+f.getEntryPoint());
            DecompileResults result = decomp.decompileFunction(f, 30, monitor);
            out.println(result.decompileCompleted() ? result.getDecompiledFunction().getC() : result.getErrorMessage());
        }
        decomp.dispose();
        out.close();
    }
}
