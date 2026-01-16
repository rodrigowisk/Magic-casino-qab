using System.Text.Json.Serialization;

namespace Magic_casino_sportbook.DTOs
{
    // 🟢 MODELO PARA: https://api.b365api.com/v4/bet365/prematch
    public class BetsApiResponse
    {
        public int Success { get; set; }
        public List<BetsApiResult> Results { get; set; }
    }

    public class BetsApiResult
    {
        [JsonPropertyName("FI")]
        public string FixtureId { get; set; }

        [JsonPropertyName("event_id")]
        public string EventId { get; set; }

        [JsonPropertyName("main")]
        public BetsApiMainMarket Main { get; set; }
    }

    public class BetsApiMainMarket
    {
        [JsonPropertyName("sp")]
        public BetsApiSp Sp { get; set; }
    }

    public class BetsApiSp
    {
        [JsonPropertyName("full_time_result")]
        public BetsApiMarket FullTimeResult { get; set; }

        [JsonPropertyName("asian_handicap")]
        public BetsApiMarket AsianHandicap { get; set; }
    }

    public class BetsApiMarket
    {
        public string id { get; set; }
        public string name { get; set; }
        public List<BetsApiOdd> odds { get; set; }
    }

    public class BetsApiOdd
    {
        public string id { get; set; }
        public string odds { get; set; } // Vem como string "1.50" ou fração "1/2"
        public string name { get; set; } // "1", "X", "2"
        public string header { get; set; }
    }
}