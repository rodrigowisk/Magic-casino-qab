using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Magic_Casino_Core.Hubs
{
    [Authorize] // Só usuários logados podem conectar
    public class UserHub : Hub
    {
        // Podemos adicionar métodos aqui se o Front precisar "falar" com o Back.
        // Por enquanto, só o Back vai "falar" com o Front, então pode ficar vazio.
    }
}