using System;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.GcpApigee.Models
{
    internal class ApigeeException : ApplicationException
    {
        public ApigeeException(string message, ApiStatus.StatusCode statusCode) : base(message)
        {
            Message = message;
            StatusCode = statusCode;
        }

        public ApigeeException(string message, ApiStatus.StatusCode statusCode, Exception ex) : base(message, ex)
        {
            Message = message;
            StatusCode = statusCode;
        }

        [JsonProperty("statusCode")] public ApiStatus.StatusCode StatusCode { get; set; }

        [JsonProperty("message")] public sealed override string Message { get; }

        public static string FlattenExceptionMessages(Exception ex, string message)
        {
            message += ex.Message + Environment.NewLine;
            if (ex.InnerException != null)
                message = FlattenExceptionMessages(ex.InnerException, message);

            return message;
        }
    }
}