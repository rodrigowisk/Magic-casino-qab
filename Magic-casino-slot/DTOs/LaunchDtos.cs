using System.Text.Json.Serialization;

namespace Magic_casino_slot.DTOs
{
    // O que o Frontend manda (igual ao seu Kotlin GameLaunchRequest)
    public class GameLaunchRequest
    {
        [JsonPropertyName("user_code")]
        public string UserCode { get; set; } = string.Empty;

        [JsonPropertyName("provider_code")]
        public string ProviderCode { get; set; } = string.Empty;

        [JsonPropertyName("game_code")]
        public string GameCode { get; set; } = string.Empty;
    }

    // O que respondemos para o Frontend
    public class GameLaunchResponse
    {
        [JsonPropertyName("launch_url")]
        public string LaunchUrl { get; set; } = string.Empty;
    }

    // (INTERNO) Para ler a resposta da Fiver
    public class FiverLaunchResponse
    {
        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("msg")]
        public string? Msg { get; set; }

        [JsonPropertyName("launch_url")]
        public string? LaunchUrl { get; set; }
    }
}