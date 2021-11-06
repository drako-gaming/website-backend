using System;
using System.Net;
using RestSharp;

namespace Drako.Api.TwitchApiClient
{
    public class ApiException : Exception
    {
        public ApiException(IRestResponse restResponse) : base("An error occurred while calling a REST API", restResponse.ErrorException)
        {
            StatusCode = restResponse.StatusCode;
        }
        
        public HttpStatusCode StatusCode { get; }
    }
}