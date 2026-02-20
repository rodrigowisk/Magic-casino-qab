using Microsoft.AspNetCore.SignalR;

namespace Magic_casino_tournament.Hubs
{
    public class TournamentHub : Hub
    {
        // ✅ 1. Sincronização de Relógio (NOVO)
        // Disparado automaticamente quando o frontend conecta.
        // Envia a hora do servidor para corrigir o atraso/adiantamento do relógio do usuário.
        public override async Task OnConnectedAsync()
        {
            await Clients.Caller.SendAsync("SyncServerTime", DateTime.UtcNow);
            await base.OnConnectedAsync();
        }


        public async Task JoinLobby()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Lobby");
        }

        public async Task LeaveLobby()
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Lobby");
        }


        // ---------------------------------------------------------
        // 📢 CANAIS PÚBLICOS (Ranking do Torneio)
        // ---------------------------------------------------------

        // O Frontend chama isso para receber atualizações do Ranking
        public async Task JoinGroup(string tournamentId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, tournamentId);
        }

        public async Task LeaveGroup(string tournamentId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, tournamentId);
        }

        // ---------------------------------------------------------
        // 🔒 CANAIS PRIVADOS (Minhas Apostas)
        // ---------------------------------------------------------

        // O Frontend chama para ouvir atualizações PESSOAIS
        // Ex: Quando a aposta dele bate ou o saldo muda.
        public async Task JoinUserGroup(string userId)
        {
            // Cria um nome de grupo único, tipo "user_45"
            string groupName = $"user_{userId}";

            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

            // (Opcional) Log para debug
            // Console.WriteLine($"🔒 Connection {Context.ConnectionId} joined {groupName}");
        }
    }
}