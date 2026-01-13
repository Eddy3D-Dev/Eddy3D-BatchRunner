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

        return Math.Max(1, cores);
    }
}
