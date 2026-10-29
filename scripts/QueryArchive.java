import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.address.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.symbol.*;
import java.io.*;
import java.util.*;

public class QueryArchive extends GhidraScript {
    public void run() throws Exception {
        String[] args=getScriptArgs();
        PrintWriter out=new PrintWriter(new File(args[0]));
        out.println("SHA256: "+currentProgram.getExecutableSHA256());
        Set<Function> selected=new LinkedHashSet<>();
        List<String> searches=new ArrayList<>();
        if(args.length==1) searches.addAll(Arrays.asList("KCEJYUGI","*.dat"));
        for(int i=1;i<args.length;i++) if(args[i].startsWith("text:")) searches.add(args[i].substring(5));
        for(String text : searches) {
            Address start=currentProgram.getMinAddress();
            while(start!=null) {
                Address hit=currentProgram.getMemory().findBytes(start,currentProgram.getMaxAddress(),text.getBytes("US-ASCII"),null,true,monitor);
                if(hit==null)break;
                out.println("MATCH "+text+" @"+hit);
                ReferenceIterator refs=currentProgram.getReferenceManager().getReferencesTo(hit);
                while(refs.hasNext()) {
                    Reference ref=refs.next(); Function f=getFunctionContaining(ref.getFromAddress());
                    out.println(" XREF "+ref+" FUNCTION "+(f==null?"none":f.getEntryPoint()));
                    if(f!=null)selected.add(f);
                }
                start=hit.next();
            }
        }
        for(int i=1;i<args.length;i++) {
            if(args[i].startsWith("text:"))continue;
            if(args[i].startsWith("ins:") || args[i].startsWith("decode:")) {
                String[] fields=args[i].split(":");
                if(fields[0].equals("decode")) {
                    out.println("TRANSIENT DISASSEMBLY "+fields[1]);
                    disassemble(toAddr(fields[1]));
                }
                Instruction instruction=currentProgram.getListing().getInstructionContaining(toAddr(fields[1]));
                if(instruction==null) {
                    out.println("NOTE no instruction at "+fields[1]+"; starting at next defined instruction");
                    instruction=currentProgram.getListing().getInstructionAfter(toAddr(fields[1]));
                }
                int limit=Math.min(Integer.parseInt(fields[2]),100);
                for(int n=0;instruction!=null && n<limit;n++) {
                    out.println("INS "+instruction.getAddress()+" "+instruction);
                    instruction=instruction.getNext();
                }
                continue;
            }
            if(args[i].startsWith("ref:") || args[i].startsWith("xref:")) {
                boolean selectOwners=args[i].startsWith("ref:");
                Address address=toAddr(args[i].substring(selectOwners?4:5));
                out.println("REFERENCES TO "+address);
                for(Reference ref:getReferencesTo(address)) {
                    Function owner=getFunctionContaining(ref.getFromAddress());
                    out.println(" XREF "+ref+" FUNCTION "+(owner==null?"none":owner.getEntryPoint()));
                    if(selectOwners && owner!=null)selected.add(owner);
                }
                continue;
            }
            Function f=getFunctionContaining(toAddr(args[i]));
            if(f!=null)selected.add(f);
        }
        DecompInterface decomp=new DecompInterface();decomp.openProgram(currentProgram);
        int count=0;
        for(Function f:selected) {
            if(count++>=12)break;
            out.println("FUNCTION "+f.getName()+" @"+f.getEntryPoint());
            for(Reference ref:getReferencesTo(f.getEntryPoint()))out.println(" CALLER "+ref.getFromAddress()+" "+getFunctionContaining(ref.getFromAddress()));
            DecompileResults result=decomp.decompileFunction(f,30,monitor);
            out.println(result.decompileCompleted()?result.getDecompiledFunction().getC():result.getErrorMessage());
        }
        decomp.dispose();out.close();
    }
}
