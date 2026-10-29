namespace EventvwrParser.Models;

public sealed class LogEvent {
    public DateTime Time { get; set; }
    public bool TimeApprox { get; set; }
    public int Id { get; set; }
    public int Seq { get; set; }
    public string Log { get; set; } = "";
    public string Group { get; set; } = "";
    public string Type { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Details { get; set; } = "";
    public bool IsFileless { get; set; }
    public bool IsAntiForensic { get; set; }
    public bool InInstance { get; set; }
    public bool IsNoise { get; set; }
    public int Related { get; set; }
    public List<string> Reasons { get; } = new();

    public string TimeText => (TimeApprox ? "~ " : "") + Time.ToString("dd/MM/yy HH:mm:ss");
    public string IdText => Id == 0 ? "—" : Id.ToString();
    public string RelatedText => Related > 0 ? $"+{Related}" : "";
}
