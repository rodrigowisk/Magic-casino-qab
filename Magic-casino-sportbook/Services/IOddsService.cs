using System.Threading.Tasks;

namespace Magic_casino_sportbook.Services
{
    public interface IOddsService
    {
        // O contrato: Todo serviço deve saber fazer isso
        Task SyncBaseOddsToDatabase();
        Task SyncMissingImages();
    }
}