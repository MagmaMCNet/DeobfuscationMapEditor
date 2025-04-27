using System.Text;
using System.Text.RegularExpressions;
using Ionic.Zlib;
using Spectre.Console;

namespace DME;
public static class DME
{
    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        Console.Title = "DME - Deobfuscation Map Editor";

        var csvPath = GetPath() ?? Input.Ask("[cyan]Deobfuscation Map file[/]:").Replace("\"", "").Trim();
        if (string.IsNullOrWhiteSpace(csvPath)) return;

        var isGzipped = csvPath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);
        var map = MapService.Load(csvPath, ref isGzipped);

        Console.Clear();
        DisplayService.Header();
        DisplayService.Stats(map, csvPath, isGzipped);

        while (true)
        {
            var option = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .AddChoices(
                        "🔍 Search", "➕ Add", "✏️ Update", "🗑 Remove", "🧹 Clean Data", "🔄 Reload",
                        isGzipped ? "🗜 Decompress" : "🗜 Compress", "🚪 Exit"
                    )
                    .WrapAround(true)
            );
            Console.WriteLine();

            if (option.Contains("Exit")) break;

            Handler.Handle(option, ref map, ref csvPath, ref isGzipped);
        }
    }
    public static string? GetPath()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && File.Exists(Path.GetFullPath(args[1]))) return Path.GetFullPath(args[1]);
        return null;
    }
}

static class Input
{
    public static string Ask(string prompt)
    {
        var cts = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        Console.CancelKeyPress += handler;

        var result = "";
        try
        {
            var task = Task.Run(() => AnsiConsole.AskAsync<string>(prompt, cts.Token));
            result = task.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            Console.Write("\r" + new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, Console.CursorTop-1);
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
        return result;
    }
}

static class MapService
{
    public static bool IsGzipped
    {
        get; set;
    }
    public static Dictionary<string, string> Load(string path, ref bool isGzipped)
    {
        IsGzipped = isGzipped;
        var map = new Dictionary<string, string>();
        try
        {
            string[] lines;
            if (IsGzipped)
            {
                using var fs = File.OpenRead(path);
                using var gz = new GZipStream(fs, CompressionMode.Decompress);
                using var rdr = new StreamReader(gz);
                lines = rdr.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            }
            else
            {
                lines = File.ReadAllLines(path);
            }

            foreach (var line in lines)
            {
                var parts = line.Trim().Split(';');
                if (parts.Length >= 2) map[parts[0]] = parts[1];
            }
        }
        catch
        {
            AnsiConsole.MarkupLine("[red]Failed to load map.[/]");
        }
        return map;
    }

