using EnglishRoulette.Contracts;

namespace EnglishRoulette.Web.Services;

public static class ThemePalette
{
    public static StoryAppearance Arcade => new() { Primary = "#A78BFA", Accent = "#FBBF6A", Background = "#100D20", Surface = "#211B35", Text = "#F5F0FF" };
    public static StoryAppearance Horror => new() { Primary = "#F29A94", Accent = "#D45555", Background = "#100B10", Surface = "#21161E", Text = "#F6EBED", Font = "classic" };
    public static bool IsHorror(string? genre) => genre?.Trim().ToLowerInvariant() is "terror" or "horror" or "terror psicológico" or "psychological horror";
    public static StoryAppearance Preset(string key) => key switch
    {
        "candy" => new() { Primary = "#BE185D", Accent = "#7C3AED", Background = "#FFF1F7", Surface = "#FFFFFF", Text = "#3B1532" },
        "ocean" => new() { Primary = "#0369A1", Accent = "#F97316", Background = "#ECFEFF", Surface = "#FFFFFF", Text = "#123147" },
        "jungle" => new() { Primary = "#047857", Accent = "#C026D3", Background = "#F0FDF4", Surface = "#FFFFFF", Text = "#143A28" },
        _ => new()
    };
    public static string Style(StoryAppearance? theme)
    {
        theme ??= new();
        static string Color(string value, string fallback) => value is { Length: 7 } && value[0] == '#' && value.Skip(1).All(char.IsAsciiHexDigit) ? value : fallback;
        var primary = Color(theme.Primary, "#6D28D9");
        var font = theme.Font == "classic" ? "Georgia,serif" : theme.Font == "modern" ? "Arial,Helvetica,sans-serif" : "ui-rounded,system-ui,sans-serif";
        var red = Convert.ToInt32(primary.Substring(1, 2), 16); var green = Convert.ToInt32(primary.Substring(3, 2), 16); var blue = Convert.ToInt32(primary.Substring(5, 2), 16);
        var foreground = .299 * red + .587 * green + .114 * blue > 150 ? "#151025" : "#FFFFFF";
        return $"--primary:{primary};--green:{primary};--orange:{Color(theme.Accent, "#F97316")};--accent:{Color(theme.Accent, "#F97316")};--page:{Color(theme.Background, "#F5F3FF")};--surface:{Color(theme.Surface, "#FFFFFF")};--ink:{Color(theme.Text, "#231942")};--button-ink:{foreground};--story-font:{font};";
    }
}
