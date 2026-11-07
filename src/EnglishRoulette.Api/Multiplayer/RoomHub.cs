using System.Security.Claims;
using EnglishRoulette.Application.Multiplayer;
using Microsoft.AspNetCore.SignalR;

namespace EnglishRoulette.Api.Multiplayer;

public sealed class RoomHub(ILobbyStore lobbies) : Hub
{
    public static string Group(Guid id) => "room:" + id;
    public async Task JoinRoom(Guid id)
    {
        var actor = Guid.Parse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new HubException("Identifique-se para entrar na sala."));
        if (await lobbies.ReadAsync(id, actor, Context.ConnectionAborted) is null)
        { throw new HubException("Você não participa desta sala."); }
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(id), Context.ConnectionAborted);
    }
}
