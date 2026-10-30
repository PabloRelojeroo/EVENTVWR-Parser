using System.Collections.Concurrent;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using EventvwrParser.Localization;
using EventvwrParser.Models;

namespace EventvwrParser.Services;

public sealed class CollectResult {
    public List<LogEvent> Events { get; set; } = new();
    public List<Localized> Warnings { get; set; } = new();
}

public static class EventCollector {
    const string TaskOperational = "Microsoft-Windows-TaskScheduler/Operational";

    static readonly Dictionary<int, (Localized type, Localized label)> TaskIds = new() {
        [100] = (Strings.TypeTaskStart, Strings.TaskStarted),
        [103] = (Strings.TypeTaskStart, Strings.TaskStartFailed),
        [107] = (Strings.TypeTaskStart, Strings.TaskTimeTrigger),
        [108] = (Strings.TypeTaskStart, Strings.TaskEventTrigger),
        [110] = (Strings.TypeTaskStart, Strings.TaskStartedByUser),
        [118] = (Strings.TypeTaskStart, Strings.TaskStartedAtBoot),
        [119] = (Strings.TypeTaskStart, Strings.TaskStartedAtLogon),
        [201] = (Strings.TypeTaskEnd, Strings.TaskActionCompleted),
        [102] = (Strings.TypeTaskEnd, Strings.TaskInstanceFinished),
        [106] = (Strings.TypeTaskChange, Strings.TaskRegistered),
        [140] = (Strings.TypeTaskChange, Strings.TaskUpdated),
        [141] = (Strings.TypeTaskChange, Strings.TaskDeleted),
    };

    const string WinPs = "Windows PowerShell";
    const string PsOperational = "Microsoft-Windows-PowerShell/Operational";
    const string NtfsOperational = "Microsoft-Windows-Ntfs/Operational";

