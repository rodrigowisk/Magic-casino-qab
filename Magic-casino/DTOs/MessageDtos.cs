using System.ComponentModel.DataAnnotations; // Adicione isso

public class SendMessageDto
{
    [Required(ErrorMessage = "O assunto é obrigatório")]
    public string Subject { get; set; }

    [Required(ErrorMessage = "A mensagem precisa ter conteúdo")]
    public string Body { get; set; }

    [Required]
    [RegularExpression("all|level|user", ErrorMessage = "Tipo inválido")]
    public string Type { get; set; } // Valida se é apenas um desses 3

    public string? Value { get; set; } // Pode ser nulo se Type == "all"
}

public class InboxMessageDto
{
    public int Id { get; set; } // ID do Recipient, não da mensagem original
    public string Subject { get; set; }
    public string Body { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}