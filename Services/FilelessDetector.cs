using System.Text.RegularExpressions;
using EventvwrParser.Localization;

namespace EventvwrParser.Services;

public sealed record Detection(bool Fileless, List<Localized> Reasons);

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

    public static Detection Analyze(string text, bool trusted = false, bool flaggedByPowerShell = false) {
        var reasons = new List<Localized>();
        var fileless = false;

        if (!trusted && !string.IsNullOrWhiteSpace(text)) {
            var score = 0;
            var refl = Reflective.IsMatch(text);
            var mem = Memory.IsMatch(text);

            if (refl) { score += 3; reasons.Add(Strings.ReasonAssemblyLoad); }
            if (mem) { score += 3; reasons.Add(Strings.ReasonMemoryApis); }
            if (Encoded.IsMatch(text)) { score += 2; reasons.Add(Strings.ReasonEncoded); }
            if (Stealth.IsMatch(text)) { score += 1; reasons.Add(Strings.ReasonStealth); }
            if (IsObfuscated(text)) { score += 2; reasons.Add(Strings.ReasonObfuscated); }
            if (flaggedByPowerShell) { score += 2; reasons.Add(Strings.ReasonFlaggedByPowerShell); }

            fileless = refl || mem || score >= 5;
        }

        return new Detection(fileless, reasons);
    }

    static bool IsObfuscated(string text) =>
        CharCast.Matches(text).Count >= 8
        || TickWord.Matches(text).Count >= 3
        || FormatObf.IsMatch(text)
        || BitObf.IsMatch(text);
}
