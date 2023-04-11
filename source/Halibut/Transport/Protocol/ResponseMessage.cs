using System;
using Halibut.Diagnostics;
using Newtonsoft.Json;

namespace Halibut.Transport.Protocol
{
    public class ResponseMessage : IResponseMessage
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("error")]
        public ServerError Error { get; set; }

        [JsonProperty("result")]
        public object Result { get; set; }
    }
}