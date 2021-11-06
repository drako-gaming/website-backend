using System;
using System.Net;
using Newtonsoft.Json;
using RestSharp;

namespace Drako.Api.TwitchApiClient
{
    public class ApiException : Exception
    {
        public ApiException(IRestResponse restResponse) : base(
            GetMessage(restResponse),
            restResponse.ErrorException)
        {
            StatusCode = restResponse.StatusCode;
        }
        
        public HttpStatusCode StatusCode { get; }

        private static string GetMessage(IRestResponse restResponse)
        {
            var errorObj = JsonConvert.DeserializeObject<ApiError>(restResponse.Content);
            return errorObj?.Message ?? "An error occurred calling the Twitch API. See innerException for details.";
        }
    }
}