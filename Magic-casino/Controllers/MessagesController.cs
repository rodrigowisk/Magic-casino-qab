using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Magic_casino.Data;
using Magic_casino.DTOs;
using Magic_casino.Models;
using Magic_casino.Events;
using MassTransit;

namespace Magic_casino.Controllers // Boa prática adicionar o namespace caso não tenha
{
    [ApiController]
    [Route("api/[controller]")]
    // [Authorize] // Pode descomentar depois que testar
    public class MessagesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;

        // Injetamos o AppDbContext (para leitura/deleção) e o IPublishEndpoint (para o RabbitMQ)
        public MessagesController(AppDbContext context, IPublishEndpoint publishEndpoint)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
        }

        // ADMIN: Enviar Mensagem (Agora usando RabbitMQ)
        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto)
        {
            // 1. Cria o evento com os dados recebidos do DTO
            var messageEvent = new SendMessageEvent
            {
                Subject = dto.Subject,
                Body = dto.Body,
                Type = dto.Type,
                Value = dto.Value
            };

            // 2. Dispara o evento para a fila do RabbitMQ
            // O SendMessageConsumer vai capturar isso e fazer as inserções no banco em segundo plano
            await _publishEndpoint.Publish(messageEvent);

            // 3. Retorna 202 Accepted imediatamente, sem travar a requisição
            return Accepted(new { message = "O disparo de mensagens foi adicionado à fila e será processado em segundo plano." });
        }

        // USER: Ler Inbox
        [HttpGet("inbox")]
        public async Task<ActionResult<List<InboxMessageDto>>> GetInbox()
        {
            // IMPORTANTE: Ajuste aqui para pegar o CPF do token JWT real do seu sistema
            // Por enquanto, deixei fixo ou buscando do Claim se existir
            var currentUserCpf = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // Se não tiver login, retorne erro ou um CPF de teste
            if (string.IsNullOrEmpty(currentUserCpf)) return Unauthorized();

            var messages = await _context.MessageRecipients
                .Include(r => r.Message)
                .Where(r => r.UserId == currentUserCpf && !r.IsDeleted)
                .OrderByDescending(r => r.Message.CreatedAt)
                .Select(r => new InboxMessageDto
                {
                    Id = r.Id,
                    Subject = r.Message.Subject,
                    Body = r.Message.Body,
                    IsRead = r.IsRead,
                    CreatedAt = r.Message.CreatedAt
                })
                .ToListAsync();

            return Ok(messages);
        }

        // USER: Marcar como lida
        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var currentUserCpf = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserCpf)) return Unauthorized();

            var recipient = await _context.MessageRecipients.FindAsync(id);
            if (recipient == null) return NotFound();

            // Validação de segurança
            if (recipient.UserId != currentUserCpf) return Forbid();

            recipient.IsRead = true;
            recipient.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // USER: Deletar
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMessage(int id)
        {
            var currentUserCpf = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserCpf)) return Unauthorized();

            var recipient = await _context.MessageRecipients.FindAsync(id);
            if (recipient == null) return NotFound();

            if (recipient.UserId != currentUserCpf) return Forbid();

            recipient.IsDeleted = true;
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}