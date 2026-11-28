using Joey.Core;

namespace Joey.Tools;

public static class ToolCli
{
    private const int MaxResourceBytes = 256 * 1024 * 1024;

    private const string Usage = """
        Joey.Tools — offline utilities for YugiRE
        Usage:
          dat list <archive.dat> [--name NAME] [--folder PATH] [--ext EXT] [--match TEXT]
          dat extract <archive.dat> (--all | filters) [--output DIR]

        Filters (combined with AND; case insensitive):
          --name NAME    Exact basename or full internal path
          --folder PATH  Recursive internal directory (including subdirectories)
          --ext EXT      Extension with or without leading dot, e.g. bmp
          --match TEXT   Substring of normalized internal path
          --all          Explicitly select all entries for extraction
          --output DIR   Default: scratch/dat-extracted/<archive stem>

        Paths inside DAT accept / or \. Existing files are never overwritten.
        List without filters lists everything. Extraction preserves archive paths
        and formats; compressed entries are decoded using Joey.Core.Archive.
        """;

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length == 0 || args is ["--help"] or ["-h"] or ["help"] or ["dat", "--help"])
        {
            output.WriteLine(Usage);
            return 0;
        }
        try
        {
            if (args.Length < 3 || args[0] != "dat" || args[1] is not ("list" or "extract"))
                throw new ArgumentException("Expected dat list or dat extract followed by the DAT path.");

            bool extract = args[1] == "extract";
            var options = Parse(args[3..], extract);
            using var archive = new Archive(args[2]);
            var selected = archive.Entries
                .Where(e => Matches(e.Name, options))
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToArray();
            if (selected.Length == 0)
            {
                error.WriteLine("No entries match the selection.");
                return 1;
            }
            if (!extract)
            {
                foreach (var entry in selected)
                    output.WriteLine($"{entry.Size,10} {entry.Stored,10} {Normalize(entry.Name)}");
                output.WriteLine($"{selected.Length} entries (decoded bytes, stored bytes, path).");
                return 0;
            }

            string root = Path.GetFullPath(options.Output ??
                Path.Combine("scratch", "dat-extracted", Path.GetFileNameWithoutExtension(args[2])));
            // Preflight all targets before writing. Reject traversal, aliases and collisions.
            var destinations = selected.Select(e => Destination(root, e.Name)).ToArray();
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in destinations)
                if (!paths.Add(path))
                    throw new IOException($"Multiple entries resolve to {path}");
            foreach (string path in destinations)
            {
                for (string? parent = Path.GetDirectoryName(path); parent is not null;
                     parent = Path.GetDirectoryName(parent))
                {
                    if (paths.Contains(parent))
                        throw new IOException($"File/directory collision at {parent}");
                    RejectLink(parent);
                    if (File.Exists(parent))
                        throw new IOException($"Parent is a file: {parent}");
                }
                RejectLink(path);
                if (File.Exists(path) || Directory.Exists(path))
                    throw new IOException($"Destination already exists: {path}");
            }

            for (int i = 0; i < selected.Length; i++)
            {
                var entry = selected[i];
                byte[] bytes = archive.Read(entry.Name, MaxResourceBytes);
                string path = destinations[i];
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                    stream.Write(bytes);
                output.WriteLine($"{bytes.Length,10} {path}");
            }
            output.WriteLine($"Extracted {selected.Length} entries to {root}");
            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or InvalidDataException
                                   or UnauthorizedAccessException or NotSupportedException)
        {
            error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private sealed record Options(string? Name, string? Folder, string? Extension,
                                  string? Match, string? Output);

    private static Options Parse(string[] args, bool extract)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        bool all = false;
        for (int i = 0; i < args.Length; i++)
        {
            string flag = args[i];
            if (flag == "--all")
            {
                if (!extract || all) throw new ArgumentException("Invalid or repeated --all.");
                all = true;
                continue;
            }
            if (flag is not ("--name" or "--folder" or "--ext" or "--match" or "--output") ||
                (!extract && flag == "--output"))
                throw new ArgumentException($"Unknown option: {flag}");
            if (i + 1 == args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(args[i + 1]))
                throw new ArgumentException($"Missing value for {flag}");
            if (!values.TryAdd(flag, args[++i]))
                throw new ArgumentException($"Repeated option: {flag}");
        }

        string? Get(string flag) => values.GetValueOrDefault(flag);
        string? name = Get("--name"), folder = Get("--folder"), ext = Get("--ext"), match = Get("--match");
        bool filtered = name is not null || folder is not null || ext is not null || match is not null;
        if (extract && all == filtered)
            throw new ArgumentException("Extraction requires either --all or at least one filter, not both.");
        if (folder is not null)
        {
            folder = Normalize(folder).TrimEnd('/');
            if (folder.Length == 0)
                throw new ArgumentException("Folder cannot be empty; use --all for the whole archive.");
        }
        if (ext is not null)
        {
            ext = ext.TrimStart('.');
            if (ext.Length == 0 || ext.Contains('/') || ext.Contains('\\') || ext.Contains('.'))
                throw new ArgumentException("Expected a single extension, e.g. bmp.");
            ext = "." + ext;
        }
        return new(name is null ? null : Normalize(name), folder, ext,
                   match is null ? null : Normalize(match), Get("--output"));
    }

    private static string Normalize(string path) => path.Replace('\\', '/');
    private static bool Matches(string name, Options options)
    {
        string path = Normalize(name);
        const StringComparison comparison = StringComparison.OrdinalIgnoreCase;
        return (options.Name is null || (options.Name.Contains('/')
                    ? path.Equals(options.Name, comparison)
                    : path[(path.LastIndexOf('/') + 1)..].Equals(options.Name, comparison))) &&
               (options.Folder is null || path.StartsWith(options.Folder + "/", comparison)) &&
               (options.Extension is null || Path.GetExtension(path).Equals(options.Extension, comparison)) &&
               (options.Match is null || path.Contains(options.Match, comparison));
    }

    private static string Destination(string root, string name)
    {
        string[] parts = Normalize(name).Split('/');
        foreach (string part in parts)
        {
            if (part.Length == 0 || part is "." or ".." || part.EndsWith('.') || part.EndsWith(' ') ||
                part.Any(c => char.IsControl(c) || "<>:\"|?*".Contains(c)))
                throw new InvalidDataException($"Unsafe archive path: {name}");
            string stem = part.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
            if (stem is "CON" or "PRN" or "AUX" or "NUL" ||
                (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) &&
                 stem[3] is >= '0' and <= '9'))
                throw new InvalidDataException($"Reserved device name: {name}");
        }
        string path = Path.GetFullPath(Path.Combine(root, Path.Combine(parts)));
        string prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, OperatingSystem.IsWindows() ?
                StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException($"Unsafe archive path: {name}");
        return path;
    }

    private static void RejectLink(string path)
    {
        // File.GetAttributes also sees existing symlinks whose targets are missing.
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Symbolic link or reparse point in output path: {path}");
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
    }
}
