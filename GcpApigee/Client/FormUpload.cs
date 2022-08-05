using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace Keyfactor.Extensions.Orchestrator.GcpApigee.Client
{
    public static class FormUpload
    {
        private static readonly Encoding Encoding = Encoding.UTF8;
        public static HttpWebResponse MultipartFormDataPost(string postUrl, string userAgent, Dictionary<string, object> postParameters,string token)
        {
            string formDataBoundary = $"----------{Guid.NewGuid():N}";
            string contentType = "multipart/form-data; boundary=" + formDataBoundary;

            byte[] formData = GetMultipartFormData(postParameters, formDataBoundary);

            return PostForm(postUrl, userAgent, contentType, formData,token);
        }
        private static HttpWebResponse PostForm(string postUrl, string userAgent, string contentType, byte[] formData,string token)
        {
            HttpWebRequest request = WebRequest.Create(postUrl) as HttpWebRequest;

            if (request == null)
            {
                throw new NullReferenceException("request is not a http request");
            }

            // Set up the request properties.
            request.Method = "POST";
            request.ContentType = contentType;
            request.UserAgent = userAgent;
            request.CookieContainer = new CookieContainer();
            request.ContentLength = formData.Length;

            // You could add authentication here as well if needed:
             request.PreAuthenticate = true;
             request.AuthenticationLevel = System.Net.Security.AuthenticationLevel.MutualAuthRequested;
             request.Headers.Add("Authorization", "Bearer " + token);

            // Send the form data to the request.
            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(formData,0, formData.Length);
                requestStream.Close();
            }

            return request.GetResponse() as HttpWebResponse;
        }

        private static byte[] GetMultipartFormData(Dictionary<string, object> postParameters, string boundary)
        {
            Stream formDataStream = new MemoryStream();
            bool needsClrf = false;

            foreach (var param in postParameters)
            {
                // Thanks to feedback from comment-ers, add a CRLF to allow multiple parameters to be added.
                // Skip it on the first parameter, add it to subsequent parameters.
                if (needsClrf)
                    formDataStream.Write(Encoding.GetBytes("\r\n"),0, Encoding.GetByteCount("\r\n"));

                needsClrf = true;

                if (param.Value is FileParameter fileToUpload)
                {
                    // Add just the first part of this param, since we will write the file data directly to the Stream
                    string header =
                        $"--{boundary}\r\nContent-Disposition: form-data; name=\"{param.Key}\"; filename=\"{fileToUpload.FileName ?? param.Key}\"\r\nContent-Type: {fileToUpload.ContentType ?? "application/octet-stream"}\r\n\r\n";

                    formDataStream.Write(Encoding.GetBytes(header),0, Encoding.GetByteCount(header));

                    // Write the file data directly to the Stream, rather than serializing it to a string.
                    formDataStream.Write(fileToUpload.File,0, fileToUpload.File.Length);
                }
                else
                {
                    string postData =
                        $"--{boundary}\r\nContent-Disposition: form-data; name=\"{param.Key}\"\r\n\r\n{param.Value}";
                    formDataStream.Write(Encoding.GetBytes(postData),0, Encoding.GetByteCount(postData));
                }
            }

            // Add the end of the request.  Start with a newline
            string footer = "\r\n--" + boundary + "--\r\n";
            formDataStream.Write(Encoding.GetBytes(footer),0, Encoding.GetByteCount(footer));

            // Dump the Stream into a byte[]
            formDataStream.Position = 0;
            byte[] formData = new byte[formDataStream.Length];
            // ReSharper disable once MustUseReturnValue
            formDataStream.Read(formData,0, formData.Length);
            formDataStream.Close();

            return formData;
        }

        public class FileParameter
        {
            public byte[] File { get; set; }
            public string FileName { get; set; }
            public string ContentType { get; set; }
            public FileParameter(byte[] file) : this(file, null) { }
            public FileParameter(byte[] file, string filename) : this(file, filename, null) { }
            public FileParameter(byte[] file, string filename, string contentType)
            {
                File = file;
                FileName = filename;
                ContentType = contentType;
            }
        }
    }
}
