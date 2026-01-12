using System.Text.Json.Serialization;

namespace Magic_casino_slot.DTOs
{
    // O que a Fiver manda para você
    public class FiverRequestDto
    {
        [JsonPropertyName("method")]
        public string Method { get; set; } // "user_balance" ou "transaction"

        [JsonPropertyName("user_code")]
        public string UserCode { get; set; } // O ID ou Email do seu usuário

        [JsonPropertyName("agent_secret")]
        public string AgentSecret { get; set; } // Para você conferir se é a Fiver mesmo

        [JsonPropertyName("slot")]
        public FiverSlotData Slot { get; set; } // Detalhes da jogada (só vem no método transaction)
    }

    public class FiverSlotData
    {
        [JsonPropertyName("provider_code")]
        public string ProviderCode { get; set; }

        [JsonPropertyName("game_code")]
        public string GameCode { get; set; }

        [JsonPropertyName("bet_money")]
        public decimal BetMoney { get; set; } // Valor apostado

        [JsonPropertyName("win_money")]
        public decimal WinMoney { get; set; } // Valor ganho

        [JsonPropertyName("txn_id")]
        public string TxnId { get; set; } // ID Único da rodada (salve isso no banco!)
    }
}