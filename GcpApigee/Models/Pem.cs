using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.GcpApigee.Models
{
    internal class Pem
    {
        public enum CertificateType
        {
            Cert,
            CertWithKey,
            Intermediate,
            Root
        }

        [JsonProperty("pemCert")] public string PemCert { get; set; }

        [JsonProperty("pemKey")] public string PemKey { get; set; }

        [JsonProperty("certType")] public CertificateType CertType { get; set; }
    }
}