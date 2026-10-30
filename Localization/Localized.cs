namespace EventvwrParser.Localization;

public sealed class Localized {
    public static readonly Localized Empty = "";

    readonly string _es;
    readonly string _en;

    public Localized(string es, string en) {
        _es = es;
        _en = en;
    }

    public string Id => _es;

    public Localized Map(Func<string, string> f) => new(f(_es), f(_en));

    public override string ToString() => Strings.IsEnglish ? _en : _es;

    public static implicit operator Localized(string text) => new(text, text);
}
