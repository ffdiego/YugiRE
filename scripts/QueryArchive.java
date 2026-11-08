import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.address.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.symbol.*;
import java.io.*;
import java.util.*;

public class QueryArchive extends GhidraScript {
    public void run() throws Exception {
        int transaction=currentProgram.startTransaction("Transient archive query");
        try {
        String[] args=getScriptArgs();
        PrintWriter out=new PrintWriter(new File(args[0]));
        out.println("SHA256: "+currentProgram.getExecutableSHA256());
        Set<Function> selected=new LinkedHashSet<>();
        // Each search is {encoding, literal}; wtext: locates UTF-16LE
        // command-line option strings without changing the Ghidra database.
        List<String[]> searches=new ArrayList<>();
        if(args.length==1) {
            for(String term : Arrays.asList("KCEJYUGI","*.dat"))
                searches.add(new String[]{"ASCII",term});
        }
        for(int i=1;i<args.length;i++) {
            if(args[i].startsWith("text:"))
                searches.add(new String[]{"ASCII",args[i].substring(5)});
            if(args[i].startsWith("wtext:"))
                searches.add(new String[]{"UTF-16LE",args[i].substring(6)});
        }
        for(String[] search : searches) {
            String encoding=search[0], literal=search[1];
            if(literal.isEmpty()) throw new IllegalArgumentException("Empty string query");
            byte[] pattern=literal.getBytes(encoding);
            Address start=currentProgram.getMinAddress();
            while(start!=null) {
                Address hit=currentProgram.getMemory().findBytes(start,currentProgram.getMaxAddress(),pattern,null,true,monitor);
                if(hit==null)break;
                out.println("MATCH "+literal+" @"+hit+
                    (encoding.equals("UTF-16LE")?" [UTF-16LE]":""));
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
            if(args[i].startsWith("text:") || args[i].startsWith("wtext:")) continue;
            if(args[i].startsWith("symbol:")) {
                String name=args[i].substring(7);
                if(name.isEmpty()) throw new IllegalArgumentException("Empty symbol query");
                SymbolIterator symbols=currentProgram.getSymbolTable().getSymbols(name);
                boolean found=false;
                while(symbols.hasNext()) {
                    Symbol symbol=symbols.next();
                    found=true;
                    out.println("SYMBOL "+symbol.getName(true)+" @"+symbol.getAddress());
                    for(Reference ref:getReferencesTo(symbol.getAddress())) {
                        Function owner=getFunctionContaining(ref.getFromAddress());
                        out.println(" XREF "+ref+" FUNCTION "+(owner==null?"none":owner.getEntryPoint()));
                        if(owner!=null) selected.add(owner);
                    }
                }
                if(!found)out.println("NOTE symbol not found: "+name);
                continue;
            }
            // Read memory bytes even when Ghidra has no instruction/data
            // defined at the given address. Unlike decode:, this is strictly
            // observational and does not alter transient disassembly.
            if(args[i].startsWith("bytes:")) {
                String[] fields=args[i].split(":");
                if(fields.length!=3) throw new IllegalArgumentException(
                    "bytes requires address and length: bytes:ADDRESS:LENGTH");
                int size=Integer.parseInt(fields[2]);
                if(size<1 || size>512) throw new IllegalArgumentException(
                    "bytes length must be between 1 and 512");
                Address at=toAddr(fields[1]);
                byte[] buf=new byte[size];
                int read=currentProgram.getMemory().getBytes(at,buf);
                out.print("BYTES "+at+" count="+read+" ");
                for(int n=0;n<read;n++) {
                    out.printf("%02X", buf[n] & 0xff);
                    if(n+1<read)out.print(' ');
                }
                out.println();
                continue;
            }
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
            if(args[i].startsWith("tempfunc:")) {
                Address entry=toAddr(args[i].substring(9));
                Function temporary=getFunctionContaining(entry);
                if(temporary!=null && !temporary.getEntryPoint().equals(entry)) {
                    throw new IllegalArgumentException("Entry lies inside existing function: "+entry);
                }
                if(temporary==null) {
                    disassemble(entry);
                    temporary=createFunction(entry,null);
                }
                if(temporary==null) throw new IllegalStateException("Cannot create transient function: "+entry);
                out.println("TRANSIENT FUNCTION "+entry);
                selected.add(temporary);
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
        } finally {
            currentProgram.endTransaction(transaction,false);
        }
    }
}
