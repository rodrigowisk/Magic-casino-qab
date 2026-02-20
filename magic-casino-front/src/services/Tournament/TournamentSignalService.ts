import { HubConnectionBuilder, HubConnection, LogLevel, HttpTransportType } from '@microsoft/signalr';

class TournamentSignalService {
    private connection: HubConnection | null = null;
    
    // Callbacks existentes
    private onRankingUpdate: ((data: any) => void) | null = null;
    private onMyBetsUpdate: (() => void) | null = null;
    private onTournamentListUpdate: ((data: any) => void) | null = null;
    private joinResultListener: ((data: any) => void) | null = null;
    
    // ✅ NOVO: Callback para atualização do número de inscritos (Cards)
    private onParticipantCountUpdate: ((tournamentId: number, count: number) => void) | null = null;

    private serverTimeOffset = 0;

    // Caminho relativo funciona perfeito com seu Nginx
    private readonly HUB_URL = "/tournamentHub"; 

    async start() {
        if (this.connection?.state === "Connected") return;

        this.connection = new HubConnectionBuilder()
            .withUrl(this.HUB_URL, {
                // Força WebSockets para evitar problemas com Nginx/Docker
                skipNegotiation: true,
                transport: HttpTransportType.WebSockets
            })
            .withAutomaticReconnect()
            .configureLogging(LogLevel.Warning)
            .build();

        // 1. Sincronização de Relógio
        this.connection.on("SyncServerTime", (serverIsoTime: string) => {
            const serverTime = new Date(serverIsoTime).getTime();
            const clientTime = Date.now();
            this.serverTimeOffset = serverTime - clientTime;
            // console.log(`🕒 [SYNC] Diff: ${this.serverTimeOffset}ms`);
        });

        // 2. Ouvinte do Ranking
        this.connection.on("ReceiveRankingUpdate", (data) => {
            if (this.onRankingUpdate) this.onRankingUpdate(data);
        });

        // 3. Ouvinte de Apostas
        this.connection.on("RefreshMyBets", () => {
            if (this.onMyBetsUpdate) this.onMyBetsUpdate();
        });

        // 4. Ouvinte de Atualização da Lista (Criação/Edição de Torneios)
        this.connection.on("TournamentListUpdate", (data) => {
            console.log("📜 [SIGNALR] Lista de Torneios Atualizada", data);
            if (this.onTournamentListUpdate) this.onTournamentListUpdate(data);
        });

        this.connection.on("JoinResult", (data) => {
            if (this.joinResultListener) this.joinResultListener(data);
        });
        
        // 5. Ouvinte de Remoção (Opcional)
        this.connection.on("TournamentRemoved", (id: number) => {
             if (this.onTournamentListUpdate) this.onTournamentListUpdate({ id, status: 'Deleted' });
        });

        // ✅ 6. NOVO: Ouvinte de Atualização da contagem de inscritos
        this.connection.on("UpdateParticipantCount", (tournamentId: number, newCount: number) => {
            console.log(`📊 [SIGNALR] Torneio ${tournamentId} agora tem ${newCount} inscritos`);
            if (this.onParticipantCountUpdate) {
                this.onParticipantCountUpdate(tournamentId, newCount);
            }
        });

        try {
            await this.connection.start();
            console.log("🏆 TournamentHub Conectado!");
        } catch (err) {
            console.error("Erro ao conectar TournamentHub", err);
            // Tenta reconectar em 5s
            setTimeout(() => this.start(), 5000);
        }
    }

    getCorrectedNow(): number {
        return Date.now() + this.serverTimeOffset;
    }

    async joinTournament(tournamentId: string | number) {
        if (this.connection?.state === "Connected") {
            try {
                await this.connection.invoke("JoinGroup", String(tournamentId));
            } catch (err) { console.error("Erro JoinGroup", err); }
        }
    }

    async joinUserChannel(userId: string | number) {
        if (this.connection?.state === "Connected") {
            try {
                await this.connection.invoke("JoinUserGroup", String(userId));
            } catch (err) { console.error("Erro JoinUserGroup", err); }
        }
    }

    // ✅ NOVO: Entrar no Lobby para receber os updates da lista e contagem
    async joinLobby() {
        if (this.connection?.state === "Connected") {
            try {
                await this.connection.invoke("JoinLobby");
                console.log("🚪 Entrou no canal Lobby");
            } catch (err) { console.error("Erro JoinLobby", err); }
        }
    }

    // ✅ NOVO: Sair do Lobby quando o componente for desmontado
    async leaveLobby() {
        if (this.connection?.state === "Connected") {
            try {
                await this.connection.invoke("LeaveLobby");
                console.log("🚪 Saiu do canal Lobby");
            } catch (err) { console.error("Erro LeaveLobby", err); }
        }
    }

    setRankingListener(callback: (data: any) => void) {
        this.onRankingUpdate = callback;
    }

    setMyBetsListener(callback: () => void) {
        this.onMyBetsUpdate = callback;
    }

    setTournamentListListener(callback: (data: any) => void) {
        this.onTournamentListUpdate = callback;
    }

    // ✅ NOVO: Setter para o Listener da Contagem de Participantes
    setParticipantCountListener(callback: (tournamentId: number, count: number) => void) {
        this.onParticipantCountUpdate = callback;
    }

    public setJoinResultListener(callback: (data: any) => void) {
        this.joinResultListener = callback;
    }
    

    async stop() {
        if (this.connection) {
            await this.connection.stop();
            this.connection = null;
        }
    }
}

export default new TournamentSignalService();