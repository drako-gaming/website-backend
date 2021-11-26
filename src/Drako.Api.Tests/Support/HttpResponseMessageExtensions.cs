using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Drako.Api.Tests.Support
{
    public static class HttpResponseMessageExtensions
    {
        public static async Task<string> Content(this HttpResponseMessage response)
        {
            return await response.Content.ReadAsStringAsync();
        }

        public static async Task<T> Content<T>(this HttpResponseMessage response)
        {
            return await response.Content.ReadFromJsonAsync<T>();
        }
    }
}