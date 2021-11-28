namespace Drako.Api.Controllers
{
    public class GetBetsQuery
    {
        public int? OptionId { get; set; }
        public int PageNum { get; set; }
        public int PageSize { get; set; }
    }
}