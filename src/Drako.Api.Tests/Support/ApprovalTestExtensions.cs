using System;
using System.Net.Http;
using System.Threading.Tasks;
using ApprovalTests;
using ApprovalTests.Namers;
using Newtonsoft.Json;

namespace Drako.Api.Tests.Support
{
    public static class ApprovalTestExtensions
    {
        public static async Task<T> Approve<T>(this HttpContent actual, string additionalName, params Action<dynamic>[] scrubbers)
        {
            string original;
            string serialized;
            if (actual != null)
            {
                serialized = original = await actual.ReadAsStringAsync();
                var copy = JsonConvert.DeserializeObject(serialized);
                foreach (var scrubber in scrubbers)
                {
                    scrubber.Invoke(copy);
                }

                serialized = JsonConvert.SerializeObject(copy, Formatting.Indented);
            }
            else
            {
                serialized = null;
                original = null;
            }

            NamerFactory.AdditionalInformation = additionalName;
            Approvals.Verify(serialized);

            return JsonConvert.DeserializeObject<T>(original);
        }
    }
}