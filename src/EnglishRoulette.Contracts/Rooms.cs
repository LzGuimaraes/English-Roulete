namespace EnglishRoulette.Contracts;

public sealed record GuestRequest(string Nickname, string Avatar);
public sealed record GuestView(Guid Id, string Nickname, string Avatar);
public sealed record CreateRoomRequest(int Capacity = 8);
public sealed record JoinRoomRequest(string Code);
public sealed record ReadyRoomRequest(long ExpectedStateVersion, bool Ready);
public sealed record RoomStoryRequest(long ExpectedStateVersion, Guid StoryVersionId);
public sealed record RoomMemberView(Guid PlayerId, string Nickname, string Avatar, bool Ready, bool Connected);
public sealed record RoomView(Guid Id, string Code, int Capacity, Guid HostId, Guid ViewerId, long StateVersion,
    Guid? StoryVersionId, IReadOnlyList<RoomMemberView> Members, DateTimeOffset ServerNowUtc);
