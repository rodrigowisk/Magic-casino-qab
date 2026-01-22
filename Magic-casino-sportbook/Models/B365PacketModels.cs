using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Magic_casino_sportbook.Models
{
    public class B365LiveResponse
    {
        [JsonPropertyName("success")]
        public int Success { get; set; }

        [JsonPropertyName("results")]
        public List<List<B365Packet>>? Results { get; set; }

        [JsonPropertyName("stats")]
        public Stats? Stats { get; set; }
    }

    public class B365Packet
    {
        // --- IDENTIFICADORES ---
        [JsonPropertyName("type")]
        public string? Type { get; set; } // EV, ST, PA...

        [JsonPropertyName("ID")]
        [JsonConverter(typeof(StringIntConverter))]
        public string? Id { get; set; }

        [JsonPropertyName("FI")]
        [JsonConverter(typeof(StringIntConverter))]
        public string? Fi { get; set; }

        // --- DADOS DO JOGO (EV) ---
        // Aqui está o segredo: Mapeando explicitamente para as siglas MAIÚSCULAS do JSON

        [JsonPropertyName("NA")] public string? Na { get; set; } // Nome (Name)
        [JsonPropertyName("SS")] public string? Ss { get; set; } // Placar (Soccer Score)
        [JsonPropertyName("TM")] public string? Tm { get; set; } // Tempo (Time Minute)
        [JsonPropertyName("TS")] public string? Ts { get; set; } // Segundos
        [JsonPropertyName("TT")] public string? Tt { get; set; } // Timer Ticking (1=Rodando)
        [JsonPropertyName("TU")] public string? Tu { get; set; } // Time Updated (Data)
        [JsonPropertyName("time_status")]                        // Status jogo

        public string? TimeStatus { get; set; }
        // --- DADOS DE LOG/TEXTO (ST) ---
        [JsonPropertyName("LA")] public string? La { get; set; } // Log Action (Onde pegamos o tempo real)

        // --- ODDS (PA) ---
        [JsonPropertyName("OD")] public string? Od { get; set; } // Valor da Odd
        [JsonPropertyName("N2")] public string? N2 { get; set; } // 1, X, 2
        [JsonPropertyName("HA")] public string? Ha { get; set; } // Handicap / Header

        // --- OUTROS CAMPOS (Evita erros de parsing) ---
        [JsonPropertyName("time")] public string? Time { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; } // Status numérico como string

        [JsonPropertyName("CC")] public string? Cc { get; set; }
        [JsonPropertyName("CT")] public string? Ct { get; set; }
        [JsonPropertyName("EI")] public string? Ei { get; set; }
        [JsonPropertyName("IG")] public string? Ig { get; set; }

        // Campos dos times e estatísticas
        [JsonPropertyName("D1")] public string? D1 { get; set; }
        [JsonPropertyName("D2")] public string? D2 { get; set; }
        [JsonPropertyName("SY")] public string? Sy { get; set; }
        [JsonPropertyName("VL")] public string? Vl { get; set; }
        [JsonPropertyName("K1")] public string? K1 { get; set; }
        [JsonPropertyName("TC")] public string? Tc { get; set; }
        [JsonPropertyName("IC")] public string? Ic { get; set; }
        [JsonPropertyName("AM")] public string? Am { get; set; }
        [JsonPropertyName("AS")] public string? As { get; set; }
        [JsonPropertyName("AU")] public string? Au { get; set; }
        [JsonPropertyName("BF")] public string? Bf { get; set; }
        [JsonPropertyName("BX")] public string? Bx { get; set; }
        [JsonPropertyName("C1")] public string? C1 { get; set; }
        [JsonPropertyName("C2")] public string? C2 { get; set; }
        [JsonPropertyName("C3")] public string? C3 { get; set; }
        [JsonPropertyName("CB")] public string? Cb { get; set; }
        [JsonPropertyName("CE")] public string? Ce { get; set; }
        [JsonPropertyName("CL")] public string? Cl { get; set; }
        [JsonPropertyName("DC")] public string? Dc { get; set; }
        [JsonPropertyName("DO")] public string? Do { get; set; }
        [JsonPropertyName("EL")] public string? El { get; set; }
        [JsonPropertyName("ES")] public string? Es { get; set; }
        [JsonPropertyName("ET")] public string? Et { get; set; }
        [JsonPropertyName("FB")] public string? Fb { get; set; }
        [JsonPropertyName("FF")] public string? Ff { get; set; }
        [JsonPropertyName("FG")] public string? Fg { get; set; }
        [JsonPropertyName("FS")] public string? Fs { get; set; }
        [JsonPropertyName("HO")] public string? Ho { get; set; }
        [JsonPropertyName("HP")] public string? Hp { get; set; }
        [JsonPropertyName("IH")] public string? Ih { get; set; }
        [JsonPropertyName("L3")] public string? L3 { get; set; }
        [JsonPropertyName("MD")] public string? Md { get; set; }
        [JsonPropertyName("MO")] public string? Mo { get; set; }
        [JsonPropertyName("MP")] public string? Mp { get; set; }
        [JsonPropertyName("MS")] public string? Ms { get; set; }
        [JsonPropertyName("OI")] public string? Oi { get; set; }
        [JsonPropertyName("RO")] public string? Ro { get; set; }
        [JsonPropertyName("SD")] public string? Sd { get; set; }
        [JsonPropertyName("SE")] public string? Se { get; set; }
        [JsonPropertyName("SF")] public string? Sf { get; set; }
        [JsonPropertyName("SO")] public string? So { get; set; }
        [JsonPropertyName("SV")] public string? Sv { get; set; }
        [JsonPropertyName("T1")] public string? T1 { get; set; }
        [JsonPropertyName("T2")] public string? T2 { get; set; }
        [JsonPropertyName("T3")] public string? T3 { get; set; }
        [JsonPropertyName("TD")] public string? Td { get; set; }
        [JsonPropertyName("TO")] public string? To { get; set; }
        [JsonPropertyName("VI")] public string? Vi { get; set; }
        [JsonPropertyName("VS")] public string? Vs { get; set; }
        [JsonPropertyName("KC")] public string? Kc { get; set; }
        [JsonPropertyName("KI")] public string? Ki { get; set; }
        [JsonPropertyName("PI")] public string? Pi { get; set; }
        [JsonPropertyName("PO")] public string? Po { get; set; }
        [JsonPropertyName("SC")] public string? Sc { get; set; }
        [JsonPropertyName("EX")] public string? Ex { get; set; }
        [JsonPropertyName("DS")] public string? Ds { get; set; }
        [JsonPropertyName("ED")] public string? Ed { get; set; }
        [JsonPropertyName("AD")] public string? Ad { get; set; }
        [JsonPropertyName("PE")] public string? Pe { get; set; }
        [JsonPropertyName("I2")] public string? I2 { get; set; }
        [JsonPropertyName("MA")] public string? Ma { get; set; }
        [JsonPropertyName("OT")] public string? Ot { get; set; }
        [JsonPropertyName("SU")] public string? Su { get; set; }
        [JsonPropertyName("CN")] public string? Cn { get; set; }
        [JsonPropertyName("CX")] public string? Cx { get; set; }
        [JsonPropertyName("PY")] public string? Py { get; set; }

        // --- TRATAMENTO ESPECIAL PARA SCORES ---
        [JsonPropertyName("scores")]
        public JsonElement? ScoresElement { get; set; }

        [JsonIgnore]
        public Dictionary<string, string>? Scores
        {
            get
            {
                if (ScoresElement.HasValue && ScoresElement.Value.ValueKind == JsonValueKind.Object)
                {
                    try { return JsonSerializer.Deserialize<Dictionary<string, string>>(ScoresElement.Value.GetRawText()); }
                    catch { return null; }
                }
                return null;
            }
        }
    }

    public class Stats
    {
        [JsonPropertyName("update_at")] public string? UpdateAt { get; set; }
        [JsonPropertyName("update_dt")] public string? UpdateDt { get; set; }
        [JsonPropertyName("event_id")] public string? EventId { get; set; }
    }

    // Mantive seu converter, ele é útil para IDs que vem como numero ou string
    public class StringIntConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number) return reader.TryGetInt64(out long l) ? l.ToString() : reader.GetDouble().ToString();
            if (reader.TokenType == JsonTokenType.String) return reader.GetString();
            return null;
        }
        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);
    }
}