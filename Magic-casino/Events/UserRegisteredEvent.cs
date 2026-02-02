namespace Magic_casino.Events
{
    public class UserRegisteredEvent
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Cpf { get; set; } = string.Empty;
        public DateTime RegisteredAt { get; set; }
    }
}