using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.GcpApigee.Models
{
    public class ApiStatus
    {
        public enum StatusCode
        {
            Success = 2,
            Warning = 3,
            Error = 4
        }

        [JsonProperty("message")] public string Message { get; set; }

        [JsonProperty("status")] public StatusCode Status { get; set; }
    }
}