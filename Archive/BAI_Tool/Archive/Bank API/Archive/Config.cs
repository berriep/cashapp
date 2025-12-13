using System.Collections.Generic;
using Newtonsoft.Json;

namespace RabobankZero
{
    public class Config
    {
        [JsonProperty("ClientId")]
        public string ClientId { get; set; } = "";

        [JsonProperty("ClientSecret")]
        public string ClientSecret { get; set; } = "";

        [JsonProperty("TokenUrl")]
        public string TokenUrl { get; set; } = "";

        [JsonProperty("ApiBaseUrl")]
        public string ApiBaseUrl { get; set; } = "";

        [JsonProperty("CertificatePath")]
        public string CertificatePath { get; set; } = "";

        [JsonProperty("PrivateKeyPath")]
        public string PrivateKeyPath { get; set; } = "";

        [JsonProperty("AuthCodeFile")]
        public string AuthCodeFile { get; set; } = "";

        [JsonProperty("TokenFile")]
        public string TokenFile { get; set; } = "";

        [JsonProperty("AccountIds")]
        public Dictionary<string, string> AccountIds { get; set; } = new();

        [JsonProperty("DefaultAccountId")]
        public string DefaultAccountId { get; set; } = "";
    }
}