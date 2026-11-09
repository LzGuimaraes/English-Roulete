using System.Text.Json;
using System.Text.Json.Nodes;
using EnglishRoulette.Domain;
using EnglishRoulette.Domain.Multiplayer;

namespace EnglishRoulette.UnitTests;

public sealed class LobbyRoomTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 7, 1, 0, 0, TimeSpan.Zero);
    private static LobbyMember Member(string name) => new(Guid.NewGuid(), name, "🦊", false, Start, Start);
    [Fact]
    public void Language_OldCheckpointsDefaultToEnglish_AndNewRoomsValidateLanguage()
    {
        var host = Member("Host");
        var room = LobbyRoom.Create(Guid.NewGuid(), "ABCDEFGH", 2, host, Start);
        var legacy = JsonSerializer.SerializeToNode(room)!.AsObject();
        legacy.Remove("Language");
        Assert.Equal("en", legacy.Deserialize<LobbyState>()!.Language);
        var portuguese = LobbyRoom.Create(Guid.NewGuid(), "ABCDEFGH", 2, host, Start, "pt-BR");
        Assert.Equal("pt-BR", JsonSerializer.Deserialize<LobbyState>(JsonSerializer.Serialize(portuguese))!.Language);
        Assert.Equal("INVALID_LANGUAGE", Assert.Throws<GameRuleException>(() =>
            LobbyRoom.Create(Guid.NewGuid(), "ABCDEFGH", 2, host, Start, "es")).Code);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(9)]
    public void Create_RejectsCapacityOutsideTwoToEight(int capacity) =>
        Assert.Throws<GameRuleException>(() => LobbyRoom.Create(Guid.NewGuid(), "ABCDEFGH", capacity, Member("Host"), Start));
    [Fact]
    public void Join_IsIdempotentForSamePlayer_AndRejectsFullRoom()
    {
        var host = Member("Host"); var guest = Member("Guest");
        var room = LobbyRoom.Create(Guid.NewGuid(), "ABCDEFGH", 2, host, Start);
        room = LobbyRoom.Join(room, guest, Start);
        var replay = LobbyRoom.Join(room, guest, Start);
        Assert.Equal(2, replay.Members.Count); Assert.Equal(room.StateVersion, replay.StateVersion);
        Assert.Equal("ROOM_FULL", Assert.Throws<GameRuleException>(() => LobbyRoom.Join(room, Member("Third"), Start)).Code);
    }
    [Fact]
    public void Join_RequiresDistinctNicknameInRoom()
    {
        var room = LobbyRoom.Create(Guid.NewGuid(), "ABCDEFGH", 8, Member("Alice"), Start);
        Assert.Equal("NAME_TAKEN", Assert.Throws<GameRuleException>(() => LobbyRoom.Join(room, Member("ALICE"), Start)).Code);
    }
    [Fact]
    public void Ready_RequiresMembershipAndCurrentVersion()
    {
        var host = Member("Host"); var room = LobbyRoom.Create(Guid.NewGuid(), "ABCDEFGH", 8, host, Start);
        Assert.Equal("ROOM_FORBIDDEN", Assert.Throws<GameRuleException>(() => LobbyRoom.SetReady(room, Guid.NewGuid(), 1, true, Start)).Code);
        var updated = LobbyRoom.SetReady(room, host.PlayerId, 1, true, Start);
        Assert.True(updated.Members[0].Ready); Assert.False(room.Members[0].Ready);
        Assert.Equal("STATE_CONFLICT", Assert.Throws<GameRuleException>(() => LobbyRoom.SetReady(updated, host.PlayerId, 1, false, Start)).Code);
    }
    [Fact]
    public void Spin_RequiresHostAndAllConnectedPlayersReady_AndPersistsOneResult()
    {
        var host = Member("Host"); var guest = Member("Guest");
        var room = LobbyRoom.Create(Guid.NewGuid(), "ABCDEFGH", 8, host, Start);
        Assert.Equal("ROOM_NOT_READY", Assert.Throws<GameRuleException>(() => LobbyRoom.Spin(room, host.PlayerId, room.StateVersion, Guid.NewGuid(), Start)).Code);
        room = LobbyRoom.Join(room, guest, Start);
        Assert.Equal("ROOM_FORBIDDEN", Assert.Throws<GameRuleException>(() => LobbyRoom.Spin(room, guest.PlayerId, room.StateVersion, Guid.NewGuid(), Start)).Code);
        room = LobbyRoom.SetReady(room, host.PlayerId, room.StateVersion, true, Start);
        Assert.Equal("ROOM_NOT_READY", Assert.Throws<GameRuleException>(() => LobbyRoom.Spin(room, host.PlayerId, room.StateVersion, Guid.NewGuid(), Start)).Code);
        room = LobbyRoom.SetReady(room, guest.PlayerId, room.StateVersion, true, Start);
        Assert.Equal("ROOM_NOT_READY", Assert.Throws<GameRuleException>(() => LobbyRoom.Spin(room, host.PlayerId, room.StateVersion, Guid.NewGuid(), Start.AddSeconds(30))).Code);
        var storyId = Guid.NewGuid();
        var selected = LobbyRoom.Spin(room, host.PlayerId, room.StateVersion, storyId, Start);
        Assert.Equal(storyId, selected.StoryVersionId);
        Assert.Equal(Start, selected.RouletteStartedAtUtc);
        Assert.Equal(room.StateVersion + 1, selected.StateVersion);
        Assert.All(selected.Members, member => Assert.True(member.Ready));
        Assert.Equal("ROULETTE_ALREADY_DRAWN", Assert.Throws<GameRuleException>(() => LobbyRoom.Spin(selected, host.PlayerId, selected.StateVersion, Guid.NewGuid(), Start)).Code);
        Assert.Equal("STATE_CONFLICT", Assert.Throws<GameRuleException>(() => LobbyRoom.Spin(selected, host.PlayerId, room.StateVersion, Guid.NewGuid(), Start)).Code);
    }
    [Fact]
    public void ReconnectGrace_ExpiresAtNinetySeconds_AndTransfersHost()
    {
        var host = Member("Host"); var guest = Member("Guest") with { JoinedAtUtc = Start.AddSeconds(1) };
        var room = LobbyRoom.Join(LobbyRoom.Create(Guid.NewGuid(), "ABCDEFGH", 8, host, Start), guest, Start);
        room = LobbyRoom.Touch(room, guest.PlayerId, Start.AddSeconds(80));
        Assert.Equal(2, LobbyRoom.Expire(room, Start.AddSeconds(89)).Members.Count);
        var expired = LobbyRoom.Expire(room, Start.AddSeconds(90));
        Assert.Single(expired.Members); Assert.Equal(guest.PlayerId, expired.HostId);
        Assert.Equal("Closed", LobbyRoom.Leave(expired, guest.PlayerId, Start.AddSeconds(91)).Status);
    }
    [Fact]
    public void Leadership_PrefersConnectedMemberOverOlderDisconnectedMember()
    {
        var host = Member("Host"); var older = Member("Older"); var connected = Member("Connected") with { JoinedAtUtc = Start.AddSeconds(1) };
        var room = LobbyRoom.Create(Guid.NewGuid(), "ABCDEFGH", 8, host, Start);
        room = LobbyRoom.Join(LobbyRoom.Join(room, older, Start), connected, Start);
        room = LobbyRoom.Touch(room, connected.PlayerId, Start.AddSeconds(40));
        Assert.Equal(connected.PlayerId, LobbyRoom.Leave(room, host.PlayerId, Start.AddSeconds(45)).HostId);
    }
    [Fact]
    public void InactiveLobby_Expires_AndProfileUsesBoundedCatalog()
    {
        var room = LobbyRoom.Create(Guid.NewGuid(), "ABCDEFGH", 8, Member("Host"), Start);
        var expired = LobbyRoom.Expire(room, Start.AddMinutes(30));
        Assert.Equal("Expired", expired.Status); Assert.Empty(expired.Members);
        Assert.Throws<GameRuleException>(() => LobbyRoom.NormalizeName("ab"));
        Assert.Equal("Alice", LobbyRoom.NormalizeName("  Alice  "));
        Assert.Throws<GameRuleException>(() => LobbyRoom.ValidateAvatar("<script>"));
    }
}
