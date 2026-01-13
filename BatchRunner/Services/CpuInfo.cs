using System.Management;

namespace BatchRunner.Services;

public static class CpuInfo
{
    public static int GetPhysicalCoreCount()
    {
        try
        {
            var total = 0;
            using var searcher = new ManagementObjectSearcher("SELECT NumberOfCores FROM Win32_Processor");
            using var results = searcher.Get();
            foreach (var item in results)
            {
                if (item["NumberOfCores"] is int cores)
                {
                    total += cores;
                }
            }

            if (total > 0)
            {
                return total;
            }
        }
        catch
        {
        }

        return Environment.ProcessorCount;
    }
}
