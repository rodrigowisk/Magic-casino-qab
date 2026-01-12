using System;
using System.Collections.Generic;

namespace Magic_casino_sportbook.DTOs
{
    public class OddsResponse
    {
        public string id { get; set; }
        public string sport_key { get; set; }
        public string home_team { get; set; }
        public string away_team { get; set; }
        public DateTime commence_time { get; set; }
        public List<Bookmaker> bookmakers { get; set; }
    }

    public class Bookmaker
    {
        public string key { get; set; }
        public List<Market> markets { get; set; }
    }

    public class Market
    {
        public string key { get; set; }
        public List<Outcome> outcomes { get; set; }
    }

    public class Outcome
    {
        public string name { get; set; }
        public decimal price { get; set; }
        public decimal? point { get; set; } // Necessário para todos os mercados
    }
}