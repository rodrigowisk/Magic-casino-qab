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
                <div class="relative w-full overflow-hidden rounded-[24px] border border-white/5 shadow-[0_15px_50px_rgba(0,0,0,0.8)] bg-[#2b2b2e]">
                    <div class="relative z-10 p-6 md:p-8 flex flex-col items-center w-full font-sans">
                        
                        <h2 class="text-[22px] font-bold text-white mb-7 tracking-tight text-center">Checkout: ${tournament.name}</h2>
                        
                        <div class="w-full text-left mb-3">
                            <span class="text-[15px] font-bold text-white/90">Detalhes do Torneio</span>
                        </div>
                        
                        <div class="w-full bg-[#1c1c1e] rounded-[16px] p-5 mb-6 flex flex-col gap-4">
                            <div class="flex justify-between items-center">
                                <div class="flex items-center gap-3 text-zinc-400">
                                    <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 10h18M7 15h1m4 0h1m-7 4h12a3 3 0 003-3V8a3 3 0 00-3-3H6a3 3 0 00-3 3v8a3 3 0 003 3z"></path></svg>
                                    <span class="text-[16px] font-medium">Entrada</span>
                                </div>
                                <span class="text-[18px] font-bold text-white">R$ ${formatCurrency(tournament.entryFee).replace('R$', '').trim()}</span>
                            </div>
                            
                            <div class="flex justify-between items-center mt-1">
                                <div class="flex items-center gap-3 text-zinc-400">
                                    <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 3v4M3 5h4M6 17v4m-2-2h4m5-16l2.286 6.857L21 12l-5.714 2.143L13 21l-2.286-6.857L5 12l5.714-2.143L13 3z"></path></svg>
                                    <span class="text-[16px] font-medium">Prêmio</span>
                                </div>
                                <span class="text-[18px] font-bold text-[#1fbd54]">R$ ${formatCurrency(tournament.prizePool).replace('R$', '').trim()}</span>
                            </div>
                        </div>

                        <div class="flex items-center justify-center gap-2 text-zinc-500 mb-6">
                            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z"></path></svg>
                            <span class="text-[14px] font-medium">Pagamento seguro</span>
                        </div>

                        <div class="flex flex-col gap-3 w-full">
                            <button id="btn-pay" class="w-full flex justify-center items-center gap-2 bg-[#1fbd54] hover:bg-[#1aa348] text-white font-bold text-[17px] py-4 rounded-[14px] shadow-[0_4px_20px_rgba(31,189,84,0.15)] transition-all active:scale-[0.98]">
                                <svg class="w-[22px] h-[22px]" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="3" d="M5 13l4 4L19 7"></path></svg>
                                PAGAR AGORA
                            </button>
                            <button id="btn-cancel" class="w-full bg-[#434346] hover:bg-[#4f4f53] text-white font-bold text-[17px] py-4 rounded-[14px] transition-all active:scale-[0.98]">
                                CANCELAR
                            </button>
                        </div>
                    </div>
                </div>
            `,
            width: '400px', 
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