using System.ComponentModel;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.GcpApigee
{
    internal class StorePath
    {
        [JsonProperty("Location")]
        [DefaultValue("global")]
        public string Location { get; set; }

        [JsonProperty("Project Number")] 
        public string ProjectNumber { get; set; }

        [JsonProperty("jsonKey")]
        public string JsonKey { get; set; }
    }
}