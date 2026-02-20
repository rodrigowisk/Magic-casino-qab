using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Magic_casino.Models
{
    // A classe da Mensagem (Texto enviado pelo Admin)
    public class Message
    {
        public int Id { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Apenas para auditoria do admin
        public string TargetType { get; set; } // "All", "Level", "User"
        public string? TargetValue { get; set; } // "Bronze", "123", null
    }

    // A classe do Destinatário (Quem recebe)
    public class MessageRecipient
    {
        public int Id { get; set; }

        public int MessageId { get; set; }
        [ForeignKey("MessageId")]
        public Message? Message { get; set; }

        // ========================================================
        // A CORREÇÃO ESTÁ AQUI: MUDAMOS DE 'int' PARA 'string'
        // ========================================================
        public string UserId { get; set; } // Agora aceita o CPF

        public bool IsRead { get; set; } = false;
        public bool IsDeleted { get; set; } = false;
        public DateTime? ReadAt { get; set; }
    }
}