namespace EnglishRoulette.Contracts;

public sealed record GuestRequest(string Nickname, string Avatar);
public sealed record GuestView(Guid Id, string Nickname, string Avatar);
public sealed record CreateRoomRequest(int Capacity = 8, string Language = "en");
public sealed record JoinRoomRequest(string Code);
public sealed record ReadyRoomRequest(long ExpectedStateVersion, bool Ready);
public sealed record RoomSettingsRequest(long ExpectedStateVersion, int QuestionSeconds);
public sealed record ChatMessageRequest(Guid RequestId, string Message);
public sealed record NextRoomRequest(long ExpectedStateVersion, Guid CompletedMatchId);
public sealed record SpinRoomRequest(long ExpectedStateVersion);
public sealed record RoomMemberView(Guid PlayerId, string Nickname, string Avatar, bool Ready, bool Connected, int Score = 0);
public sealed record RoomView(Guid Id, string Code, int Capacity, Guid HostId, Guid ViewerId, long StateVersion,
    Guid? StoryVersionId, IReadOnlyList<RoomMemberView> Members, DateTimeOffset ServerNowUtc,
    DateTimeOffset? RouletteStartedAtUtc = null, MultiplayerMatchView? Match = null, int GameNumber = 1, string Language = "en",
    int QuestionSeconds = 15, IReadOnlyList<RoomChatMessageView>? ChatMessages = null);

public sealed record MatchCommandRequest(Guid RequestId, long ExpectedStateVersion, Guid SceneInstanceId, string Kind, string? Key = null);
public sealed record MatchPlayerView(Guid Id, string Nickname, string Avatar, int Score, int CorrectAnswers,
    bool Connected, bool AudioReady, bool Answered, bool Voted, bool Left);
public sealed record MatchResultView(Guid PlayerId, bool Correct, int Points);
public sealed record MatchDecisionView(string SceneTitle, string Choice, bool Tied, bool NoVotes);
public sealed record MatchStandingView(Guid PlayerId, int? Position, int Score, int MatchPoints, bool Left);
public sealed record RoomChatMessageView(Guid Id, Guid PlayerId, string Nickname, string Avatar, string Text, DateTimeOffset SentAtUtc);
public sealed record MultiplayerMatchView(Guid Id, Guid SceneInstanceId, AdventureView Scene,
    IReadOnlyList<MatchPlayerView> Players, bool MyAudioReady, bool MyAnswerAccepted, string? MyOptionKey,
    bool MyVoteAccepted, string? MyVoteKey, IReadOnlyList<MatchResultView> RoundResults,
    IReadOnlyList<Guid> Winners, MatchDecisionView? LastDecision, string? CancellationReason, IReadOnlyList<MatchStandingView>? Standings = null);
