using Microsoft.AspNetCore.SignalR;

// ✅ O namespace deve refletir a pasta onde o arquivo está (Hubs)
namespace Magic_casino_tournament.Hubs
{
    public class TournamentHub : Hub
    {
        // O Front-end chama este método assim que entra na tela do torneio
        // Isso garante que ele só receba atualizações DO TORNEIO QUE ESTÁ VENDO
        public async Task JoinGroup(string tournamentId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, tournamentId);
        }

        // (Opcional) Método para sair do grupo quando o usuário muda de rota
        public async Task LeaveGroup(string tournamentId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, tournamentId);
        }
    }
}