using System.Net.Http;
using System.Threading.Tasks;

namespace Drako.Api.Tests
{
    public static class HttpResponseMessageExtensions
    {
        public static async Task<string> Content(this HttpResponseMessage response)
        {
            return await response.Content.ReadAsStringAsync();
        }
    }
}