    static readonly Regex HostApp = new(@"HostApplication=([^\r\n]*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex CmdLine = new(@"CommandLine=([^\r\n]*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex CtxCommand = new(@"Command Name\s*=\s*([^\r\n]*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex CtxHost = new(@"Host Application\s*=\s*([^\r\n]*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);
    static readonly Regex PsImage = new(@"\\(powershell|pwsh|powershell_ise)\.exe$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex NoiseScript = new(@"^(\{\s*Set-StrictMode -Version 1;.*\}|prompt|\$global:\?|exit)$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

    public static CollectResult Collect(DateTime logon) {
        var events = new ConcurrentBag<LogEvent>();
        var warnings = new ConcurrentBag<Localized>();

        var jobs = new (string name, Action<ConcurrentBag<LogEvent>> run)[] {
            ("Application", b => Read("Application", new[] { 3079 }, r => b.Add(MapJournal3079(r)))),
            ("NTFS Operational", b => Read(NtfsOperational, new[] { 501 }, r => b.Add(MapNtfs501(r)))),
            ("System", b => Read("System", new[] { 104, 7045 }, r => b.Add(MapSystem(r)))),
            ("Security", b => Read("Security", new[] { 1102, 4688 }, r => b.Add(MapSecurity(r)))),
            ("Windows PowerShell", b => Read(WinPs, new[] { 400, 600, 800 }, r => b.Add(MapWinPs(r)))),
            ("PowerShell Operational (4103)", b => Read(PsOperational, new[] { 4103 }, r => b.Add(Map4103(r)))),
            ("PowerShell Operational (4104)", b => { foreach (var e in ReadScriptBlocks()) b.Add(e); }),
            ("PSReadLine", b => { foreach (var e in ReadPsReadLine(warnings)) b.Add(e); }),
            ("TaskScheduler", b => {
                if (!IsLogEnabled(TaskOperational)) warnings.Add(Strings.TaskHistoryDisabled);
                Read(TaskOperational, TaskIds.Keys.ToArray(), r => b.Add(MapTask(r)));
            }),
        };

        Parallel.ForEach(jobs, job => {
            try {
                job.run(events);
            } catch (EventLogNotFoundException) {
                warnings.Add(Strings.LogNotFound(job.name));
            } catch (UnauthorizedAccessException) {
                warnings.Add(Strings.AccessDenied(job.name));
            } catch (Exception ex) {
                warnings.Add($"{job.name}: {ex.Message}");
            }
        });

        var list = events.ToList();
        foreach (var e in list) e.InInstance = e.Time >= logon;
        MarkNoise(list);
        list.Sort((a, b) => {
            var c = b.Time.CompareTo(a.Time);
            return c != 0 ? c : b.Seq.CompareTo(a.Seq);
        });

        return new CollectResult { Events = list, Warnings = warnings.OrderBy(w => w.ToString()).ToList() };
    }

    static void Read(string log, int[] ids, Action<EventRecord> onRecord) {
        var xpath = "*[System[(" + string.Join(" or ", ids.Select(i => $"EventID={i}")) + ")]]";
        var query = new EventLogQuery(log, PathType.LogName, xpath) { ReverseDirection = true };
        using var reader = new EventLogReader(query);
        EventRecord? rec;
        while ((rec = reader.ReadEvent()) != null) {
            using (rec) onRecord(rec);
        }
    }

    static LogEvent MapJournal3079(EventRecord r) {
        var desc = Describe(r);
        var e = Base(r, Strings.GroupJournal, Strings.TypeUsnDeleted, desc);
        e.Summary = OneLine(desc);
        return e;
    }

    static LogEvent MapNtfs501(EventRecord r) {
        var desc = Describe(r) + "\n\n" + JoinProps(r);
        var fsutil = desc.IndexOf("fsutil", StringComparison.OrdinalIgnoreCase) >= 0;
        var e = Base(r, Strings.GroupJournal, fsutil ? Strings.TypeJournalFsutil : Strings.TypeJournalDeleted, desc);
        e.Summary = OneLine(Describe(r));
        return e;
    }

    static LogEvent MapSystem(EventRecord r) {
        var desc = Describe(r);
        if (r.Id == 104) {
            var e = Base(r, Strings.GroupLogs, Strings.TypeLogCleared, desc);
            e.Summary = OneLine(desc);
            return e;
        }

        var svc = Base(r, Strings.GroupSystem, Strings.TypeServiceInstalled, desc);
        svc.Summary = OneLine($"{Prop(r, 0)} -> {Prop(r, 1)}");
        ApplyDetection(svc, svc.Summary.ToString());
        return svc;
    }

    static LogEvent MapSecurity(EventRecord r) {
        if (r.Id == 1102) {
            var desc = Describe(r);
            var e = Base(r, Strings.GroupLogs, Strings.TypeLogCleared, desc);
            e.Summary = Strings.SecurityLogCleared;
            return e;
        }

        var image = Prop(r, 5);
        var cmd = Prop(r, 8);
        var parent = Prop(r, 13);
        var isPs = PsImage.IsMatch(image);
        var details = Strings.ProcessDetails(image, cmd, parent, $"{Prop(r, 2)}\\{Prop(r, 1)}");
        var p = Base(r, isPs ? Strings.GroupPowerShell : Strings.GroupSystem, isPs ? Strings.TypePowerShellProcess : Strings.TypeProcessCreated, details);
        p.Summary = OneLine(string.IsNullOrWhiteSpace(cmd) ? image : cmd);
        ApplyDetection(p, cmd);
        return p;
    }

    static LogEvent MapWinPs(EventRecord r) {
        var raw = JoinProps(r);
        var host = Match(HostApp, raw);
        var cmd = Match(CmdLine, raw);
        var e = Base(r, Strings.GroupPowerShell, Strings.TypeCommand, raw.Trim());
        e.Summary = OneLine(!string.IsNullOrWhiteSpace(host) ? host : !string.IsNullOrWhiteSpace(cmd) ? cmd : FirstLine(raw));
        ApplyDetection(e, host + "\n" + cmd);
        return e;
    }

    static LogEvent Map4103(EventRecord r) {
        var context = Prop(r, 0);
        var payload = Prop(r, 2);
        var command = Match(CtxCommand, context);
        var host = Match(CtxHost, context);
        var e = Base(r, Strings.GroupPowerShell, Strings.TypeCmdlet, Strings.CmdletDetails(payload, context));
        e.Summary = OneLine($"{command}  |  {FirstLine(payload)}");
        ApplyDetection(e, payload + "\n" + host);
        return e;
    }

    static LogEvent MapTask(EventRecord r) {
        var (type, label) = TaskIds.TryGetValue(r.Id, out var t) ? t : (Strings.TypeTaskStart, Strings.TaskEvent);
        var data = EventData(r);
        var task = data.FirstOrDefault(d => d.name == "TaskName").value ?? Prop(r, 0);
        var action = data.FirstOrDefault(d => d.name == "ActionName").value;

        var details = label.Map(l => {
            var sb = new StringBuilder();
            sb.AppendLine(l);
            sb.AppendLine();
            foreach (var (name, value) in data) sb.AppendLine($"{name}: {value}");
            return sb.ToString();
        });

        var e = Base(r, Strings.GroupTasks, type, details);
        e.Summary = label.Map(l => OneLine($"{l} - {task}" + (string.IsNullOrWhiteSpace(action) ? "" : $" -> {action}")));
        if (!string.IsNullOrWhiteSpace(action)) ApplyDetection(e, action!);
        return e;
    }

    static List<(string name, string value)> EventData(EventRecord r) {
        var result = new List<(string, string)>();
        try {
            var doc = System.Xml.Linq.XDocument.Parse(r.ToXml());
            foreach (var el in doc.Descendants().Where(x => x.Name.LocalName == "Data"))
                result.Add(((string?)el.Attribute("Name") ?? "", el.Value));
        } catch {
        }
        return result;
    }

    static bool IsLogEnabled(string log) {
        try {
            using var cfg = new EventLogConfiguration(log);
            return cfg.IsEnabled;
        } catch {
            return true;
        }
    }

    static IEnumerable<LogEvent> ReadScriptBlocks() {
        var parts = new Dictionary<string, List<(int n, int total, string text, DateTime time, string path, bool warn, long rec)>>();
        Read(PsOperational, new[] { 4104 }, r => {
            var id = Prop(r, 3);
            if (string.IsNullOrEmpty(id)) id = r.RecordId?.ToString() ?? Guid.NewGuid().ToString();
            if (!parts.TryGetValue(id, out var list)) parts[id] = list = new();
            int.TryParse(Prop(r, 0), out var n);
            int.TryParse(Prop(r, 1), out var total);
            list.Add((n, total, Prop(r, 2), r.TimeCreated ?? DateTime.MinValue, Prop(r, 4), r.Level == 3, r.RecordId ?? 0));
        });

        foreach (var kv in parts) {
            var id = kv.Key;
            var ordered = kv.Value.OrderBy(p => p.n).ToList();
            var text = string.Concat(ordered.Select(p => p.text));
            var first = ordered[0];
            var path = first.path;
            var warn = ordered.Any(p => p.warn);
            var total = ordered.Max(p => p.total);
            var trusted = IsTrustedPath(path);

            var e = new LogEvent {
                Time = ordered.Min(p => p.time),
                Id = 4104,
                Log = PsOperational,
                Group = Strings.GroupPowerShell,
                Type = Strings.TypeScript,
                Details = Strings.ScriptDetails(id, path, ordered.Count, total, text),
                Summary = OneLine((string.IsNullOrWhiteSpace(path) ? "" : $"[{Path.GetFileName(path)}]  ") + text),
                Seq = (int)Math.Min(int.MaxValue, first.rec),
                IsNoise = NoiseScript.IsMatch(text.Trim()),
            };
            ApplyDetection(e, text, trusted, warn);
            yield return e;
        }
    }

    static IEnumerable<LogEvent> ReadPsReadLine(ConcurrentBag<Localized> warnings) {
        var usersRoot = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\", "Users");
        foreach (var userDir in SafeDirs(usersRoot)) {
            var dir = Path.Combine(userDir, @"AppData\Roaming\Microsoft\Windows\PowerShell\PSReadLine");
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.GetFiles(dir, "*.txt")) {
                string[] lines;
                try {
                    lines = File.ReadAllLines(file);
                } catch (Exception ex) {
                    warnings.Add($"PSReadLine: {ex.Message}");
                    continue;
                }

                var written = File.GetLastWriteTime(file);
                var user = Path.GetFileName(userDir);
                var commands = JoinContinuations(lines);
                for (var i = 0; i < commands.Count; i++) {
                    var cmd = commands[i];
                    var e = new LogEvent {
                        Time = written,
                        TimeApprox = true,
                        Id = 0,
                        Seq = i,
                        Log = $"PSReadLine ({user})",
                        Group = Strings.GroupPowerShell,
                        Type = Strings.TypePsReadLine,
                        Summary = OneLine(cmd),
                        Details = Strings.PsReadLineDetails(file, i + 1, commands.Count, written, cmd),
                    };
                    ApplyDetection(e, cmd);
                    yield return e;
                }
            }
        }
    }

    static List<string> JoinContinuations(string[] lines) {
        var result = new List<string>();
        var sb = new StringBuilder();
        foreach (var line in lines) {
            if (line.EndsWith("`")) {
                sb.AppendLine(line.Substring(0, line.Length - 1));
                continue;
            }
            sb.Append(line);
            var cmd = sb.ToString();
            sb.Clear();
            if (!string.IsNullOrWhiteSpace(cmd)) result.Add(cmd);
        }
        if (sb.Length > 0) result.Add(sb.ToString());
        return result;
    }

    static IEnumerable<string> SafeDirs(string root) {
        try {
            return Directory.GetDirectories(root);
        } catch {
            return Array.Empty<string>();
        }
    }

    static void MarkNoise(List<LogEvent> events) {
        var last = new Dictionary<string, (LogEvent rep, DateTime t)>();
        foreach (var e in events.Where(x => !x.TimeApprox && !x.IsNoise).OrderBy(x => x.Time).ThenBy(x => x.Seq)) {
            var winPs = e.Log == WinPs;
            var key = winPs ? $"{e.Log}|{e.Summary}" : $"{e.Log}|{e.Id}|{e.Summary}";
            var window = TimeSpan.FromSeconds(winPs ? 10 : 2);

            if (last.TryGetValue(key, out var prev) && e.Time - prev.t <= window) {
                var rep = prev.rep;
                if (winPs && e.Id == 400 && rep.Id != 400) {
                    rep.IsNoise = true;
                    e.Related = rep.Related + 1;
                    rep.Related = 0;
                    rep = e;
                } else {
                    e.IsNoise = true;
                    rep.Related++;
                }
                last[key] = (rep, e.Time);
            } else {
                last[key] = (e, e.Time);
            }
        }
    }

    static LogEvent Base(EventRecord r, Localized group, Localized type, Localized details) => new() {
        Time = r.TimeCreated ?? DateTime.MinValue,
        Id = r.Id,
        Seq = (int)Math.Min(int.MaxValue, r.RecordId ?? 0),
        Log = r.LogName ?? "",
        Group = group,
        Type = type,
        Details = details,
    };

    static void ApplyDetection(LogEvent e, string text, bool trusted = false, bool flagged = false) {
        var d = FilelessDetector.Analyze(text, trusted, flagged);
        e.IsFileless |= d.Fileless;
        foreach (var reason in d.Reasons)
            if (!e.Reasons.Contains(reason)) e.Reasons.Add(reason);
    }

    static bool IsTrustedPath(string path) {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        return path.StartsWith(win + "\\", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(pf + "\\", StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrEmpty(pf86) && path.StartsWith(pf86 + "\\", StringComparison.OrdinalIgnoreCase));
    }

    static string Describe(EventRecord r) {
        try {
            var d = r.FormatDescription();
            if (!string.IsNullOrWhiteSpace(d)) return d;
        } catch {
        }
        return JoinProps(r);
    }

    static string JoinProps(EventRecord r) {
        try {
            return string.Join("\n", r.Properties.Select(p => p.Value?.ToString() ?? ""));
        } catch {
            return "";
        }
    }

    static string Prop(EventRecord r, int i) {
        try {
            return r.Properties.Count > i ? r.Properties[i].Value?.ToString() ?? "" : "";
        } catch {
            return "";
        }
    }

    static string Match(Regex re, string text) {
        var m = re.Match(text);
        return m.Success ? m.Groups[1].Value.Trim() : "";
    }

    static string FirstLine(string text) {
        foreach (var line in text.Split('\n'))
            if (!string.IsNullOrWhiteSpace(line)) return line.Trim();
        return "";
    }

    static string OneLine(string text, int max = 300) {
        var s = Spaces.Replace(text ?? "", " ").Trim();
        return s.Length > max ? s.Substring(0, max) + "..." : s;
    }
}
