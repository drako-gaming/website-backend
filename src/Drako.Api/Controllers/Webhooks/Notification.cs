namespace Drako.Api.Controllers.Webhooks
{
    public class Notification<T>
    {
        public T Event { get; set; }
    }
}