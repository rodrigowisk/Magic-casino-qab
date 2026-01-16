using System;
using System.Collections.Generic;

namespace Magic_casino_sportbook.DTOs
{
    // 🟡 ANTIGO: OddsResponse.cs
    // 🟢 NOVO NOME: TheOddsApiResponse.cs
    public class TheOddsApiResponse
    {
        public string id { get; set; }
        public string sport_key { get; set; }
        public string home_team { get; set; }
        public string away_team { get; set; }
        public DateTime commence_time { get; set; }
        public List<TheOddsBookmaker> bookmakers { get; set; }
    }

    public class TheOddsBookmaker
    {
        public string key { get; set; }
        public List<TheOddsMarket> markets { get; set; }
    }

    public class TheOddsMarket
    {
        public string key { get; set; }
        public List<TheOddsOutcome> outcomes { get; set; }
    }

    public class TheOddsOutcome
    {
        public string name { get; set; }
        public decimal price { get; set; }
        public decimal? point { get; set; }
    }
}