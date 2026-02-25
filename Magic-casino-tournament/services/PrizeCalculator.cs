using System;
using System.Collections.Generic;
using System.Linq;

namespace Magic_casino_tournament.Services
{
    public static class PrizeCalculator
    {
        public static List<PrizeRuleDefinition> GetAvailableRules()
        {
            return new List<PrizeRuleDefinition>
            {
                new PrizeRuleDefinition { Id = "PREMIO_1", Name = "Clássico Top 3 (50%, 30%, 20%)" },
                // ✅ NOME ATUALIZADO PARA A NOVA DISTRIBUIÇÃO JUSTA
                new PrizeRuleDefinition { Id = "PREMIO_2", Name = "Estendido Top 5 (40%, 25%, 15%, 12%, 8%)" },
                new PrizeRuleDefinition { Id = "PREMIO_3", Name = "Double Up (Ganha o Dobro da Entrada)" },
                new PrizeRuleDefinition { Id = "WINNER_TAKES_ALL", Name = "Vencedor Leva Tudo (100%)" }
            };
        }

        public static List<decimal> GetDistribution(string ruleId, int participantCount, decimal entryFee = 0, decimal prizePool = 0)
        {
            var rule = (ruleId ?? "").ToUpper().Trim();

            switch (rule)
            {
                // =========================================================
                // 🥇 MODALIDADE 1: CLÁSSICO TOP 3
                // =========================================================
                case "PREMIO_1":
                case "TOP3":
                case "1":
                    return new List<decimal> { 0.50m, 0.30m, 0.20m };

                // =========================================================
                // 🥈 MODALIDADE 2: DISTRIBUIÇÃO TOP 5
                // =========================================================
                case "PREMIO_2":
                case "TOP5":
                case "2":
                    // ✅ VALORES ATUALIZADOS: 40% + 25% + 15% + 12% + 8% = 100%
                    return new List<decimal> { 0.40m, 0.25m, 0.15m, 0.12m, 0.08m };

                // =========================================================
                // 🥉 MODALIDADE 3: DOUBLE UP (DOBRA A ENTRADA)
                // =========================================================
                case "PREMIO_3":
                case "DOUBLE_UP":
                case "3":
                    // Se for um torneio grátis (sem taxa), divide igual pra 50% dos jogadores
                    if (entryFee <= 0 || prizePool <= 0 || participantCount < 1)
                    {
                        if (participantCount < 1) return new List<decimal> { 1.0m };
                        int halfWinners = (int)Math.Ceiling(participantCount * 0.5);
                        if (halfWinners == 0) halfWinners = 1;
                        return Enumerable.Repeat(1.0m / halfWinners, halfWinners).ToList();
                    }

                    // 1. Define o alvo: Dobrar a entrada exata
                    decimal targetPrize = entryFee * 2m;

                    // 2. Quantos vencedores cabem no pote líquido atual? (Arredondado pra baixo)
                    int winnersCount = (int)Math.Floor(prizePool / targetPrize);

                    if (winnersCount == 0)
                    {
                        return new List<decimal> { 1.0m };
                    }

                    // 3. Calcula a porcentagem matemática que representa o prêmio dobrado
                    decimal exactPercentage = targetPrize / prizePool;

                    return Enumerable.Repeat(exactPercentage, winnersCount).ToList();

                // =========================================================
                // ⚠️ PADRÃO: VENCEDOR LEVA TUDO
                // =========================================================
                default:
                    return new List<decimal> { 1.0m };
            }
        }
    }

    public class PrizeRuleDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}