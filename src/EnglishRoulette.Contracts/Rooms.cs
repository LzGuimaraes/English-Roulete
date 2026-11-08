namespace EnglishRoulette.Contracts;

public sealed record GuestRequest(string Nickname, string Avatar);
public sealed record GuestView(Guid Id, string Nickname, string Avatar);
public sealed record CreateRoomRequest(int Capacity = 8);
public sealed record JoinRoomRequest(string Code);
public sealed record ReadyRoomRequest(long ExpectedStateVersion, bool Ready);
public sealed record SpinRoomRequest(long ExpectedStateVersion);
public sealed record RoomMemberView(Guid PlayerId, string Nickname, string Avatar, bool Ready, bool Connected);
public sealed record RoomView(Guid Id, string Code, int Capacity, Guid HostId, Guid ViewerId, long StateVersion,
    Guid? StoryVersionId, IReadOnlyList<RoomMemberView> Members, DateTimeOffset ServerNowUtc,
    DateTimeOffset? RouletteStartedAtUtc = null, MultiplayerMatchView? Match = null);

public sealed record MatchCommandRequest(Guid RequestId, long ExpectedStateVersion, Guid SceneInstanceId, string Kind, string? Key = null);
public sealed record MatchPlayerView(Guid Id, string Nickname, string Avatar, int Score, int CorrectAnswers,
    bool Connected, bool AudioReady, bool Answered, bool Voted, bool Left);
public sealed record MatchResultView(Guid PlayerId, bool Correct, int Points);
public sealed record MatchDecisionView(string SceneTitle, string Choice, bool Tied, bool NoVotes);
public sealed record MultiplayerMatchView(Guid Id, Guid SceneInstanceId, AdventureView Scene,
    IReadOnlyList<MatchPlayerView> Players, bool MyAudioReady, bool MyAnswerAccepted, string? MyOptionKey,
    bool MyVoteAccepted, string? MyVoteKey, IReadOnlyList<MatchResultView> RoundResults,
    IReadOnlyList<Guid> Winners, MatchDecisionView? LastDecision, string? CancellationReason);
