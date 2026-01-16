using System.Threading.Tasks;

namespace Magic_casino_sportbook.Services
{
    public interface IOddsService
    {
        // 1. Calendário (Apenas eventos, times e ligas - Roda a cada 6h)
        Task SyncEventsSchedule();

        // 2. Odds Pré-Jogo (Apenas odds de jogos futuros - Roda a cada 20min)
        Task SyncPrematchOdds();

        // 3. Ao Vivo (Placar e Odds em tempo real - Roda a cada 10s)
        Task SyncLiveFeed();

        // Auxiliar (Baixa imagens que faltaram)
        Task SyncMissingImages();

        // Mantemos esse para compatibilidade se ainda estiver usando em algum lugar antigo, 
        // mas o ideal é parar de usar o 'SyncBaseOddsToDatabase' genérico.
        Task SyncBaseOddsToDatabase();
    }
}