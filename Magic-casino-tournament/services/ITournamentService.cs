using Magic_casino_tournament.Models;

namespace Magic_casino_tournament.Services
{
    public interface ITournamentService
    {
        Task<List<Tournament>> GetActiveTournamentsAsync();
        Task<Tournament?> CreateTournamentAsync(Tournament tournament);
        Task<string> JoinTournamentAsync(int tournamentId, string userId);
    }
}