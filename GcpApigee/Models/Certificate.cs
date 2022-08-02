using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.GcpApigee.Models
{
    public class Certificate
    {
        [JsonProperty("aliasName")] public string AliasName { get; set; }

        [JsonProperty("certificates")] public string[] Certificates { get; set; }
    }
}