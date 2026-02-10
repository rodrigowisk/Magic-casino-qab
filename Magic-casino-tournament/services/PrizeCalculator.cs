using System;
using System.Collections.Generic;
using System.Linq;

namespace Magic_casino_tournament.Services
{
    public static class PrizeCalculator
    {
        // ✅ NOVO: Lista de definições para o Front-end montar o Select
        public static List<PrizeRuleDefinition> GetAvailableRules()
        {
            return new List<PrizeRuleDefinition>
            {
                new PrizeRuleDefinition { Id = "PREMIO_1", Name = "Clássico Top 3 (50%, 30%, 20%)" },
                new PrizeRuleDefinition { Id = "PREMIO_2", Name = "Estendido Top 5 (40% ao 5%)" },
                new PrizeRuleDefinition { Id = "PREMIO_3", Name = "Double Up (50% Ganha / 50% Perde)" },
                new PrizeRuleDefinition { Id = "WINNER_TAKES_ALL", Name = "Vencedor Leva Tudo (100%)" }
            };
        }

        /// <summary>
        /// Retorna a lista de porcentagens para dividir o prêmio.
        /// Ex: [0.50, 0.30, 0.20] significa 1º ganha 50%, 2º 30%, 3º 20%.
        /// </summary>
        public static List<decimal> GetDistribution(string ruleId, int participantCount)
        {
            // Normaliza para evitar problemas de maiúsculas/minúsculas
            var rule = (ruleId ?? "").ToUpper().Trim();

            switch (rule)
            {
                // =========================================================
                // 🥇 MODALIDADE 1: CLÁSSICO TOP 3
                // 1º 50% | 2º 30% | 3º 20%
                // =========================================================
                case "PREMIO_1":
                case "TOP3":
                    return new List<decimal> { 0.50m, 0.30m, 0.20m };

                // =========================================================
                // 🥈 MODALIDADE 2: DISTRIBUIÇÃO TOP 5
                // 1º 40% | 2º 25% | 3º 10% | 4º 10% | 5º 5% (Total 90%*)
                // *Nota: Sobra 10% que fica pra casa ou acumula. 
                // Se quiser que feche 100%, ajuste os valores.
                // =========================================================
                case "PREMIO_2":
                case "TOP5":
                    return new List<decimal> { 0.40m, 0.25m, 0.10m, 0.10m, 0.05m };

                // =========================================================
                // 🥉 MODALIDADE 3: 50/50 (METADE GANHA)
                // Os 50% melhores dividem o prêmio igualmente.
                // Ex: 10 jogam -> 5 ganham (cada um leva 20% do total)
                // =========================================================
                case "PREMIO_3":
                case "DOUBLE_UP":
                    if (participantCount < 1) return new List<decimal> { 1.0m };

                    // Calcula quantos ganham (Metade dos participantes, arredondado pra cima)
                    int winnersCount = (int)Math.Ceiling(participantCount * 0.5);

                    // Evita divisão por zero
                    if (winnersCount == 0) winnersCount = 1;

                    // Divide 100% do prêmio igualmente entre os vencedores
                    decimal share = 1.0m / winnersCount;

                    // Cria uma lista repetindo a porcentagem para cada vencedor
                    return Enumerable.Repeat(share, winnersCount).ToList();

                // =========================================================
                // ⚠️ PADRÃO: VENCEDOR LEVA TUDO
                // =========================================================
                default:
                    return new List<decimal> { 1.0m };
            }
        }
    }

    // Classe auxiliar simples para o retorno da API
    public class PrizeRuleDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}