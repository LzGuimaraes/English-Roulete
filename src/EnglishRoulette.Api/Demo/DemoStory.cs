using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using EnglishRoulette.Domain.Stories;

namespace EnglishRoulette.Api.Demo;

internal static class DemoStory
{
    public static StoryVersion Create()
    {
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("EnglishRoulette.Api.Demo.story.json")
            ?? throw new InvalidOperationException("The demo story definition is missing.");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return (JsonSerializer.Deserialize<StoryDefinition>(resource, options)
            ?? throw new InvalidOperationException("The demo story definition is invalid.")).Restore();
    }

    public static (string Title, int Number) Describe(string key) => key switch
    {
        "platform" => ("O encontro na estação", 1),
        "bookshop" => ("A pista na livraria", 2),
        "cafe" => ("Um encontro à beira do rio", 3),
        "library" => ("Uma história para contar", 3),
        _ => throw new ArgumentOutOfRangeException(nameof(key))
    };
}
