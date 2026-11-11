namespace Joey.Lab;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("Joey.Lab — offline capture analysis (.NET 10)");
        Console.WriteLine("This application does not launch Joey or load Frida/the native DLL.");
        return args.Length == 0 || args is ["--help"] ? 0 : 2;
    }
}
