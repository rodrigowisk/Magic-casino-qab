using Magic_casino_tournament.Models;
using Magic_casino_tournament.Controllers; // Necessário para ver o PlaceTournamentBetRequest

namespace Magic_casino_tournament.Services
{
    public interface ITournamentService
    {
        Task<List<Tournament>> GetActiveTournamentsAsync(string? userId);

        // Retorno 'object?' permite enviar o JSON enriquecido com Rank e Saldo
        Task<object?> GetTournamentByIdAsync(int id, string? userId);

        Task<Tournament> CreateTournamentAsync(Tournament tournament);

        Task<string> JoinTournamentAsync(int tournamentId, string userId, string token);

        // Método legado (1 aposta)
        Task<string> PlaceBetAsync(int tournamentId, string userId, TournamentBet bet);

        // ✅ Método de Lote (Batch) - Obrigatório para o novo Cupom
        Task<(bool Success, string Message)> PlaceBatchBetsAsync(int tournamentId, string userId, PlaceTournamentBetRequest request);

        Task<List<TournamentParticipant>> GetTournamentRankingAsync(int tournamentId);
        Task<decimal> GetUserFantasyBalance(int tournamentId, string userId);
        Task DeductFantasyBalance(int tournamentId, string userId, decimal amount);
    }
}