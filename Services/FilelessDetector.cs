using System.Text.RegularExpressions;

namespace EventvwrParser.Services;

public sealed record Detection(bool Fileless, bool AntiForensic, List<string> Reasons);

public static class FilelessDetector {
    const RegexOptions O = RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant;
    static readonly Regex Reflective = new(
        @"\[(System\.)?Reflection\.Assembly\]::(Load|LoadFile|LoadFrom|UnsafeLoadFrom)|\[AppDomain\]::CurrentDomain\.Load|::Load\s*\(\s*\[(System\.)?Convert\]|\.EntryPoint\.Invoke|GetMethod\(\s*['""]Main", O);

    static readonly Regex Memory = new(
        @"\b(VirtualAlloc(Ex)?|VirtualProtect|CreateThread|CreateRemoteThread|WriteProcessMemory|RtlMoveMemory|NtAllocateVirtualMemory)\b|DllImport|GetDelegateForFunctionPointer|DefineDynamicAssembly", O);

    static readonly Regex Encoded = new(
        @"FromBase64String|\s-(e|ec|en|enc|enco|encodedcommand)\s+[A-Za-z0-9+/=]{24,}|\bGzipStream\b|\bDeflateStream\b", O);

    static readonly Regex Stealth = new(
        @"\s-w(in(dow(style)?)?)?\s+h(idden)?\b|\s-nop(rofile)?\b|-(ep|exec|executionpolicy)\s+bypass|Set-MpPreference|Add-MpPreference\s+-Exclusion|AmsiUtils|amsiInitFailed", O);

    static readonly Regex CharCast = new(@"\[char\]\s*\(?\s*\d+", O);
    static readonly Regex TickWord = new(@"[a-z]`[a-z]", O);
    static readonly Regex FormatObf = new(@"[""'](\{\d+\}){3,}[""']\s*-f", O);
    static readonly Regex BitObf = new(@"-bxor|\[array\]::Reverse|-join\s*\(?\s*\[char", O);

    static readonly (Regex re, string reason)[] AntiForensic = {
        (new(@"(Remove-Item|\bri\b|\brm\b|\bdel\b|\berase\b|\brd\b|\brmdir\b|Clear-Content)[^\r\n|;]*Prefetch", O), "Borrado de Prefetch"),
        (new(@"wevtutil(\.exe)?\s+(cl|clear-log)\b", O), "Limpieza de logs (wevtutil)"),
        (new(@"Clear-EventLog|Remove-EventLog|Limit-EventLog|\.ClearLog\s*\(", O), "Limpieza de logs de eventos"),
        (new(@"fsutil(\.exe)?\s+usn\s+deletejournal", O), "Borrado del journal USN (fsutil)"),
        (new(@"Clear-History|ConsoleHost_history|HistorySaveStyle\s+SaveNothing|Remove-Module\s+PSReadLine", O), "Toco el historial de powershell"),
        (new(@"vssadmin(\.exe)?\s+delete\s+shadows|shadowcopy\s+delete", O), "Borrado de shadow copies"),
        (new(@"\bSet-Date\b|w32tm(\.exe)?\s+/config", O), "Cambio de hora del sistema"),
        (new(@"(Remove-Item|\bri\b|\brm\b|\bdel\b|\berase\b)[^\r\n|;]*(\\Recent\\?|AppCompat|Amcache|SRUDB|\.pf\b|\\Minidump|JumpLists)", O), "Borrado de artefactos forenses"),
        (new(@"(reg(\.exe)?\s+delete|Remove-Item(Property)?)[^\r\n|;]*(\\bam\\|UserAssist|BagMRU|MuiCache|AppCompatFlags|RecentDocs|ShimCache)", O), "Borrado de claves de registro forenses"),
        (new(@"(Stop-Service|Set-Service|sc(\.exe)?\s+(stop|config)|net\s+stop)[^\r\n|;]*\b(EventLog|SysMain|PcaSvc|bam|DPS|DiagTrack)\b", O), "Freno o deshabilito un servicio (sysmain, pcasvc, etc)"),
    };

    public static Detection Analyze(string text, bool trusted = false, bool flaggedByPowerShell = false) {
        var reasons = new List<string>();
        var fileless = false;

        if (!trusted && !string.IsNullOrWhiteSpace(text)) {
            var score = 0;
            var refl = Reflective.IsMatch(text);
            var mem = Memory.IsMatch(text);

            if (refl) { score += 3; reasons.Add("Carga Assembly.Load"); }
            if (mem) { score += 3; reasons.Add("Usa APIs de memoria/inyeccion"); }
            if (Encoded.IsMatch(text)) { score += 2; reasons.Add("Tiene base64 o -enc"); }
            if (Stealth.IsMatch(text)) { score += 1; reasons.Add("Ventana oculta o bypass"); }
            if (IsObfuscated(text)) { score += 2; reasons.Add("Script ofuscado"); }
            if (flaggedByPowerShell) { score += 2; reasons.Add("PowerShell lo marco como sospechoso"); }

            fileless = refl || mem || score >= 5;
        }

        var anti = false;
        foreach (var (re, reason) in AntiForensic) {
            if (!re.IsMatch(text)) continue;
            anti = true;
            reasons.Add(reason);
        }

        return new Detection(fileless, anti, reasons);
    }

    static bool IsObfuscated(string text) =>
        CharCast.Matches(text).Count >= 8
        || TickWord.Matches(text).Count >= 3
        || FormatObf.IsMatch(text)
        || BitObf.IsMatch(text);
}
