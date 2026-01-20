using System.Text.Json.Serialization;

// 🟢 CORREÇÃO: Mudamos o namespace para DTOs.Live para satisfazer o Worker e o Service
namespace Magic_casino_sportbook.DTOs.Live
{
    // Resposta Principal
    public class B365LiveResponse
    {
        [JsonPropertyName("success")]
        public int Success { get; set; }

        [JsonPropertyName("results")]
        public List<List<B365Packet>> Results { get; set; } = new();
    }

    // O Pacote Genérico (Pode ser Evento, Estatística, etc)
    public class B365Packet
    {
        // "EV" (Event), "ST" (Stats), etc.
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        // --- DADOS DO EVENTO (Type = EV) ---

        // FI = FixtureId (ID do Jogo)
        [JsonPropertyName("FI")]
        public string FixtureId { get; set; } = string.Empty;

        // ID = ID interno do evento na resposta
        [JsonPropertyName("ID")]
        public string Id { get; set; } = string.Empty;

        // SS = Score String (Placar "2-1")
        [JsonPropertyName("SS")]
        public string ScoreString { get; set; } = string.Empty;

        // TM = Time (Minutos "90")
        [JsonPropertyName("TM")]
        public string Time { get; set; } = string.Empty;

        // TS = Time Status ("0"=Prematch, "1"=Live, "3"=Ended)
        [JsonPropertyName("TS")]
        public string Status { get; set; } = string.Empty;

        // NA = Name (Nome dos times ou da estatística)
        [JsonPropertyName("NA")]
        public string Name { get; set; } = string.Empty;

        // --- DADOS DE ODDS (Se houver) ---
        [JsonPropertyName("OD")]
        public string Odds { get; set; } = string.Empty;

        [JsonPropertyName("HA")]
        public string Handicap { get; set; } = string.Empty;

        // TU = Turn/Update Time (Timestamp)
        [JsonPropertyName("TU")]
        public string UpdateTime { get; set; } = string.Empty;
    }
}