using System.Text.Json.Nodes;
using EnglishRoulette.Domain.Challenges;
using EnglishRoulette.Domain.Sessions;
using EnglishRoulette.Domain.Stories;
using EnglishRoulette.Infrastructure.Persistence;

namespace EnglishRoulette.IntegrationTests;

public sealed class LegacyAssistanceCheckpointTests
{
    [Fact]
    public void MissingAssistanceFields_RestoresOldJsonWithoutInventingScoresOrBreakingAnswers()
    {
        var now = new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
        var player = Guid.NewGuid(); var version = Guid.NewGuid();
        var story = new StoryVersion(version, "Legacy story", PublicationStatus.Published, "end",
            [new(version, "end", "Mia opens a door.", new MultipleChoiceChallenge(Guid.NewGuid(), "What does Mia open?",
                [new("a", "A door"), new("b", "A book")], "a", "Mia opens a door."), true, [])]);
        var session = new SoloAdventureSession(Guid.NewGuid(), player, story,
            [new("end", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(15))]);
        var start = new StartSoloNarration(Guid.NewGuid(), 1);
        session.Execute(player, start, now);
        var json = JsonNode.Parse(CheckpointJson.Serialize(session.Capture()))!;
        foreach (var key in new[] { "scoringRules", "hints", "roundScores" }) { json.AsObject().Remove(key); }
        foreach (var scene in json["story"]!["scenes"]!.AsArray()) { scene!.AsObject().Remove("support"); }
        foreach (var receipt in json["receipts"]!.AsArray())
        {
            foreach (var key in new[] { "sceneInstanceId", "scoringRules", "usedHintKeys", "roundScores" })
            { receipt!["snapshot"]!.AsObject().Remove(key); }
        }
        var restored = SoloAdventureSession.Restore(CheckpointJson.Deserialize(json.ToJsonString()));
        Assert.Equal(2, restored.Execute(player, start, now).StateVersion);
        var open = restored.GetSnapshot(player, now.AddSeconds(30));
        Assert.Null(open.ScoringRules); Assert.Empty(open.UsedHintKeys!); Assert.Empty(open.RoundScores!);
        Assert.Equal(now.AddSeconds(45), open.PhaseDeadlineUtc);
        var answered = restored.Execute(player, new SubmitSoloAnswer(Guid.NewGuid(), open.StateVersion, open.Question!.InstanceId, "a"), now.AddSeconds(31));
        Assert.True(answered.Answer!.IsCorrect);
        Assert.Empty(answered.RoundScores!);
    }
}
