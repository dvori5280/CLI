using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;

// מיפוי שפות תכנות לסיומות קבצים
var languageExtensions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
{
    ["csharp"] = new[] { ".cs" },
    ["c#"] = new[] { ".cs" },
    ["java"] = new[] { ".java" },
    ["python"] = new[] { ".py" },
    ["javascript"] = new[] { ".js", ".jsx" },
    ["js"] = new[] { ".js", ".jsx" },
    ["typescript"] = new[] { ".ts", ".tsx" },
    ["ts"] = new[] { ".ts", ".tsx" },
    ["c"] = new[] { ".c", ".h" },
    ["cpp"] = new[] { ".cpp", ".hpp", ".cc", ".h" },
    ["c++"] = new[] { ".cpp", ".hpp", ".cc", ".h" },
    ["html"] = new[] { ".html", ".htm" },
    ["css"] = new[] { ".css", ".scss" },
    ["sql"] = new[] { ".sql" },
    ["go"] = new[] { ".go" },
    ["php"] = new[] { ".php" },
    ["ruby"] = new[] { ".rb" },
    ["kotlin"] = new[] { ".kt" },
    ["swift"] = new[] { ".swift" },
    ["rust"] = new[] { ".rs" },
};

// תיקיות שאין לכלול (תוצרי בנייה, ספריות חיצוניות וכו')
var excludedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "bin", "obj", "node_modules", ".git", ".vs", ".vscode", ".idea", "debug", "release", "packages"
};

// ---- הגדרת האפשרויות ----
var languageOption = new Option<string[]>("--language", "-l")
{
    Description = "רשימת שפות תכנות לכלול ב-bundle, או all לכל קבצי הקוד",
    Required = true,
    AllowMultipleArgumentsPerToken = true
};

var outputOption = new Option<FileInfo>("--output", "-o")
{
    Description = "שם קובץ ה-bundle (שם קובץ בלבד או נתיב מלא)",
    DefaultValueFactory = _ => new FileInfo("bundle.txt")
};

var noteOption = new Option<bool>("--note", "-n")
{
    Description = "רישום מקור הקוד (שם וניתוב יחסי) כהערה בקובץ ה-bundle"
};

var sortOption = new Option<string>("--sort", "-s")
{
    Description = "סדר העתקת הקבצים: name או type",
    DefaultValueFactory = _ => "name"
};
sortOption.AcceptOnlyFromAmong("name", "type");

var removeEmptyLinesOption = new Option<bool>("--remove-empty-lines", "-r")
{
    Description = "מחיקת שורות ריקות מקוד המקור"
};

var authorOption = new Option<string>("--author", "-a")
{
    Description = "שם יוצר הקובץ"
};

// ---- הגדרת הפקודה ----
var bundleCommand = new Command("bundle", "אריזת קבצי קוד לקובץ אחד");
bundleCommand.Options.Add(languageOption);
bundleCommand.Options.Add(outputOption);
bundleCommand.Options.Add(noteOption);
bundleCommand.Options.Add(sortOption);
bundleCommand.Options.Add(removeEmptyLinesOption);
bundleCommand.Options.Add(authorOption);


