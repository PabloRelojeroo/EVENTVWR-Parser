using System.Diagnostics;
using System.Runtime.InteropServices;

namespace EventvwrParser.Services;

public static class SessionInfo {
    [DllImport("kernel32.dll")]
    static extern ulong GetTickCount64();

    public static DateTime BootTime => DateTime.Now - TimeSpan.FromMilliseconds(GetTickCount64());

    public static DateTime LogonTime {
        get {
            var session = Process.GetCurrentProcess().SessionId;
            var best = DateTime.MaxValue;
            foreach (var p in Process.GetProcessesByName("explorer")) {
                try {
                    if (p.SessionId == session && p.StartTime < best) best = p.StartTime;
                } catch {
                } finally {
                    p.Dispose();
                }
            }
            return best == DateTime.MaxValue ? BootTime : best;
        }
    }
}
