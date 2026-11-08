using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using EnglishRoulette.Domain.Sessions;

namespace EnglishRoulette.Infrastructure.Persistence;

public static class CheckpointJson
{
    private static readonly JsonSerializerOptions Options = CreateOptions();
    public static string Serialize(SoloAdventureCheckpoint checkpoint) => JsonSerializer.Serialize(checkpoint, Options);
    public static SoloAdventureCheckpoint Deserialize(string json) =>
        JsonSerializer.Deserialize<SoloAdventureCheckpoint>(json, Options)
        ?? throw new InvalidOperationException("The stored session checkpoint is missing.");

    private static JsonSerializerOptions CreateOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            if (info.Type != typeof(SoloAdventureCommand)) { return; }
            info.PolymorphismOptions = new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = "$command",
                DerivedTypes =
                {
                    new(typeof(StartSoloNarration), "start"), new(typeof(SubmitSoloAnswer), "answer"),
                    new(typeof(UseSoloHint), "hint"), new(typeof(ContinueSoloAdventure), "continue"), new(typeof(ChooseSoloPath), "choose")
                }
            };
        });
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = resolver,
            AllowOutOfOrderMetadataProperties = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
