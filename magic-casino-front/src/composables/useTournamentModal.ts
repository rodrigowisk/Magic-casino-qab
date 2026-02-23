import Swal from 'sweetalert2';

export const useTournamentModal = () => {
    const formatCurrency = (val: number) => val?.toFixed(2).replace('.', ',') || '0,00';

    const confirmPurchase = async (tournament: any): Promise<boolean> => {
        const result = await Swal.fire({
            title: '', 
            padding: 0, 
            background: 'transparent', 
            showConfirmButton: false, 
            showCancelButton: false, 
            allowOutsideClick: true,
            html: `
                <div class="relative w-full overflow-hidden rounded-xl border border-white/20 shadow-[0_0_50px_rgba(0,0,0,0.8)] bg-[#0f172a]">
                    <div class="absolute inset-0 z-0 bg-cover bg-center opacity-40 mix-blend-overlay" style="background-image: url('/images/util/caixa.png'); filter: contrast(1.2);"></div>
                    <div class="absolute inset-0 z-0 bg-gradient-to-b from-[#0f172a]/90 via-[#0f172a]/95 to-[#020617]"></div>
                    <div class="relative z-10 p-6 flex flex-col items-center w-full text-center">
                        <span class="text-[10px] font-bold text-blue-400 uppercase tracking-[0.2em] mb-2 drop-shadow-md">CONFIRMAR COMPRA</span>
                        <h2 class="text-lg font-black text-white leading-tight mb-6 px-4 drop-shadow-lg">${tournament.name}</h2>
                        <div class="w-full bg-black/30 border border-white/10 rounded-lg p-4 mb-5 backdrop-blur-sm">
                            <div class="flex justify-between items-center mb-2">
                                <span class="text-xs text-slate-400 font-medium">Entrada</span>
                                <span class="text-xl font-black text-white tracking-wide">R$ ${formatCurrency(tournament.entryFee).replace('R$', '').trim()}</span>
                            </div>
                            <div class="w-full h-px bg-white/10 mb-2"></div>
                            <div class="flex justify-between items-center">
                                <span class="text-xs text-slate-400 font-medium">Prêmio</span>
                                <span class="text-sm font-bold text-emerald-400 tracking-wide">R$ ${formatCurrency(tournament.prizePool).replace('R$', '').trim()}</span>
                            </div>
                        </div>
                        <div class="flex flex-col gap-3 w-full">
                            <button id="btn-pay" class="w-full bg-emerald-600 hover:bg-emerald-500 text-white font-black uppercase text-xs tracking-widest py-3.5 rounded-lg shadow-lg shadow-emerald-500/20 transition-transform active:scale-[0.98] border-t border-white/20">PAGAR E ENTRAR</button>
                            <button id="btn-cancel" class="text-xs font-bold text-slate-500 hover:text-white transition-colors uppercase tracking-wider py-2">CANCELAR</button>
                        </div>
                    </div>
                </div>
            `,
            width: '320px', 
            customClass: { popup: '!overflow-visible !bg-transparent !p-0 !border-0' },
            didOpen: (popup) => {
                // 👇 FORÇA O SWEETALERT A FICAR NA FRENTE DO MODAL DE INFO
                const container = Swal.getContainer();
                if (container) {
                    container.style.zIndex = '100000';
                }
                
                popup.querySelector('#btn-pay')?.addEventListener('click', () => Swal.clickConfirm());
                popup.querySelector('#btn-cancel')?.addEventListener('click', () => Swal.close());
            }
        });

        return result.isConfirmed;
    };

    return { confirmPurchase };
};