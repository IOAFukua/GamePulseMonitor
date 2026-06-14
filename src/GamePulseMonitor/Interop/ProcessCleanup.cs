using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GamePulseMonitor.Interop;

internal static class ProcessCleanup
{
    private const uint Th32csSnapProcess = 0x00000002;
    private static readonly nint InvalidHandleValue = new(-1);

    public static void KillDescendants(int rootPid)
    {
        foreach (var pid in GetDescendantProcessIds(rootPid))
        {
            KillProcess(pid, rootPid);
        }
    }

    public static void KillProcessesByName(string processName, int currentPid)
    {
        foreach (var process in Process.GetProcessesByName(processName))
        {
            using (process)
            {
                KillProcess(process, currentPid);
            }
        }
    }

    private static IReadOnlyList<int> GetDescendantProcessIds(int rootPid)
    {
        var childrenByParent = SnapshotChildrenByParentId();
        var descendants = new List<int>();
        var stack = new Stack<int>();
        stack.Push(rootPid);

        while (stack.Count > 0)
        {
            var parentPid = stack.Pop();
            if (!childrenByParent.TryGetValue(parentPid, out var children))
            {
                continue;
            }

            foreach (var childPid in children)
            {
                descendants.Add(childPid);
                stack.Push(childPid);
            }
        }

        return descendants;
    }

    private static Dictionary<int, List<int>> SnapshotChildrenByParentId()
    {
        var result = new Dictionary<int, List<int>>();
        var snapshot = CreateToolhelp32Snapshot(Th32csSnapProcess, 0);
        if (snapshot == InvalidHandleValue)
        {
            return result;
        }

        try
        {
            var entry = new ProcessEntry32
            {
                dwSize = (uint)Marshal.SizeOf<ProcessEntry32>()
            };

            if (!Process32First(snapshot, ref entry))
            {
                return result;
            }

            do
            {
                var parentPid = unchecked((int)entry.th32ParentProcessID);
                var pid = unchecked((int)entry.th32ProcessID);
                if (!result.TryGetValue(parentPid, out var children))
                {
                    children = new List<int>();
                    result[parentPid] = children;
                }

                children.Add(pid);
            }
            while (Process32Next(snapshot, ref entry));
        }
        finally
        {
            CloseHandle(snapshot);
        }

        return result;
    }

    private static void KillProcess(int pid, int currentPid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            KillProcess(process, currentPid);
        }
        catch
        {
            // Process cleanup is best-effort; the process may already be gone.
        }
    }

    private static void KillProcess(Process process, int currentPid)
    {
        try
        {
            if (process.Id == currentPid || process.HasExited)
            {
                return;
            }

            process.Kill(entireProcessTree: true);
            process.WaitForExit(3000);
        }
        catch
        {
            // Avoid blocking app shutdown on a process that exits or denies access mid-cleanup.
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", EntryPoint = "Process32FirstW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Process32First(nint hSnapshot, ref ProcessEntry32 lppe);

    [DllImport("kernel32.dll", EntryPoint = "Process32NextW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Process32Next(nint hSnapshot, ref ProcessEntry32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(nint hObject);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public nint th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }
}
