namespace EnglishRoulette.Contracts;

public sealed class StoryAppearance
{
    public string Primary { get; set; } = "#6D28D9";
    public string Accent { get; set; } = "#F97316";
    public string Background { get; set; } = "#F5F3FF";
    public string Surface { get; set; } = "#FFFFFF";
    public string Text { get; set; } = "#231942";
    public string Font { get; set; } = "rounded";
    public string Motion { get; set; } = "gentle";
}
public sealed record SceneVisualView(string Url, string AltText, bool Animated);
