using System.Text.RegularExpressions;

namespace EnglishRoulette.Application.Presentation;

public sealed partial record CatalogAppearance
{
    public string Primary { get; init; } = "#6D28D9";
    public string Accent { get; init; } = "#F97316";
    public string Background { get; init; } = "#F5F3FF";
    public string Surface { get; init; } = "#FFFFFF";
    public string Text { get; init; } = "#231942";
    public string Font { get; init; } = "rounded";
    public string Motion { get; init; } = "gentle";

    public bool IsValid() => new[] { Primary, Accent, Background, Surface, Text }.All(color => color is not null && HexColor().IsMatch(color)) &&
        Font is "rounded" or "classic" or "modern" && Motion is "none" or "gentle" &&
        Contrast(Text, Background) >= 4.5 && Contrast(Text, Surface) >= 4.5;

    private static double Contrast(string first, string second)
    {
        static double Light(string color)
        {
            double Channel(int start)
            {
                var value = Convert.ToInt32(color.Substring(start, 2), 16) / 255d;
                return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
            }
            return .2126 * Channel(1) + .7152 * Channel(3) + .0722 * Channel(5);
        }
        var a = Light(first); var b = Light(second);
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }
    [GeneratedRegex("^#[a-fA-F0-9]{6}$")]
    private static partial Regex HexColor();
}
