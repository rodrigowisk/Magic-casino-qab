namespace Magic_casino.DTOs
{
    // Modelo para LOGIN
    public class LoginRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    // Modelo para CRIAR USUÁRIO
    public class CreateUserRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Agent_code { get; set; }
    }

    // Modelo de RESPOSTA PADRÃO
    public class AuthResponse
    {
        public int Status { get; set; }
        public string Msg { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;

        // --- ADICIONEI ESTE CAMPO AQUI PARA CORRIGIR O ERRO ---
        public string? Name { get; set; }

        public bool Is_admin { get; set; }
        public bool Is_affiliate { get; set; }

        public string Token { get; set; } = string.Empty;

        // --- CAMPOS DE SALDO ---
        public decimal Balance_fiver { get; set; }
        public decimal Balance_qab { get; set; }
        public decimal Balance_bonus { get; set; }
    }
}