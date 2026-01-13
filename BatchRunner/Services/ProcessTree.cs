using System.Management;

namespace BatchRunner.Services;

public static class ProcessTree
{
    public static HashSet<int> GetDescendantProcessIds(int rootProcessId)
    {
        var descendants = new HashSet<int>();

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT ProcessId, ParentProcessId FROM Win32_Process");
            using var results = searcher.Get();
            var children = new Dictionary<int, List<int>>();

            foreach (ManagementObject item in results)
            {
                if (item["ProcessId"] is not uint pid || item["ParentProcessId"] is not uint ppid)
                {
                    continue;
                }

                var parentId = unchecked((int)ppid);
                var childId = unchecked((int)pid);

                if (!children.TryGetValue(parentId, out var list))
                {
                    list = new List<int>();
                    children[parentId] = list;
                }

                list.Add(childId);
            }

            var queue = new Queue<int>();
            queue.Enqueue(rootProcessId);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!children.TryGetValue(current, out var list))
                {
                    continue;
                }

                foreach (var child in list)
                {
                    if (descendants.Add(child))
                    {
                        queue.Enqueue(child);
                    }
                }
            }
        }
        catch
        {
        }

        return descendants;
    }
}
