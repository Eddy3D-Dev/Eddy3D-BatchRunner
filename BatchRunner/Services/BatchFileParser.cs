using System.IO;
using System.Text.RegularExpressions;

namespace BatchRunner.Services;

public static class BatchFileParser
{
    private static readonly Regex NpRegex = new(@"(?i)(?:^|\s)-(?:np|n)\s*=?\s*(\d+)", RegexOptions.Compiled);

    public static int GetRequiredCores(string batPath)
    {
        if (!File.Exists(batPath))
        {
            return 1;
        }

        var cores = 1;

        try
        {
            // ⚡ Bolt: Use File.ReadLines() instead of File.ReadAllLines()
            // File.ReadAllLines loads the entire file into an array of strings in memory.
            // File.ReadLines returns an IEnumerable<string> and reads lines lazily.
            // This prevents unnecessary memory allocation, especially for large script files.
            foreach (var line in File.ReadLines(batPath))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("REM", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("::"))
                {
                    continue;
                }

                // Fast preliminary check to avoid Regex overhead on most lines
                if (line.IndexOf("-n", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    foreach (Match match in NpRegex.Matches(line))
                    {
                        if (match.Groups[1].Success && int.TryParse(match.Groups[1].Value, out var parsed) && parsed > cores)
                        {
                            cores = parsed;
                        }
                    }
                }
            }
        }
        catch
        {
            return 1;
        }

        // Always check system/decomposeParDict and take the max value.
        // This handles cases where scripts might not have -np flags but rely on the dictionary.
        try
        {
            var dir = Path.GetDirectoryName(batPath);
            
            // Recursively check parent directories for system/decomposeParDict (max 5 levels up)
            for (int i = 0; i < 5; i++)
            {
                if (dir is null || !Directory.Exists(dir))
                {
                    break;
                }

                var dictPath = Path.Combine(dir, "system", "decomposeParDict");
                if (File.Exists(dictPath))
                {
                    var content = File.ReadAllText(dictPath);
                    var match = Regex.Match(content, @"numberOfSubdomains\s+(\d+)\s*;");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out var dictCores))
                    {
                        if (dictCores > cores)
                        {
                            cores = dictCores;
                        }
                    }
                    break;
                }
                
                dir = Directory.GetParent(dir)?.FullName;
            }
        }
        catch
        {
            // ignore errors reading dict
        }

        return Math.Max(1, cores);
    }
}
