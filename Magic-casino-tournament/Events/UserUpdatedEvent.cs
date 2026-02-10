namespace Magic_casino.Events
{
    public class UserUpdatedEvent
    {
        public string UserId { get; set; }
        public string Name { get; set; }
        public string Avatar { get; set; }
    }
}