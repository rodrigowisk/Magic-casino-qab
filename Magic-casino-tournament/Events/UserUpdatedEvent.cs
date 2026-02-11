namespace Magic_casino.Events
{
    public class UserUpdatedEvent
    {
        public required string UserId { get; set; }
        public required string Name { get; set; }
        public required string Avatar { get; set; }
    }
}