bundleCommand.SetAction(parseResult =>
{
    var languages = parseResult.GetValue(languageOption)!;
    var output = parseResult.GetValue(outputOption)!;
    var note = parseResult.GetValue(noteOption);
    var sort = parseResult.GetValue(sortOption)!;
    var removeEmptyLines = parseResult.GetValue(removeEmptyLinesOption);
    var author = parseResult.GetValue(authorOption);
    try
    {
        // בניית קבוצת הסיומות המותרות
        bool includeAll = languages.Any(l => l.Equals("all", StringComparison.OrdinalIgnoreCase));
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (includeAll)
        {
            foreach (var exts in languageExtensions.Values)
                extensions.UnionWith(exts);
        }
        else
        {
            foreach (var lang in languages)
            {
                if (languageExtensions.TryGetValue(lang, out var exts))
                    extensions.UnionWith(exts);
                else
                    Console.WriteLine($"Warning: unknown language '{lang}' - ignored.");
            }
        }

        if (extensions.Count == 0)
        {
            Console.WriteLine("Error: no valid languages were selected.");
            return;
        }

        // איסוף הקבצים מהתיקייה הנוכחית ותתי-התיקיות, בדילוג על תיקיות מוחרגות
        var currentDir = Directory.GetCurrentDirectory();
        var files = Directory.EnumerateFiles(currentDir, "*", SearchOption.AllDirectories)
            .Where(f => extensions.Contains(Path.GetExtension(f)))
            .Where(f => !Path.GetRelativePath(currentDir, f)
                             .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                             .Any(part => excludedDirs.Contains(part)))
            .Where(f => !Path.GetFullPath(f).Equals(output.FullName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        files = sort == "type"
        ? files.OrderBy(f => Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)
               .ThenBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
               .ToList()
        : files.OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
               .ToList();

        if (files.Count == 0)
        {
            Console.WriteLine("No matching code files were found.");
            return;
        }


        // כתיבת הקבצים לקובץ אחד
        using var writer = new StreamWriter(output.FullName);

        if (!string.IsNullOrWhiteSpace(author))
        {
            writer.WriteLine($"// Author: {author}");
            writer.WriteLine();
        }

        foreach (var file in files)
        {
            if (note)
                writer.WriteLine($"// Source: {Path.GetFileName(file)} | Path: {Path.GetRelativePath(currentDir, file)}");

            var lines = File.ReadAllLines(file);

            if (removeEmptyLines)
                lines = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();

            foreach (var line in lines)
                writer.WriteLine(line);

            writer.WriteLine();
        }

        Console.WriteLine($"Bundle created: {output.FullName} ({files.Count} files)");
    }
    catch (DirectoryNotFoundException)
    {
        Console.WriteLine("Error: the output path is invalid.");
    }
    catch (UnauthorizedAccessException)
    {
        Console.WriteLine("Error: no permission to write to the output path.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
});

// ---- פקודת create-rsp ----
var createRspCommand = new Command("create-rsp", "יצירת קובץ תגובה עם פקודת bundle מוכנה");

createRspCommand.SetAction(parseResult =>
{
    try
    {
        // language - חובה, ורק שפות מוכרות או all
        string languages;
        while (true)
        {
            Console.Write("Languages to include (separated by spaces, or 'all'): ");
            languages = Console.ReadLine()?.Trim() ?? "";

            if (languages == "")
            {
                Console.WriteLine("This field is required.");
                continue;
            }

            var langList = languages.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var unknown = langList
                .Where(l => !l.Equals("all", StringComparison.OrdinalIgnoreCase)
                         && !languageExtensions.ContainsKey(l))
                .ToList();

            if (unknown.Count == 0)
            {
                languages = string.Join(" ", langList);
                break;
            }

            Console.WriteLine($"Unknown language(s): {string.Join(", ", unknown)}");
            Console.WriteLine($"Available: all, {string.Join(", ", languageExtensions.Keys)}");
        }

        // output - לא חובה, Enter משאיר את ברירת המחדל
        Console.Write("Output file name or full path (Enter for bundle.txt): ");
        var output = Console.ReadLine()?.Trim().Trim('"') ?? "";

        //note to do: האם נכון?
        Console.Write("Add source file name and relative path as a comment? (yes/no, Enter for no): ");
        var note = Console.ReadLine()?.Trim().ToLower() is "yes" or "y";

        // sort
        string sort;
        while (true)
        {
            Console.Write("Sort order - name or type (Enter for name): ");
            sort = Console.ReadLine()?.Trim().ToLower() ?? "";
            if (sort == "") sort = "name";
            if (sort is "name" or "type") break;
            Console.WriteLine("Invalid value. Please enter 'name' or 'type'.");
        }
        //remove empty lines

        Console.Write("Remove empty lines? (yes/no, Enter for no): ");
        var remove = Console.ReadLine()?.Trim().ToLower() is "yes" or "y";



        //author
        Console.Write("Author name (Enter to skip): ");
        var author = Console.ReadLine()?.Trim().Trim('"') ?? "";


        // שם קובץ התגובה עצמו
        Console.Write("Response file name (Enter for bundle.rsp): ");
        var rspName = Console.ReadLine()?.Trim().Trim('"') ?? "";
        if (rspName == "")
            rspName = "bundle.rsp";
        if (!rspName.EndsWith(".rsp", StringComparison.OrdinalIgnoreCase))
            rspName += ".rsp";

        // בניית הפקודה המלאה - כל חלק בשורה נפרדת
        var lines = new List<string>
        {
            "bundle",
            $"--language {languages}"
        };
        if (output != "")
            lines.Add($"--output \"{output}\"");

        if (note)
            lines.Add("--note");

        lines.Add($"--sort {sort}");

        if (remove)
            lines.Add("--remove-empty-lines");
        if (author != "")
            lines.Add($"--author \"{author}\"");

        File.WriteAllLines(rspName, lines);

        Console.WriteLine($"Response file created: {Path.GetFullPath(rspName)}");
        Console.WriteLine($"Run it with: fib @{rspName}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: could not create the response file. {ex.Message}");
    }
});

// ---- פקודת השורש ----
var rootCommand = new RootCommand("כלי לאריזת קבצי קוד");
rootCommand.Subcommands.Add(bundleCommand);
rootCommand.Subcommands.Add(createRspCommand);

return rootCommand.Parse(args).Invoke();
