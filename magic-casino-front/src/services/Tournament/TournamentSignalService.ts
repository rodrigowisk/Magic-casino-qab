import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';

class TournamentSignalService {
    private connection: HubConnection | null = null;
    
    // Callback para atualizar a tela
    private onRankingUpdate: ((data: any) => void) | null = null;

    // ✅ CORREÇÃO 1: Use o caminho relativo. O Nginx vai capturar "/tournamentHub"
    // e redirecionar para o container correto (http://tournament:8080)
    private readonly HUB_URL = "/tournamentHub"; 

    async start() {
        if (this.connection?.state === "Connected") return;

        this.connection = new HubConnectionBuilder()
            .withUrl(this.HUB_URL)
            .withAutomaticReconnect()
            .configureLogging(LogLevel.Warning)
            .build();

        // Configurar os ouvintes
        this.connection.on("ReceiveRankingUpdate", (data) => {
            // console.log("📊 Ranking recebido via SignalR", data);
            if (this.onRankingUpdate) this.onRankingUpdate(data);
        });

        this.connection.on("BetConfirmed", (message) => {
             console.log("✅ Aposta confirmada!", message);
             // Aqui você pode disparar um Toast/Alerta se quiser
        });

        try {
            await this.connection.start();
            console.log("🏆 TournamentHub Conectado!");
        } catch (err) {
            console.error("Erro ao conectar TournamentHub", err);
        }
    }

    // ✅ CORREÇÃO 2: Método para dizer ao backend qual torneio estamos olhando
    async joinTournament(tournamentId: string | number) {
        if (this.connection?.state === "Connected") {
            try {
                // Chama o método "JoinGroup" que criamos no C#
                await this.connection.invoke("JoinGroup", String(tournamentId));
                console.log(`📡 Entrou no grupo do torneio: ${tournamentId}`);
            } catch (err) {
                console.error("Erro ao entrar no grupo do torneio", err);
            }
        }
    }

    setRankingListener(callback: (data: any) => void) {
        this.onRankingUpdate = callback;
    }

    async stop() {
        if (this.connection) {
            await this.connection.stop();
            this.connection = null;
        }
    }
}

export default new TournamentSignalService();