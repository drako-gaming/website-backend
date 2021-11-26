using System;
using ApprovalTests;
using ApprovalTests.Namers;
using Newtonsoft.Json;

namespace Drako.Api.Tests.Support
{
    public static class ApprovalTestExtensions
    {
        public static void Approve<T>(this T actual, string additionalName, params Action<T>[] scrubbers)
        {
                string serialized;
            if (actual != null)
            {
                serialized = JsonConvert.SerializeObject(actual);
                var copy = JsonConvert.DeserializeObject<T>(serialized);
                foreach (var scrubber in scrubbers)
                {
                    scrubber.Invoke(copy);
                }

                serialized = JsonConvert.SerializeObject(copy, Formatting.Indented);
            }
            else
            {
                serialized = null;
            }

            NamerFactory.AdditionalInformation = additionalName;
            Approvals.Verify(serialized);
        }
    }
}