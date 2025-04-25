using System.Text;
using Ionic.Zlib;
using Spectre.Console;


class Program
{
    static string CsvPath = "DeobfuscationMap.csv";
    static bool IsGzipped = false;

    public static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        Console.Title = "DME - Deobfuscation Map Editor";

        CsvPath = AnsiConsole.Ask<string>("[cyan]Deobfuscation Map file[/]:");
        var map = LoadMap(CsvPath);
        Console.Clear();

        AnsiConsole.Write(new Rule("[bold #FF4500]M[/][bold #FF6347]a[/][bold #FF7F50]g[/][bold #FF8C00]m[/][bold #FFA500]a's[/] [orangered1]Deobfuscation Map Editor[/]").RuleStyle(Color.Red3).Centered());
        DisplayMapStats(map);

        while (true)
        {
            var option = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .AddChoices("🔍 Search", "➕ Add", "✏️ Update", "🗑  Remove", "🧹 Clean Data", "🔄️Reload", (IsGzipped ? "🗜️ Decompress" : "🗜️ Compress"), "🚪 Exit")
            );

            Console.WriteLine();
            if (option.Contains("Reload"))
            {
                SaveMap(CsvPath, map);
                map = LoadMap(CsvPath);
                Console.Clear();
                AnsiConsole.Write(new Rule("[bold #FF4500]M[/][bold #FF6347]a[/][bold #FF7F50]g[/][bold #FF8C00]m[/][bold #FFA500]a's[/] [orangered1]Deobfuscation Map Editor[/]").RuleStyle(Color.Red3).Centered());
                DisplayMapStats(map);
                continue;
            }
            else if (option.Contains("Decompress"))
            {
                IsGzipped = false;
                if (CsvPath.EndsWith(".gz"))
                {
                    CsvPath = CsvPath.Substring(0, CsvPath.Length - 3);
                }
                SaveMap(CsvPath, map);
                LoadMap(CsvPath);
                Thread.Sleep(250);
                AnsiConsole.MarkupLine("[bold green]✔ Decompressed and saved.[/]");
                Thread.Sleep(350);
                Console.Clear();
                AnsiConsole.Write(new Rule("[bold #FF4500]M[/][bold #FF6347]a[/][bold #FF7F50]g[/][bold #FF8C00]m[/][bold #FFA500]a's[/] [orangered1]Deobfuscation Map Editor[/]").RuleStyle(Color.Red3).Centered());
                DisplayMapStats(map);
                continue;
            }
            else if(option.Contains("Compress"))
            {
                IsGzipped = true;
                if (!CsvPath.EndsWith(".gz"))
                    CsvPath += ".gz";
                SaveMap(CsvPath, map);
                LoadMap(CsvPath);
                Thread.Sleep(250);
                AnsiConsole.MarkupLine("[bold green]✔ Compressed and saved.[/]");
                Thread.Sleep(350);
                Console.Clear();
                AnsiConsole.Write(new Rule("[bold #FF4500]M[/][bold #FF6347]a[/][bold #FF7F50]g[/][bold #FF8C00]m[/][bold #FFA500]a's[/] [orangered1]Deobfuscation Map Editor[/]").RuleStyle(Color.Red3).Centered());
                DisplayMapStats(map);
                continue;
            }
            else if (option.Contains("Search"))
            {
                string term = AnsiConsole.Ask<string>("[cyan]Enter name[/] ([yellow]identifier[/] or [green]obfuscated[/]):");

                var results = map
                    .Where(kvp => kvp.Key.Contains(term, StringComparison.OrdinalIgnoreCase)
                               || kvp.Value.Contains(term, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (results.Count == 0)
                {
                    AnsiConsole.MarkupLine("[red]No matches found.[/]");
                }
                else
                {
                    Console.CursorTop -= 1;
                    var table = new Table()
                        .Title("[bold yellow]Search Results[/]")
                        .AddColumn("[bold yellow]Obfuscated[/]")
                        .AddColumn("[bold cyan]Identifier[/]")
                        .BorderColor(Color.Green)
                        .Border(TableBorder.Rounded);

                    foreach (var (key, value) in results)
                        table.AddRow(key, value);

                    AnsiConsole.Write(table);
                }
            }
            else if (option.Contains("Add"))
            {
                string obf = AnsiConsole.Ask<string>("[cyan]Enter Obfuscated name[/]:");
                string real = AnsiConsole.Ask<string>("[cyan]Enter Identifier name[/]:");

                map[obf] = real;
                SaveMap(CsvPath, map);
                AnsiConsole.MarkupLine("[bold green]✔ Added and saved.[/]");
            }
            else if (option.Contains("Update"))
            {
                string realNameToUpdate = AnsiConsole.Ask<string>("Enter the identifier you want to update:");

                var obfuscatedNames = map.Where(kvp => kvp.Value.Equals(realNameToUpdate, StringComparison.Ordinal)).ToList();

                if (obfuscatedNames.Count == 0)
                {
                    AnsiConsole.MarkupLine("[red]No obfuscated names found for this identifier.[/]");
                    return;
                }

                var table = new Table()
                    .AddColumn("[bold yellow]Obfuscated[/]")
                    .AddColumn("[bold cyan]Identifier[/]")
                    .BorderColor(Color.Green)
                    .Border(TableBorder.Rounded);

                foreach (var (key, value) in obfuscatedNames)
                    table.AddRow(key, value);

                AnsiConsole.Write(table);

                string newRealName = AnsiConsole.Ask<string>("[cyan]Enter the new identifier[/]:");

                foreach (var kvp in obfuscatedNames)
                    map[kvp.Key] = newRealName;
                
                SaveMap(CsvPath, map);
                AnsiConsole.MarkupLine("[green]identifier updated for all matching obfuscated names and saved.[/]");
            }
            else if (option.Contains("Clean"))
            {
                CleanData(ref map);
            }
            else if (option.Contains("Remove"))
            {
                string obf = AnsiConsole.Ask<string>("[red]Enter Obfuscated name to remove[/]:");

                if (map.Remove(obf))
                {
                    SaveMap(CsvPath, map);
                    AnsiConsole.MarkupLine("[bold green]✔ Entry removed.[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]No such key found.[/]");
                }
            }
            else if (option.Contains("Exit"))
            {
                break;
            }
        }
    }