    public static void Save(string path, Dictionary<string, string> map)
    {
        static int ExtractNumber(string input)
        {
            var match = Regex.Match(input, @"_(\d+)\b");
            return match.Success ? int.Parse(match.Groups[1].Value) : int.MaxValue;
        }

        var normal = map
            .Where(kvp => !kvp.Key.Contains("::"))
            .OrderBy(kvp => kvp.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var special = map
            .Where(kvp => kvp.Key.Contains("::"))
            .OrderBy(kvp => {
                var parts = kvp.Key.Split(new[] { "::" }, StringSplitOptions.None);
                var name = parts[0];
                return (name, number: ExtractNumber(name));
            })
            .ThenBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var lines = new List<string>();

        if (normal.Count > 0)
        {
            lines.Add("## Classes");
            lines.AddRange(normal.Select(kvp =>
            {
                var key = kvp.Key;
                if (!key.Contains("."))
                    key = "." + key;
                return $"{key};{kvp.Value}";
            }));
        }

        if (special.Count > 0)
        {
            lines.Add("");
            lines.Add("## Types");
            lines.AddRange(special.Select(kvp =>
            {
                var key = kvp.Key;
                if (!key.Contains("."))
                    key = "." + key;
                return $"{key};{kvp.Value}";
            }));
        }

        var csv = string.Join(Environment.NewLine, lines);

        if (IsGzipped)
        {
            using var fs = File.Create(path);
            using var gz = new GZipStream(fs, CompressionMode.Compress, CompressionLevel.BestCompression, false);
            using var wr = new StreamWriter(gz, new UTF8Encoding(false));
            wr.Write(csv);
        }
        else
        {
            File.WriteAllText(path, csv, new UTF8Encoding(false));
        }
    }

    static string SortKey(string name)
    {
        var parts = name.Split('.');
        return parts.Length >= 2 ? string.Join(".", parts.Take(2)) : name;
    }
}

static class DisplayService
{
    public static void Header()
    {
        AnsiConsole.Write(
            new Rule("[bold #FF4500]M[/][bold #FF6347]a[/][bold #FF7F50]g[/][bold #FF8C00]m[/][bold #FFA500]a's[/] [orangered1]Deobfuscation Map Editor[/]")
                .RuleStyle(Color.Red3)
                .Centered()
        );
    }
    public static void Stats(Dictionary<string, string> map, string path, bool isGzipped)
    {
        int distinct = map.Values.Distinct().Count();
        var info = new FileInfo(path);
        var last = info.Exists ? info.LastWriteTime : DateTime.MinValue;
        AnsiConsole.MarkupLine($"[bold green]File Statistics:[/]");
        AnsiConsole.MarkupLine($"[bold yellow] - Distinct Entries[/]: [Orange1]{distinct}[/]");
        AnsiConsole.MarkupLine($"[bold yellow] - Total Entries[/]: [Orange1]{map.Count}[/]");
        AnsiConsole.MarkupLine($"[bold yellow] - Last Edited[/]: [Orange1]{last}[/]");
        AnsiConsole.MarkupLine($"[bold yellow] - File Size[/]: [Orange1]{info.Length / 1024}[/] [orange3]KB[/]");
        AnsiConsole.MarkupLine($"[bold yellow] - Compressed[/]: {(isGzipped ? "[Orange1]true[/]" : "[Lime]false[/]")}");
        AnsiConsole.MarkupLine("[Orange1]---------------------------[/]");
    }
}

static class Handler
{
    public static void Handle(string option, ref Dictionary<string, string> map, ref string path, ref bool isGzipped)
    {
        if (option.Contains("Reload", StringComparison.InvariantCultureIgnoreCase)) Reload(ref map, path, isGzipped);
        else if (option.Contains("Compress", StringComparison.InvariantCultureIgnoreCase)) ToggleCompression(ref path, ref isGzipped, map);
        else if (option.Contains("Search", StringComparison.InvariantCultureIgnoreCase)) Search(map);
        else if (option.Contains("Add", StringComparison.InvariantCultureIgnoreCase)) Add(map, path);
        else if (option.Contains("Update", StringComparison.InvariantCultureIgnoreCase)) Update(map, path);
        else if (option.Contains("Clean", StringComparison.InvariantCultureIgnoreCase)) Clean(ref map, path);
        else if (option.Contains("Remove", StringComparison.InvariantCultureIgnoreCase)) Remove(map, path);
    }
    static void Reload(ref Dictionary<string, string> map, string path, bool isGzipped)
    {
        MapService.Save(path, map);
        map = MapService.Load(path, ref isGzipped);
        Console.Clear();
        DisplayService.Header();
        DisplayService.Stats(map, path, isGzipped);
    }
    static void ToggleCompression(ref string path, ref bool isGzipped, Dictionary<string, string> map)
    {
        isGzipped = !isGzipped;


        if (path.EndsWith(".gz") && !isGzipped)
            path = path.Substring(0, path.Length - 3);
        else if (!path.EndsWith(".gz") && isGzipped)
            path += ".gz";
        MapService.IsGzipped = isGzipped;
        MapService.Save(path, map);
        Thread.Sleep(250);
        AnsiConsole.MarkupLine(isGzipped
            ? "[bold green]✔ Compressed and saved.[/]"
            : "[bold green]✔ Decompressed and saved.[/]");
        Thread.Sleep(350);
        Console.Clear();
        DisplayService.Header();
        DisplayService.Stats(map, path, isGzipped);
    }
    static void Search(Dictionary<string, string> map)
    {
        var term = Input.Ask("[cyan]Enter name[/] ([yellow]identifier[/] or [green]obfuscated[/]):");
        if (string.IsNullOrEmpty(term)) return;

        var results = map.Where(kvp => kvp.Key.Contains(term, StringComparison.OrdinalIgnoreCase)
                                    || kvp.Value.Contains(term, StringComparison.OrdinalIgnoreCase))
                         .ToList();
        if (!results.Any()) AnsiConsole.MarkupLine("[red]No matches found.[/]");
        else
        {
            Console.CursorTop -= 1;
            var tbl = new Table().Title("[bold yellow]Search Results[/]")
                .AddColumn("[bold yellow]Obfuscated[/]")
                .AddColumn("[bold cyan]Identifier[/]")
                .BorderColor(Color.Green)
                .Border(TableBorder.Rounded);
            foreach (var kv in results) tbl.AddRow(kv.Key, kv.Value);
            AnsiConsole.Write(tbl);
        }
    }
    static void Add(Dictionary<string, string> map, string path)
    {
        var obf = Input.Ask("[cyan]Enter Obfuscated name[/]:");
        if (string.IsNullOrEmpty(obf)) return;
        var real = Input.Ask("[cyan]Enter Identifier name[/]:");
        if (string.IsNullOrEmpty(real)) return;

        if (obf.Contains("::") || obf.Contains("."))
            map[obf] = real;
        else
            map["."+obf] = real;
        MapService.Save(path, map);
        AnsiConsole.MarkupLine("[bold green]✔ Added and saved.[/]");
    }
    static void Update(Dictionary<string, string> map, string path)
    {
        var target = Input.Ask("Enter the identifier you want to update:");
        if (string.IsNullOrEmpty(target)) return;

        var matches = map.Where(kvp => kvp.Value.Equals(target, StringComparison.Ordinal))
                          .ToList();
        if (!matches.Any()) { AnsiConsole.MarkupLine("[red]No obfuscated names found for this identifier.[/"); return; }

        var tbl = new Table().AddColumn("[bold yellow]Obfuscated[/]")
                             .AddColumn("[bold cyan]Identifier[/]")
                             .BorderColor(Color.Green)
                             .Border(TableBorder.Rounded);
        foreach (var kv in matches) tbl.AddRow(kv.Key, kv.Value);
        AnsiConsole.Write(tbl);

        var newName = Input.Ask("[cyan]Enter the new identifier[/]:");
        if (string.IsNullOrEmpty(newName)) return;

        foreach (var kv in matches) map[kv.Key] = newName;
        MapService.Save(path, map);
        AnsiConsole.MarkupLine("[green]identifier updated for all matching obfuscated names and saved.[/]");
    }
    static void Clean(ref Dictionary<string, string> map, string path)
    {
        var invalid = map.Where(kvp => !Identifier.IsValid(kvp.Value)).ToList();
        if (!invalid.Any()) { AnsiConsole.MarkupLine("[bold green]No invalid entries found.[/]"); return; }

        var tbl = new Table().Title("[bold red]Invalid Entries[/]")
            .AddColumn("[bold yellow]Obfuscated[/]")
            .AddColumn("[bold red]identifier Name[/]")
            .BorderColor(Color.Red)
            .Border(TableBorder.Rounded);
        foreach (var kv in invalid) tbl.AddRow(kv.Key, kv.Value);
        AnsiConsole.Write(tbl);

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[red]Do you want to remove these invalid entries?[/]")
                .AddChoices("Yes", "No (Keep them)")
        );
        if (choice.StartsWith("Yes"))
        {
            foreach (var kv in invalid) map.Remove(kv.Key);
            MapService.Save(path, map);
            AnsiConsole.MarkupLine($"[bold green]{invalid.Count} invalid entries removed and file updated.[/]");
        }
        else AnsiConsole.MarkupLine("[yellow]Keeping all entries.[/]");
    }
    static void Remove(Dictionary<string, string> map, string path)
    {
        var obf = Input.Ask("[red]Enter Obfuscated name to remove[/]:");
        if (string.IsNullOrEmpty(obf)) return;

        if (map.Remove(obf))
        {
            MapService.Save(path, map);
            AnsiConsole.MarkupLine("[bold green]✔ Entry removed.[/]");
        }
        else AnsiConsole.MarkupLine("[yellow]No such key found.[/]");
    }
}

static class Identifier
{
    public static bool IsValid(string name)
    {
        var parts = name.Split('.');
        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part)) return false;
            if (!char.IsLetter(part[0]) && part[0] != '_') return false;
            for (int i = 1; i < part.Length; i++)
            {
                if (!char.IsLetterOrDigit(part[i]) && part[i] != '_' && part[i] != '`')
                    return false;
            }
        }
        return true;
    }
}
