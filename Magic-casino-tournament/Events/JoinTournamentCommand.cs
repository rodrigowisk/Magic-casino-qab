namespace Magic_casino_tournament.Events
{
    public class JoinTournamentCommand
    {
        public int TournamentId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
    }
}