    static void DisplayMapStats(Dictionary<string, string> map)
    {
        int uniqueRealNamesCount = map.Values.Distinct().Count();
        long lastEditTimestamp = File.Exists(CsvPath) ? new FileInfo(CsvPath).LastWriteTime.Ticks : 0;
        DateTime lastEditTime = new DateTime(lastEditTimestamp);

        AnsiConsole.MarkupLine($"[bold green]File Statistics:[/]");
        AnsiConsole.MarkupLine($"[bold yellow] - Distinct Entries[/]: [Orange1]{uniqueRealNamesCount}[/]");
        AnsiConsole.MarkupLine($"[bold yellow] - Total Entries[/]: [Orange1]{map.Count}[/]");
        AnsiConsole.MarkupLine($"[bold yellow] - Last Edited[/]: [Orange1]{lastEditTime}[/]");
        AnsiConsole.MarkupLine($"[bold yellow] - File Size[/]: [Orange1]{new FileInfo(CsvPath).Length / 1024}[/] [orange3]KB[/]");
        AnsiConsole.MarkupLine($"[bold yellow] - Compressed[/]: {(IsGzipped ? "[Orange1]true[/]" : "[Lime]false[/]")}");
        AnsiConsole.MarkupLine($"[Orange1]---------------------------[/]");
    }

    static void CleanData(ref Dictionary<string, string> map)
    {
        var invalidEntries = map
            .Where(kvp => !IsValidCSharpIdentifier(kvp.Value))
            .ToList();

        if (invalidEntries.Count == 0)
        {
            AnsiConsole.MarkupLine("[bold green]No invalid entries found.[/]");
            return;
        }

        var table = new Table().Title("[bold red]Invalid Entries[/]")
            .AddColumn("[bold yellow]Obfuscated[/]")
            .AddColumn("[bold red]identifier Name[/]")
            .BorderColor(Color.Red)
            .Border(TableBorder.Rounded);

        foreach (var kvp in invalidEntries)
            table.AddRow(kvp.Key, kvp.Value);

        AnsiConsole.Write(table);

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[red]Do you want to remove these invalid entries?[/]")
                .AddChoices("Yes", "No (Keep them)")
        );

        if (choice.StartsWith("Yes"))
        {
            foreach (var kvp in invalidEntries)
                map.Remove(kvp.Key);

            SaveMap(CsvPath, map);
            AnsiConsole.MarkupLine($"[bold green]{invalidEntries.Count} invalid entries removed and file updated.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Keeping all entries.[/]");
        }
    }

    static Dictionary<string, string> LoadMap(string path)
    {
        IsGzipped = path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);
        var map = new Dictionary<string, string>();

        try
        {
            string[] lines;
            if (IsGzipped)
            {
                using var fileStream = File.OpenRead(path);
                using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
                using var reader = new StreamReader(gzipStream);
                var raw = reader.ReadToEnd();
                lines = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            }
            else
            {
                lines = File.ReadAllLines(path);
            }

            foreach (var line in lines)
            {
                var parts = line.Trim().Split(';');
                if (parts.Length >= 2)
                    map[parts[0]] = parts[1];
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to load map: {ex.Message}[/]");
        }

        return map;
    }
    public static void SaveMap(string path, Dictionary<string, string> map)
    {
        var sorted = map
        .OrderBy(kvp => GetSortKey(kvp.Value), StringComparer.OrdinalIgnoreCase)
        .ThenBy(kvp => kvp.Value, StringComparer.OrdinalIgnoreCase)
        .ToList();

        var csvData = string.Join(Environment.NewLine, sorted.Select(kvp => $"{kvp.Key};{kvp.Value}"));

        if (IsGzipped)
        {
            // Use UTF-8 without BOM to match gzip behavior from Python/zlib
            using var fileStream = File.Create(path);
            using var gzipStream = new GZipStream(fileStream, CompressionMode.Compress, CompressionLevel.BestCompression, false);
            using var writer = new StreamWriter(gzipStream, new UTF8Encoding(false)); // no BOM
            writer.Write(csvData);
            writer.Flush(); // Make sure all content is written
        }
        else
        {
            File.WriteAllText(path, csvData, new UTF8Encoding(false)); // Also no BOM for consistency
        }
    }

    static string GetSortKey(string realName)
    {
        var parts = realName.Split('.');
        if (parts.Length >= 2)
            return string.Join(".", parts.Take(2));
        return realName;
    }
    static bool IsValidCSharpIdentifier(string name)
    {
        var parts = name.Split('.');
        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part)) return false;
            if (!char.IsLetter(part[0]) && part[0] != '_') return false;

            for (var i = 1; i < part.Length; i++)
            {
                if (!char.IsLetterOrDigit(part[i]) && part[i] != '_' && part[i] != '`')
                    return false;
            }
        }
        return true;
    }
}
