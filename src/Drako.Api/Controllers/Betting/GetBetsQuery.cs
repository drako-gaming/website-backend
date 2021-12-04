namespace Drako.Api.Controllers
{
    public class GetBetsQuery
    {
        public string UserId { get; set; }
        public int? OptionId { get; set; }
        public int PageNum { get; set; }
        public int PageSize { get; set; }
    }
}