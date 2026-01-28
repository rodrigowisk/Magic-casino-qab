using System.Text.Json.Serialization;

namespace Magic_casino.Models.Integrations
{
    // --- REQUEST ---
    public class VelanaDepositRequest
    {
        [JsonPropertyName("amount")]
        public int Amount { get; set; } // Em centavos

        [JsonPropertyName("paymentMethod")]
        public string PaymentMethod { get; set; } = "pix";

        [JsonPropertyName("customer")]
        public VelanaCustomer Customer { get; set; }

        [JsonPropertyName("postbackUrl")]
        public string PostbackUrl { get; set; }

        [JsonPropertyName("items")]
        public List<VelanaItem> Items { get; set; }
    }

    public class VelanaItem
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("unitPrice")]
        public int UnitPrice { get; set; }

        [JsonPropertyName("tangible")]
        public bool Tangible { get; set; } = false;
    }

    public class VelanaCustomer
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonPropertyName("document")]
        public VelanaDocument Document { get; set; }
    }

    public class VelanaDocument
    {
        [JsonPropertyName("number")]
        public string Number { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } // "cpf" ou "cnpj"
    }

    // --- RESPONSE ---
    public class VelanaTransactionResponse
    {
        public long Id { get; set; }
        public string Status { get; set; }
        public int Amount { get; set; }
        public VelanaPixData Pix { get; set; }
    }

    public class VelanaPixData
    {
        public string QrCode { get; set; }
        public string QrCodeUrl { get; set; }
    }
}