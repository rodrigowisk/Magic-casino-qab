using Magic_casino_tournament.Models;
using Magic_casino_tournament.Controllers;

namespace Magic_casino_tournament.Services
{
    public interface ITournamentService
    {
        Task<List<Tournament>> GetActiveTournamentsAsync(string? userId);
        Task<object?> GetTournamentByIdAsync(int id, string? userId);
        Task<Tournament> CreateTournamentAsync(Tournament tournament);

        Task<string> JoinTournamentAsync(int tournamentId, string userId, string token, string userName, string avatar);

        Task<string> PlaceBetAsync(int tournamentId, string userId, TournamentBet bet);

        Task<(bool Success, string Message)> PlaceBatchBetsAsync(int tournamentId, string userId, PlaceTournamentBetRequest request);

        Task<List<TournamentBet>> GetUserBetsAsync(int tournamentId, string userId);

        // ✅ CORREÇÃO: Mantive apenas esta versão que retorna o DTO novo
        Task<List<TournamentRankingDto>> GetTournamentRankingAsync(int tournamentId);

        Task<decimal> GetUserFantasyBalance(int tournamentId, string userId);
        Task DeductFantasyBalance(int tournamentId, string userId, decimal amount);

        Task ProcessFinishedTournamentsAsync();

        Task ProcessGameResultAsync(string gameId, string score);

        Task<bool> ToggleFavoriteAsync(int tournamentId, string userId);

        Task<object> GetUserHistoryAsync(string userId);
    }
}