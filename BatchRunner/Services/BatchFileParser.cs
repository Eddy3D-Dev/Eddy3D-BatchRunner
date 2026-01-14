using System.IO;
using System.Text.RegularExpressions;

namespace BatchRunner.Services;

public static class BatchFileParser
{
    private static readonly Regex NpRegex = new(@"(?i)(?:^|\s)-np\s*=?\s*(\d+)", RegexOptions.Compiled);

    public static int GetRequiredCores(string batPath)
    {
        if (!File.Exists(batPath))
        {
            return 1;
        }

        var cores = 1;
        string[] lines;

        try
        {
            lines = File.ReadAllLines(batPath);
        }
        catch
        {
            return 1;
        }

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("REM", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("::"))
            {
                continue;
            }

            foreach (Match match in NpRegex.Matches(line))
            {
                var value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                if (int.TryParse(value, out var parsed) && parsed > cores)
                {
                    cores = parsed;
                }
            }
        }

        // If no explicit -np found (or found 1), try to find system/decomposeParDict
        if (cores <= 1)
        {
            try
            {
                var dir = Path.GetDirectoryName(batPath);
                if (dir is not null)
                {
                    var dictPath = Path.Combine(dir, "system", "decomposeParDict");
                    if (File.Exists(dictPath))
                    {
                        var content = File.ReadAllText(dictPath);
                        var match = Regex.Match(content, @"numberOfSubdomains\s+(\d+);");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out var dictCores))
                        {
                            cores = dictCores;
                        }
                    }
                }
            }
            catch
            {
                // ignore errors reading dict
            }
        }

        return Math.Max(1, cores);
    }
}
