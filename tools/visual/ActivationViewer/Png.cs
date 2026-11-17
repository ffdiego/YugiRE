using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
namespace ActivationViewer;
public static class Png
{
    public static void Write(string path,int width,int height,byte[] pixels)
    {
        if(width<1 || height<1 || pixels.Length!=(long)width*height*4) throw new InvalidDataException("PNG bounds");
        using var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write);
        output.Write(new byte[]{137,80,78,71,13,10,26,10});
        byte[] header=new byte[13]; BinaryPrimitives.WriteInt32BigEndian(header,width); BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4),height);
        header[8]=8;header[9]=6;Chunk(output,"IHDR",header);
        using var memory=new MemoryStream();
        using(var z=new ZLibStream(memory,CompressionLevel.Optimal,true))
            for(int y=0;y<height;y++) { z.WriteByte(0);z.Write(pixels.AsSpan(y*width*4,width*4)); }
        Chunk(output,"IDAT",memory.ToArray());Chunk(output,"IEND",[]);
    }
    private static void Chunk(Stream output,string kind,byte[] data)
    {
        Span<byte> word=stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(word,data.Length);output.Write(word);
        byte[] type=Encoding.ASCII.GetBytes(kind);output.Write(type);output.Write(data);
        uint crc=0xffffffff;
        foreach(byte b in type.Concat(data)) { crc^=b;for(int n=0;n<8;n++) crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0); }
        BinaryPrimitives.WriteUInt32BigEndian(word,~crc);output.Write(word);
    }
    public static string ExportDirectory(string path)
    {
        // Locate this source checkout from cwd, then enforce its ignored scratch directory.
        var current=new DirectoryInfo(Environment.CurrentDirectory);
        while(current!=null && !File.Exists(Path.Combine(current.FullName,"AGENTS.md"))) current=current.Parent;
        if(current==null) throw new InvalidOperationException("Run from this repository checkout");
        string scratchRoot=Path.GetFullPath(Path.Combine(current.FullName,"scratch"));
        if(Directory.Exists(scratchRoot) && (File.GetAttributes(scratchRoot)&FileAttributes.ReparsePoint)!=0) throw new InvalidOperationException("Linked scratch directory refused");
        string root=scratchRoot+Path.DirectorySeparatorChar;
        string target=Path.GetFullPath(path);
        var comparison=OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
        if(!target.StartsWith(root,comparison)) throw new InvalidOperationException("Exports must be inside ignored scratch/");
        for(var dir=new DirectoryInfo(target);dir!=null && dir.FullName.StartsWith(root,comparison);dir=dir.Parent)
            if(dir.Exists && (dir.Attributes&FileAttributes.ReparsePoint)!=0) throw new InvalidOperationException("Export through link refused");
        Directory.CreateDirectory(target);return target;
    }
}
