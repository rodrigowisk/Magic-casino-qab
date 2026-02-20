namespace Magic_casino_sportbook.Domain.Enums
{
    /// <summary>
    /// Mapeia os códigos de status da BetsAPI (B365) para nomes legíveis.
    /// </summary>
    public enum GameStatus
    {
        /// <summary>
        /// Código API: "0"
        /// O jogo ainda não começou.
        /// </summary>
        Prematch = 0,

        /// <summary>
        /// Código API: "1"
        /// O jogo está acontecendo agora (In-Play).
        /// </summary>
        Live = 1,

        /// <summary>
        /// Código API: "2"
        /// (Raro) Às vezes usado para intervalo ou pausa temporária.
        /// </summary>
        ToBeFixed = 2,

        /// <summary>
        /// Código API: "3"
        /// O jogo acabou normalmente.
        /// </summary>
        Ended = 3,

        /// <summary>
        /// Código API: "4"
        /// O jogo foi adiado (Postponed).
        /// </summary>
        Postponed = 4,

        /// <summary>
        /// Código API: "5"
        /// O jogo foi cancelado.
        /// </summary>
        Cancelled = 5,

        /// <summary>
        /// Código API: "6"
        /// O jogo foi abandonado (Walkover).
        /// </summary>
        Walkover = 6,

        /// <summary>
        /// Código API: "7"
        /// O jogo foi interrompido.
        /// </summary>
        Interrupted = 7,

        /// <summary>
        /// Código API: "8"
        /// O jogo foi abandonado.
        /// </summary>
        Abandoned = 8,

        /// <summary>
        /// Código API: "9"
        /// O jogo foi aposentado (Retired) - Comum em Tênis.
        /// </summary>
        Retired = 9,

        /// <summary>
        /// Status Interno: Aguardando virar ao vivo (Entrou na HotZone)
        /// </summary>
        WaitingLive = 99
    }
}