namespace Magic_casino.DTOs
{
    public class RegisterRequest
    {
        public string Name { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string Cpf { get; set; }

        // --- ESTAVA FALTANDO ESTA LINHA ---
        public string Phone { get; set; }
    }
}