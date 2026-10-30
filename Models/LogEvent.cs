using EventvwrParser.Localization;

namespace EventvwrParser.Models;

public sealed class LogEvent {
    public DateTime Time { get; set; }
    public bool TimeApprox { get; set; }
    public int Id { get; set; }
    public int Seq { get; set; }
    public string Log { get; set; } = "";
    public Localized Group { get; set; } = Localized.Empty;
    public Localized Type { get; set; } = Localized.Empty;
    public Localized Summary { get; set; } = Localized.Empty;
    public Localized Details { get; set; } = Localized.Empty;
    public bool IsFileless { get; set; }
    public bool InInstance { get; set; }
    public bool IsNoise { get; set; }
    public int Related { get; set; }
    public List<Localized> Reasons { get; } = new();

    public string TimeText => (TimeApprox ? "~ " : "") + Strings.FullDate(Time);
    public string IdText => Id == 0 ? "—" : Id.ToString();
    public string RelatedText => Related > 0 ? $"+{Related}" : "";
    public string TypeText => Type.ToString();
    public string SummaryText => Summary.ToString();
}
