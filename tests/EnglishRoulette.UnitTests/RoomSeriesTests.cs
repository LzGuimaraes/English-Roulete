using EnglishRoulette.Domain;
using EnglishRoulette.Domain.Multiplayer;
using EnglishRoulette.Domain.Sessions;

namespace EnglishRoulette.UnitTests;

public sealed class RoomSeriesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
    private static (LobbyState Room, Guid Host, Guid Guest) Completed()
    {
        var host = new LobbyMember(Guid.NewGuid(), "Host", "🦊", true, Now, Now);
        var guest = new LobbyMember(Guid.NewGuid(), "Guest", "🐸", true, Now, Now);
        var room = LobbyRoom.Join(LobbyRoom.Create(Guid.NewGuid(), "ABCDEFGH", 8, host, Now), guest, Now);
        var story = StoryFixture.Story();
        var match = MultiplayerGame.Create(story, story.Scenes.Select(scene => new SceneTiming(scene.Key, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(15))).ToArray(), room.Members, Now, Now);
        match = match with { Phase = MultiplayerPhase.StoryResult, SceneKey = "reunion", Players = [match.Players[0] with { Score = 380, CorrectAnswers = 2 }, match.Players[1] with { Score = 200, CorrectAnswers = 1 }] };
        return (room with { Match = match, StoryVersionId = story.Id, RouletteStartedAtUtc = Now }, host.PlayerId, guest.PlayerId);
    }

    [Fact]
    public void Next_KeepsRoomMembershipAndScores_ResetsReadinessAndIsIdempotent()
    {
        var (room, host, _) = Completed();
        var next = LobbyRoom.PrepareNext(room, host, room.StateVersion, room.Match!.Id, Now.AddSeconds(1));
        Assert.Equal(room.Id, next.Id); Assert.Equal(room.Code, next.Code); Assert.Equal(room.HostId, next.HostId);
        Assert.Null(next.Match); Assert.Null(next.StoryVersionId); Assert.Null(next.RouletteStartedAtUtc);
        Assert.Equal(1, next.CompletedGames);
        Assert.All(next.Members, member => Assert.False(member.Ready));
        Assert.Equal(new[] { 380, 200 }, next.SeriesScores!.Select(player => player.Score));
        var replay = LobbyRoom.PrepareNext(next, host, room.StateVersion, room.Match.Id, Now.AddSeconds(2));
        Assert.Equal(next.StateVersion, replay.StateVersion);
        Assert.Equal(next.CompletedGames, replay.CompletedGames);
        Assert.All(room.Members, member => Assert.True(member.Ready));
    }

    [Fact]
    public void Next_RequiresHostCompletedMatchAndCurrentVersion()
    {
        var (room, host, guest) = Completed();
        Assert.Equal("ROOM_FORBIDDEN", Assert.Throws<GameRuleException>(() => LobbyRoom.PrepareNext(room, guest, room.StateVersion, room.Match!.Id, Now)).Code);
        Assert.Equal("ROOM_FORBIDDEN", Assert.Throws<GameRuleException>(() => LobbyRoom.PrepareNext(room, Guid.NewGuid(), room.StateVersion, room.Match!.Id, Now)).Code);
        Assert.Equal("STATE_CONFLICT", Assert.Throws<GameRuleException>(() => LobbyRoom.PrepareNext(room, host, 0, room.Match!.Id, Now)).Code);
        Assert.Equal("INVALID_GAME_PHASE", Assert.Throws<GameRuleException>(() => LobbyRoom.PrepareNext(room, host, room.StateVersion, Guid.NewGuid(), Now)).Code);
        Assert.Equal("INVALID_GAME_PHASE", Assert.Throws<GameRuleException>(() => LobbyRoom.PrepareNext(room with { Match = room.Match! with { Phase = MultiplayerPhase.QuestionReveal } }, host, room.StateVersion, room.Match!.Id, Now)).Code);
    }

    [Fact]
    public void NewMatch_SeedsOnlyParticipantTotals_AndNewMemberStartsAtZero()
    {
        var (room, host, guest) = Completed();
        var next = LobbyRoom.PrepareNext(room, host, room.StateVersion, room.Match!.Id, Now);
        next = LobbyRoom.Leave(next, guest, Now);
        var newcomer = new LobbyMember(Guid.NewGuid(), "Newcomer", "🐼", false, Now, Now);
        next = LobbyRoom.Join(next, newcomer, Now);
        var story = StoryFixture.Story();
        var match = MultiplayerGame.Create(story, story.Scenes.Select(scene => new SceneTiming(scene.Key, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(15))).ToArray(), next.Members, Now, Now, startingScores: next.SeriesScores);
        Assert.Equal(380, match.Players[0].Score); Assert.Equal(380, match.Players[0].StartingScore);
        Assert.Equal(2, match.Players[0].CorrectAnswers);
        Assert.Equal(0, match.Players[1].Score); Assert.Equal(0, match.Players[1].StartingScore);
        Assert.Empty(match.Answers); Assert.Empty(match.Rounds); Assert.Empty(match.Votes);
        Assert.Throws<GameRuleException>(() => LobbyRoom.PrepareNext(next with { Match = match }, host, next.StateVersion, room.Match.Id, Now));
    }
}
