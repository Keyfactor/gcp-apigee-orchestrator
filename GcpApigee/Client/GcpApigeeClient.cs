using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Google.Apis.Auth;
using Google.Apis.Auth.OAuth2;
using Keyfactor.Extensions.Orchestrator.GcpApigee.Models;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.PKI.PEM;
using Keyfactor.PKI.PrivateKeys;
using Keyfactor.PKI.X509;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Org.BouncyCastle.OpenSsl;
using RestSharp;

namespace Keyfactor.Extensions.Orchestrator.GcpApigee.Client
{
    public class GcpApigeeClient
    {
        private const string
            KsIdentifier = "/keystores/"; // String that precedes the keystore/truststore name in the cert store path

        private const string
            KsTempExtension =
                "_temp"; // String that gets appended to the managed keystore name and is used as the name for the temp keystore created for renewals

        private readonly bool _isTrustStore;
        private readonly string _jsonKey;
        private readonly string _project;
        private readonly string _restClientUrl;

        public GcpApigeeClient(InventoryJobConfiguration config)
        {
            Logger = LogHandler.GetClassLogger<GcpApigeeClient>();
            _project = config.CertificateStoreDetails.StorePath;
            var storeProperties =
                JsonConvert.DeserializeObject<Dictionary<string, string>>(config.CertificateStoreDetails.Properties);
            if (storeProperties != null)
            {
                _jsonKey = storeProperties["jsonKey"];
                _restClientUrl = "https://" + config.CertificateStoreDetails.ClientMachine;
                _isTrustStore = Convert.ToBoolean(storeProperties["isTrustStore"]);
            }

            Logger.LogDebug("project: " + _project);
            Logger.LogDebug("jsonKey size:" + _jsonKey.Length);
            Logger.LogDebug("trust store:" + _isTrustStore);
        }

        public GcpApigeeClient(ManagementJobConfiguration config)
        {
            Logger = LogHandler.GetClassLogger<GcpApigeeClient>();
            _project = config.CertificateStoreDetails.StorePath;
            var storeProperties =
                JsonConvert.DeserializeObject<Dictionary<string, string>>(config.CertificateStoreDetails.Properties);
            if (storeProperties != null)
            {
                _jsonKey = storeProperties["jsonKey"];
                _restClientUrl = "https://" + config.CertificateStoreDetails.ClientMachine;
                _isTrustStore = Convert.ToBoolean(storeProperties["isTrustStore"]);
            }

            Logger.LogDebug("project: " + _project);
            Logger.LogDebug("jsonKey size:" + _jsonKey.Length);
            Logger.LogDebug("trust store:" + _isTrustStore);
        }

        private ILogger Logger { get; }

        public List<Certificate> List(string storePath)
        {
            var certsFound = new List<Certificate>();

            try
            {
                Logger.MethodEntry();
                // Pull out the keystore name from the certificate store path --- this will verify proper setup of cert store in KF
                ParseKeystore(storePath);
                Logger.LogTrace($"Got Store Path: {storePath}");
                if (KeystoreExists(storePath))
                {
                    Logger.LogTrace($"Keystore Exists for: {storePath}");
                    var aliases = GetAliases(storePath);
                    Logger.LogTrace($"Got Aliases : {JsonConvert.SerializeObject(aliases)}");
                    // For each alias, create an object containing the certificate portion of the alias as a PEM
                    certsFound.AddRange(aliases.Select(alias => GetCertificate(alias, storePath)).Select(ConvertToPem));
                    Logger.LogTrace("Added Range...");
                }
                else
                {
                    throw new ApigeeException("Unable to find keystore in Apigee.", ApiStatus.StatusCode.Error);
                }
            }
            catch (ApigeeException ex1)
            {
                Logger.LogError("Error completing certificate inventory: " + ex1.Message);
                Logger.LogDebug(ex1.StackTrace);
                throw new Exception(ex1.Message);
            }
            catch (Exception ex2)
            {
                Logger.LogError("Error completing certificate inventory: " + ex2.Message);
                Logger.LogDebug(ex2.StackTrace);
                throw new Exception(ex2.Message);
            }

            Logger.MethodExit();
            return certsFound;
        }

        /**
         * Adds/renews a certificate and key to an Apigee keystore or adds a certificate to an Apigee truststore.
         */
        public ApiStatus Add(string certToAdd, string password, string alias, bool overwrite)
        {
            var addStatus = new ApiStatus { Status = ApiStatus.StatusCode.Success };

            try
            {
                if (_isTrustStore)
                    AddTruststoreAlias(certToAdd, password, alias, overwrite);
                else
                    AddKeystoreAlias(certToAdd, password, alias, overwrite);
            }
            catch (ApigeeException ex1)
            {
                Logger.LogError("Error adding or renewing certificate: " + ex1.Message);
                Logger.LogDebug(ex1.StackTrace);

                addStatus.Status = ex1.StatusCode;
                addStatus.Message = ex1.Message;
            }
            catch (Exception ex2)
            {
                Logger.LogError("Error adding or renewing certificate: " + ex2.Message);
                Logger.LogDebug(ex2.StackTrace);

                addStatus.Status = ApiStatus.StatusCode.Error;
                addStatus.Message =
                    ApigeeException.FlattenExceptionMessages(ex2, "Unable to add certificate to keystore - ");
            }

            return addStatus;
        }

        /**
         * Adds a certificate to an Apigee truststore.
         */
        private void AddTruststoreAlias(string certToAdd, string password, string alias, bool overwrite)
        {
            // Pull out the keystore name from the certificate store path --- this will verify proper setup of cert store in KF
            var keystore = ParseKeystore(_project);

            // If the cert to add contains a private key, throw an error - truststore certs cannot have a private key
            if (!string.IsNullOrEmpty(password))
                throw new ApigeeException(
                    $"'{alias}' contains a key and the Apigee store '{keystore}' is a truststore. Can only add public certs to a truststore.",
                    ApiStatus.StatusCode.Error);

            if (KeystoreExists(_project))
            {
                var aliasExists = AliasExists(alias, _project);

                // More than one alias is supported for TrustStores
                // SCENARIO 1 => Overwrite flag FALSE && alias already exists
                if (!overwrite && aliasExists)
                    throw new ApigeeException($"Alias '{alias}' already exists in Apigee truststore '{keystore}'.",
                        ApiStatus.StatusCode.Error);

                // SCENARIO 2 => Overwrite flag TRUE && alias already exists
                if (overwrite && aliasExists)
                    throw new ApigeeException(
                        $"Overwrite flag is set and alias '{alias}' already exists in Apigee truststore '{keystore}'. Renewals are not supported for TrustStores.",
                        ApiStatus.StatusCode.Error);

                // All remaining scenarios will need the certificate contents to be added to the truststore ---
                // Convert cert contents from base-64 DER to PEM
                var pem = PemUtilities.DERToPEM(Convert.FromBase64String(certToAdd),
                    PemUtilities.PemObjectType.Certificate);

                // Add the incoming cert to the certificate store (DER certs will not contain a chain)
                HttpStatusCode postAddCode;
                if ((postAddCode = PostAdd(pem, alias, _project)) != HttpStatusCode.OK)
                    throw new ApigeeException(
                        $"Unable to add cert '{alias}' to Apigee truststore '{keystore}' - Http Response {postAddCode}",
                        ApiStatus.StatusCode.Error);
            }
            else
            {
                throw new ApigeeException("Unable to find keystore in Apigee.", ApiStatus.StatusCode.Error);
            }
        }

        /**
         * Adds/renews a certificate and key to an Apigee keystore.
         */
        private void AddKeystoreAlias(string certToAdd, string password, string alias, bool overwrite)
        {
            // Pull out the keystore name from the certificate store path --- this will verify proper setup of cert store in KF
            var keystore = ParseKeystore(_project);

            // If the cert to add does not contain a private key, throw an error - keystore certs must have a private key
            if (string.IsNullOrEmpty(password))
                throw new ApigeeException(
                    $"Alias '{alias}' does not contain a key and the Apigee store '{keystore}' is a keystore. Can only add certs with their key.",
                    ApiStatus.StatusCode.Error);

            if (KeystoreExists(_project))
            {
                var aliasExists = AliasExists(alias, _project);

                // Get the number of aliases in the managed keystore
                // PREREQUISITE: One alias per keystore, so user cannot add an alias to a keystore if at least 1 alias already exists
                var managedAliasCount = GetAliasesCount(_project);

                // SCENARIO 1 => Overwrite flag FALSE && alias already exists
                if (!overwrite && aliasExists)
                    throw new ApigeeException(
                        $"Overwrite flag not set and alias '{alias}' already exists in Apigee keystore '{keystore}'. If attempting to renew, please check overwrite when scheduling this job.",
                        ApiStatus.StatusCode.Error);

                // SCENARIO 2 => Overwrite flag FALSE && at least 1 alias exists in the keystore ---
                if (!overwrite && managedAliasCount >= 1)
                    throw new ApigeeException(
                        $"Overwrite flag not set and {managedAliasCount} alias(es) already exist in Apigee keystore '{keystore}'. Only 1 alias per keystore is supported for Add.",
                        ApiStatus.StatusCode.Error);

                // SCENARIO 3 => Overwrite flag TRUE && (2 or more aliases in the store || 1 alias that doesn't match incoming alias)
                if (overwrite && managedAliasCount >= 2)
                    throw new ApigeeException(
                        $"Overwrite flag is set and {managedAliasCount} alias(es) already exist in Apigee keystore '{keystore}'. Only 1 alias per keystore is supported for Renewal.",
                        ApiStatus.StatusCode.Error);
                if (overwrite && managedAliasCount == 1 && !aliasExists)
                    throw new ApigeeException(
                        $"Overwrite flag is set and '{alias}' does not exist in Apigee keystore '{keystore}'. Cannot renew an alias if it doesn't exist.",
                        ApiStatus.StatusCode.Error);

                // All remaining scenarios will need the certificate contents to be added to the keystore ---
                // Convert cert contents from base-64 PKCS12 to collection of PEMs (PCKS12 will contain the cert chain)
                var pems = Pkcs12ToPemCollection(certToAdd, password);

                if (pems.Count > 0)
                {
                    // SCENARIO 4 => Overwrite flag FALSE and no aliases exist in the keystore (ADD)
                    // OR
                    // SCENARIO 5 => Overwrite flag is TRUE and no aliases exist in the keystore (ADD)
                    if (!overwrite && managedAliasCount == 0 || overwrite && managedAliasCount == 0)
                    {
                        // Add the incoming cert (include its chain) to the certificate store
                        HttpStatusCode postAddCode;
                        if ((postAddCode = PostAdd(pems, password, alias, _project)) != HttpStatusCode.OK)
                            throw new ApigeeException(
                                $"Unable to add cert '{alias}' to Apigee keystore '{keystore}' - Http Response {postAddCode}",
                                ApiStatus.StatusCode.Error);
                    }

                    // SCENARIO 6 => Overwrite flag is TRUE and 1 alias exists that matches incoming alias (RENEWAL)
                    if (overwrite && aliasExists && managedAliasCount == 1)
                    {
                        // Create a new temporary keystore
                        var tempKeystore = keystore + KsTempExtension;
                        var tempKeystorePath = _project + KsTempExtension;
                        var tempStatus = Create(tempKeystorePath, true);

                        if (tempStatus.Status != ApiStatus.StatusCode.Success)
                            throw new ApigeeException(tempStatus.Message, tempStatus.Status);

                        // Check if incoming cert already exists in the temp keystore
                        var tempExists = AliasExists(alias, tempKeystorePath);

                        if (!tempExists) // Alias doesn't exist in the temp keystore
                        {
                            // Add the incoming cert (include its chain) to the temp certificate store
                            HttpStatusCode postAddCode;
                            if ((postAddCode = PostAdd(pems, password, alias, tempKeystorePath)) != HttpStatusCode.OK)
                                throw new ApigeeException(
                                    $"Adding renewed cert to temp store - Unable to add cert '{alias}' to Apigee keystore '{tempKeystore}' - Http Response {postAddCode}",
                                    ApiStatus.StatusCode.Error);
                        }

                        // Get all the references tied to the managed keystore
                        var envIndex = _project.IndexOf(KsIdentifier, StringComparison.Ordinal);
                        var envPath = _project.Substring(0, envIndex);
                        var refs = GetReferences(envPath);

                        // Traverse the list of references in the Apigee environment
                        foreach (var r in refs)
                        {
                            // Get the keystore name tied to the reference
                            var refObj = GetReferenceResource(envPath, r);
                            if (refObj == null)
                                throw new ApigeeException("Unable to get reference refObj in Apigee",
                                    ApiStatus.StatusCode.Error);

                            // For those references pointing to the managed keystore, change to point to the temp keystore
                            if (refObj.refers == keystore && refObj.resourceType == "KeyStore")
                            {
                                // Update the reference to point to the temp keystore
                                var tempRefObj = PutReferenceResource(envPath, refObj, tempKeystore);
                                if (tempRefObj == null)
                                    throw new ApigeeException(
                                        $"Unable to update reference '{refObj.name}' to point to temp keystore '{tempKeystore}' in Apigee",
                                        ApiStatus.StatusCode.Error);
                            }
                        }

                        // Delete all the aliases in the managed keystore
                        var managedAliases = GetAliases(_project);
                        if (managedAliases.Count > 1)
                            throw new ApigeeException(
                                $"{managedAliases.Count} alias(es) exist in Apigee keystore '{keystore}'. Cannot remove alias as part of Renewal process.",
                                ApiStatus.StatusCode.Error);

                        // Traverse the list of aliases in the managed keystore --- there should only be 1
                        foreach (var a in managedAliases)
                            if (a == alias) // Name of alias being deleted matches the one being renewed
                            {
                                var removeStatus = Remove(a, _project);
                                if (removeStatus.Status != ApiStatus.StatusCode.Success)
                                    throw new ApigeeException(removeStatus.Message, removeStatus.Status);
                            }
                            else
                            {
                                throw new ApigeeException(
                                    $"Alias '{a}' being deleted doesn't match alias '{alias}' being renewed. Cannot remove alias as part of Renewal process.",
                                    ApiStatus.StatusCode.Error);
                            }

                        // TODO: Would it be possible for another cert to be added in the time of the renewal?
                        // Add the incoming alias to the managed keystore --- Check if alias exists in the managed keystore before attempting to add it
                        var tempManagedExists = AliasExists(alias, _project);

                        if (!tempManagedExists) // Alias doesn't exist in the managed keystore
                        {
                            // Add the incoming (renewed) cert (include its chain) to the managed certificate store
                            HttpStatusCode postAddCode;
                            if ((postAddCode = PostAdd(pems, password, alias, _project)) != HttpStatusCode.OK)
                                throw new ApigeeException(
                                    $"Unable to renew cert '{alias}' in Apigee keystore '{keystore}'  - Http Response {postAddCode}",
                                    ApiStatus.StatusCode.Error);
                        }

                        // Traverse the list of references ---
                        // For those pointing to the temp keystore, change to point to the managed keystore
                        foreach (var r in refs)
                        {
                            // Get the keystore name tied to the reference
                            var refObj = GetReferenceResource(envPath, r);
                            if (refObj == null)
                                throw new ApigeeException("Unable to get reference refObj in Apigee",
                                    ApiStatus.StatusCode.Error);

                            if (refObj.refers == tempKeystore && refObj.resourceType == "KeyStore")
                            {
                                // Update the reference to point to the managed keystore
                                var managedRefObj = PutReferenceResource(envPath, refObj, keystore);
                                if (managedRefObj == null)
                                    throw new ApigeeException(
                                        $"Unable to update reference '{refObj.name}' to point to managed keystore '{keystore}' in Apigee",
                                        ApiStatus.StatusCode.Error);
                            }
                        }

                        // Delete the temp keystore
                        HttpStatusCode deleteKsCode;
                        if ((deleteKsCode = DeleteKeystore(tempKeystorePath)) != HttpStatusCode.OK)
                            throw new ApigeeException(
                                $"Unable to delete temp keystore '{tempKeystore}' in Apigee - Http Response {deleteKsCode}",
                                ApiStatus.StatusCode.Error);
                    } // END renewal
                }
                else
                {
                    throw new ApigeeException("Unable to successfully convert the cert contents to PEM-formats.",
                        ApiStatus.StatusCode.Error);
                }
            }
            else
            {
                throw new ApigeeException("Unable to find keystore in Apigee.", ApiStatus.StatusCode.Error);
            }
        }

        /**
         * Removes an alias from an Apigee keystore.
         */
        public ApiStatus Remove(string alias, string storePath)
        {
            var removeStatus = new ApiStatus { Status = ApiStatus.StatusCode.Success };

            try
            {
                if (AliasExists(alias, storePath))
                {
                    // Remove the alias from the keystore
                    HttpStatusCode deleteAliasCode;
                    if ((deleteAliasCode = DeleteAlias(alias, storePath)) != HttpStatusCode.OK)
                        throw new ApigeeException(
                            $"Unable to remove cert '{alias}' in Apigee - Http Response {deleteAliasCode}",
                            ApiStatus.StatusCode.Error);
                }
                else
                {
                    throw new ApigeeException(
                        $"Unable to remove alias '{alias}'. Doesn't exist in the Apigee keystore.",
                        ApiStatus.StatusCode.Error);
                }
            }
            catch (ApigeeException ex1)
            {
                Logger.LogError("Error removing certificate: " + ex1.Message);
                Logger.LogDebug(ex1.StackTrace);

                removeStatus.Status = ex1.StatusCode;
                removeStatus.Message = ex1.Message;
            }
            catch (Exception ex2)
            {
                Logger.LogError("Error removing certificate: " + ex2.Message);
                Logger.LogDebug(ex2.StackTrace);

                removeStatus.Status = ApiStatus.StatusCode.Error;
                removeStatus.Message =
                    ApigeeException.FlattenExceptionMessages(ex2, "Unable to remove certificate from keystore - ");
            }

            return removeStatus;
        }

        /**
         * Creates an empty Apigee keystore/truststore.
         */
        public ApiStatus Create(string storePath, bool renew)
        {
            var createStatus = new ApiStatus { Status = ApiStatus.StatusCode.Success };

            try
            {
                // Pull out the keystore name from the certificate store path --- this will verify proper setup of cert store in KF
                var keystore = ParseKeystore(storePath);

                if (KeystoreExists(storePath))
                {
                    if (!renew)
                        throw new ApigeeException($"Keystore '{keystore}' already exists in Apigee.",
                            ApiStatus.StatusCode.Error);
                }
                else
                {
                    // Keystore doesn't exist - create it
                    HttpStatusCode postCreateCode;
                    if ((postCreateCode = PostCreate(keystore,
                        storePath.IndexOf(KsIdentifier, StringComparison.Ordinal), storePath)) != HttpStatusCode.OK)
                        throw new ApigeeException(
                            $"Unable to create keystore '{keystore}' in Apigee - Http Response {postCreateCode}",
                            ApiStatus.StatusCode.Error);
                }
            }
            catch (ApigeeException ex1)
            {
                Logger.LogError("Error creating keystore: " + ex1.Message);
                Logger.LogDebug(ex1.StackTrace);

                createStatus.Status = ex1.StatusCode;
                createStatus.Message = ex1.Message;
            }
            catch (Exception ex2)
            {
                Logger.LogError("Error creating keystore: " + ex2.Message);
                Logger.LogDebug(ex2.StackTrace);

                createStatus.Status = ApiStatus.StatusCode.Error;
                createStatus.Message = ApigeeException.FlattenExceptionMessages(ex2, "Unable to create keystore - ");
            }

            return createStatus;
        }


        // APIGEE REST CALLS

        /**
         * HTTP Get: Lists all aliases in a keystore as a JSON array.
         */
        private List<string> GetAliases(string storePath)
        {
            var client = new RestClient(_restClientUrl);
            var token = GetCredential();

            var resource = $"/v1/{storePath}/aliases";
            var request = new RestRequest(resource);
            request.AddHeader("Authorization", $"Bearer {token}");

            var response = client.Execute(request);

            return JsonConvert.DeserializeObject<List<string>>(
                response.Content ?? throw new InvalidOperationException());
        }

        /**
         * HTTP Get: Returns the count of all aliases in a keystore.
         */
        private int GetAliasesCount(string storePath)
        {
            try
            {
                var client = new RestClient(_restClientUrl);
                var token = GetCredential();

                var resource = $"/v1/{storePath}/aliases";
                var request = new RestRequest(resource);
                request.AddHeader("Authorization", $"Bearer {token}");

                var response = client.Execute(request);

                // ReSharper disable once PossibleNullReferenceException
                return JsonConvert.DeserializeObject<List<string>>(response.Content).Count;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        /**
         * HTTP Get: Gets the certificate from an alias in PEM-encoded format and returns as a Certificate object.
         */
        private Certificate GetCertificate(string aliasName, string storePath)
        {
            var client = new RestClient(_restClientUrl);
            var token = GetCredential();

            var resource = $"/v1/{storePath}/aliases/{aliasName}/certificate";
            var request = new RestRequest(resource);
            request.AddHeader("Authorization", $"Bearer {token}");

            var response = client.Execute(request);

            Certificate cert = null;
            if (response.StatusCode == HttpStatusCode.OK)
                cert = new Certificate
                {
                    AliasName = aliasName,
                    Certificates = new[] { response.Content }
                };

            return cert;
        }

        /**
         * HTTP Post: Creates an alias from a base-64 encoded PKCS12 certificate, including the cert chain, and key pair.
         * Format = keycertfile
         * To be used with keystores.
         */
        private HttpStatusCode PostAdd(List<Pem> pemCollection, string password, string alias, string storePath)
        {
            var client = new RestClient(_restClientUrl);
            var token = GetCredential();

            var resource = $"/v1/{storePath}/aliases";
            var request = new RestRequest(resource, Method.Post);

            // Add headers
            request.AddHeader("Authorization", $"Bearer {token}");
            //request.AddHeader("Content-Type", "multipart/form-data;boundary=63c5979328c44e2c869349443a94200e");
            request.AddQueryParameter("format", "keycertfile");
            request.AddQueryParameter("alias", $"{alias}");

            request.AlwaysMultipartFormData = true;

            // Create the certificate body
            var certBody = string.Empty;
            var keyBody = string.Empty;
            foreach (var pem in pemCollection)
                switch (pem.CertType)
                {
                    case Pem.CertificateType.Cert:
                        {
                            // TODO: Not sure this use case will ever get hit
                            break;
                        }
                    case Pem.CertificateType.CertWithKey:
                        {
                            certBody = pem.PemCert;
                            keyBody = pem.PemKey;
                            break;
                        }
                    case Pem.CertificateType.Intermediate:
                        {
                            certBody = certBody + "\r\n\r\n" + pem.PemCert;
                            break;
                        }
                    case Pem.CertificateType.Root:
                        {
                            certBody = certBody + "\r\n\r\n" + pem.PemKey;
                            break;
                        }
                }

            

            var postParameters = new Dictionary<string, object>
            {
                {"certFile", certBody},
                {"keyFile", keyBody},
                {"password", password}
            };

            // Create request and receive response
            var userAgent = "Keyfactor Agent";
            var webResponse = FormUpload.MultipartFormDataPost(resource, userAgent, postParameters);
            Logger.LogTrace("Got webResponse...");
            using var responseReader = new StreamReader(webResponse.GetResponseStream() ?? Stream.Null);
            responseReader.ReadToEnd();
            var sc = webResponse.StatusCode;
            webResponse.Close();
            Logger.MethodExit();

            return sc;
            // Body
            //var boundary = "--63c5979328c44e2c869349443a94200e";
            //var endBoundary = "--63c5979328c44e2c869349443a94200e--";
            //var body =
            //    $"{boundary}\r\nContent-Disposition: form-data; name=\"certFile\"\r\nContent-Type: text/plain\r\n\r\n{certBody}\r\n" +
            //    $"{boundary}\r\nContent-Disposition: form-data; name=\"keyFile\"\r\nContent-Type: text/plain\r\n\r\n{keyBody}\r\n" +
            //    $"{boundary}\r\nContent-Disposition: form-data; name=\"password\"\r\nContent-Type: text/plain\r\n\r\n{password}\r\n" +
            //    $"{endBoundary}";
            //request.AddParameter("multipart/form-data;boundary=63c5979328c44e2c869349443a94200e", body, ParameterType.RequestBody);

            //var response = client.Execute(request);

            //return response.StatusCode;
        }

        /**
         * HTTP Post: Creates an alias from a base-64 DER certificate.
         * Format = keycertfile; Omit keyFile
         * To be used with TrustStores.
         */
        private HttpStatusCode PostAdd(string pem, string alias, string storePath)
        {
            var client = new RestClient(_restClientUrl);
            var token = GetCredential();

            var resource = $"/v1/{storePath}/aliases";
            var request = new RestRequest(resource, Method.Post);

            // Add headers
            request.AddHeader("Authorization", $"Bearer {token}");
            request.AddHeader("Content-Type", "multipart/form-data;boundary=|");
            //request.AddHeader("Content-Type", "multipart/form-data");
            request.AddQueryParameter("format", "keycertfile");
            request.AddQueryParameter("alias", $"{alias}");

            request.AlwaysMultipartFormData = true;

            // Create the certificate body
            var certBody = pem;

            // Body
            var boundary = "--|";
            var endBoundary = "--|--";
            var body =
                $"{boundary}\r\nContent-Disposition: form-data; name=\"certFile\"\r\nContent-Type: text/plain\r\n\r\n{certBody}\r\n" +
                $"{endBoundary}";
            request.AddParameter("multipart/form-data;boundary=|", body, ParameterType.RequestBody);

            var response = client.Execute(request);

            return response.StatusCode;
        }

        /**
         * HTTP Delete: Deletes an alias.
         */
        private HttpStatusCode DeleteAlias(string alias, string storePath)
        {
            var client = new RestClient(_restClientUrl);
            var token = GetCredential();

            var resource = $"/v1/{storePath}/aliases/{alias}";
            var request = new RestRequest(resource, Method.Delete);
            request.AddHeader("Authorization", $"Bearer {token}");

            var response = client.Execute(request);

            return response.StatusCode;
        }

        /**
         * HTTP Post: Creates a keystore.
         */
        private HttpStatusCode PostCreate(string keystoreId, int keystoreIndex, string storePath)
        {
            var client = new RestClient(_restClientUrl);
            var token = GetCredential();

            var resource = $"/v1/{storePath.Substring(0, keystoreIndex)}/keystores";
            var request = new RestRequest(resource, Method.Post);
            request.AddHeader("Authorization", $"Bearer {token}");

            request.RequestFormat = DataFormat.Json;
            request.AddParameter("name", $"{keystoreId}");

            var response = client.Execute(request);

            return response.StatusCode;
        }

        /**
         * HTTP Get: Gets a keystore.
         */
        private HttpStatusCode GetKeystore(string storePath)
        {
            var client = new RestClient(_restClientUrl);
            var token = GetCredential();

            var resource = $"/v1/{storePath}";
            var request = new RestRequest(resource);
            request.AddHeader("Authorization", $"Bearer {token}");

            var response = client.Execute(request);

            return response.StatusCode;
        }

        /**
         * HTTP Delete: Deletes a keystore.
         */
        private HttpStatusCode DeleteKeystore(string storePath)
        {
            var client = new RestClient(_restClientUrl);
            var token = GetCredential();

            var resource = $"/v1/{storePath}";
            var request = new RestRequest(resource, Method.Delete);
            request.AddHeader("Authorization", $"Bearer {token}");

            var response = client.Execute(request);

            return response.StatusCode;
        }

        /**
         * HTTP Get: Lists all References in an environment as a JSON array.
         */
        private List<string> GetReferences(string envPath)
        {
            var client = new RestClient(_restClientUrl);
            var token = GetCredential();

            var resource = $"/v1/{envPath}/references";
            var request = new RestRequest(resource);
            request.AddHeader("Authorization", $"Bearer {token}");

            var response = client.Execute(request);

            return JsonConvert.DeserializeObject<List<string>>(
                response.Content ?? throw new InvalidOperationException());
        }

        /**
         * HTTP Get: Gets a Reference resource and returns as a Reference object.
         */
        private Reference GetReferenceResource(string envPath, string reference)
        {
            var client = new RestClient(_restClientUrl);
            var token = GetCredential();

            var resource = $"/v1/{envPath}/references/{reference}";
            var request = new RestRequest(resource);
            request.AddHeader("Authorization", $"Bearer {token}");

            var response = client.Execute(request);

            return JsonConvert.DeserializeObject<Reference>(response.Content ?? throw new InvalidOperationException());
        }

        /**
         * HTTP Put: Updates an existing Reference.
         */
        private Reference PutReferenceResource(string envPath, Reference refObj, string newKeystore)
        {
            var client = new RestClient(_restClientUrl);
            var token = GetCredential();

            var resource = $"/v1/{envPath}/references/{refObj.name}";
            var request = new RestRequest(resource, Method.Put);
            request.AddHeader("Authorization", $"Bearer {token}");

            // Add body
            var param = new Reference
            {
                name = refObj.name,
                description = refObj.description,
                resourceType = refObj.resourceType,
                refers = newKeystore
            };
            request.AddJsonBody(param);

            var response = client.Execute(request);

            Reference tempRefObj = null;
            if (response.StatusCode == HttpStatusCode.OK)
                tempRefObj =
                    JsonConvert.DeserializeObject<Reference>(response.Content ?? throw new InvalidOperationException());

            return tempRefObj;
        }


        // HELPER FUNCTIONS 

        /**
         * Extracts the name of the keystore from the certificate store path in the KF Certificate Store setup.
         */
        private string ParseKeystore(string storePath)
        {
            var searchString = KsIdentifier;

            // Check to see if the keystore name was included in the certificate store path
            int keystoreIndex;

            // Keystore name NOT included in the certificate store path
            if ((keystoreIndex = storePath.IndexOf(searchString, StringComparison.Ordinal)) == -1)
                throw new ApigeeException($"Store path '{storePath}' does not include the Apigee keystore name.",
                    ApiStatus.StatusCode.Error);

            // Parse out the keystore name from the certificate store path
            // ASSUMPTION: For Apigee keystores, the name of the keystore will be included in the certificate store type store path
            var keystoreId = storePath.Substring(keystoreIndex + searchString.Length);

            if (string.IsNullOrEmpty(keystoreId))
                throw new ApigeeException("Keystore name specified cannot be blank.", ApiStatus.StatusCode.Error);

            return keystoreId;
        }

        /**
         * Checks to see if the Apigee keystore exists.
         */
        private bool KeystoreExists(string storePath)
        {
            var exists = false;

            var getKsCode = GetKeystore(storePath);

            // Keystore already exists in Apigee
            if (getKsCode == HttpStatusCode.OK) exists = true;

            return exists;
        }

        /**
         * Checks to see if the alias exists in the Apigee keystore.
         */
        private bool AliasExists(string alias, string keystorePath)
        {
            var exists = false;

            var certFound = GetCertificate(alias, keystorePath);

            // Alias already exists in Apigee
            if (certFound != null) exists = true;

            return exists;
        }

        /**
         * Converts the string certificate contents from the Apigee GET alias endpoint to a PEM-format.
         */
        private Certificate ConvertToPem(Certificate cert)
        {
            // Run conversion on cert format
            for (var i = 0; i < cert.Certificates.Length; i++)
                cert.Certificates[i] = PemUtilities.DERToPEM(PemUtilities.PEMToDER(cert.Certificates[i]),
                    PemUtilities.PemObjectType.NoHeaders);

            return cert;
        }

        /**
         * Converts the KF Command PCKS12 certificate contents to a collection of PEM certs in the certificate chain.
         */
        private List<Pem> Pkcs12ToPemCollection(string certContents, string pfxPassword)
        {
            // Read pkcs12 cert contents into byte array
            var pfx = Convert.FromBase64String(certContents);

            // Create X509 cert object
            var x509Cert = new X509Certificate2(pfx, pfxPassword, X509KeyStorageFlags.Exportable);

            // From the x509 cert contents, build out an object containing all certs in the chain
            var certChain = new X509Chain();
            certChain.Build(x509Cert);

            // For each cert in the cert chain, convert it to a PEM
            // NOTE: The certs in the chain appear in the following order: public cert, intermediate, root
            var pemCerts = new List<Pem>();
            var i = 0;
            foreach (var cert in certChain.ChainElements)
            {
                var converter = CertificateConverterFactory.FromX509Certificate2(cert.Certificate);
                var pem = new Pem();

                // This is the public cert with a private key
                if (cert.Certificate.HasPrivateKey)
                {
                    pem.CertType = Pem.CertificateType.CertWithKey;
                    pem.PemCert = converter.ToPEM(true, pfxPassword);
                    pemCerts.Add(pem);
                }
                // This is a cert without a private key
                else
                {
                    if (i == 0
                    ) // Public cert without a private key; TODO: This probably will never get hit in this use case since a private key will always get passed along
                        pem.CertType = Pem.CertificateType.Cert;
                    else if (i == certChain.ChainElements.Count - 1) // Root cert = last cert in the chain
                        pem.CertType = Pem.CertificateType.Root;
                    else // Intermediate cert(s)
                        pem.CertType = Pem.CertificateType.Intermediate;

                    pem.PemCert = converter.ToPEM(true);
                    pemCerts.Add(pem);
                }

                i++;
            }

            // If there is a PEM in the collection with a cert and key, decrypt the key and add to the PEM object
            var certKeyPem = pemCerts.Find(x => x.CertType.Equals(Pem.CertificateType.CertWithKey));
            if (certKeyPem != null)
            {
                var keyIndex =
                    certKeyPem.PemCert.IndexOf("-----BEGIN ENCRYPTED PRIVATE KEY-----", StringComparison.Ordinal);

                // Decrypt the private key if it is encrypted
                if (keyIndex != -1)
                {
                    // Decrypt the private key
                    var keyConverter = PrivateKeyConverterFactory.FromPKCS12(pfx, pfxPassword);
                    var key = keyConverter.ToBCPrivateKey();
                    var stringBuilder = new StringBuilder();
                    var sw = new StringWriter(stringBuilder);
                    var pemWriter = new PemWriter(sw);
                    pemWriter.WriteObject(key);
                    pemWriter.Writer.Flush();

                    // Update the PEM key with the decrypted key
                    certKeyPem.PemKey = sw.ToString();

                    // Update the PEM cert with only the cert (no key)
                    var certIndex = certKeyPem.PemCert.IndexOf("-----BEGIN CERTIFICATE-----", StringComparison.Ordinal);
                    certKeyPem.PemCert = certKeyPem.PemCert.Substring(certIndex, keyIndex - certIndex);
                }
                else // ASSUMPTION: The private key contained in the certificate will be encrypted; if it isn't, return empty list of PEMs
                {
                    // Unable to parse out the key and cert
                    return new List<Pem>();
                }
            }
            else // Unable to find a PEM with a cert and key; return empty list of PEMs
            {
                return new List<Pem>();
            }

            return pemCerts;
        }

        /**
         * Create an OAuth token to authenticate against Google Cloud.
         */
        private string GetCredential()
        {
            // Load secret key from cert store properties
            Logger.LogDebug("Loading key from store properties");

            if (string.IsNullOrEmpty(_jsonKey))
                throw new Exception("A service key must be provided in the certificate store setup.");

            var credential = GoogleCredential.FromJson(_jsonKey);

            // Check for additional scoping on credential
            if (credential.IsCreateScopedRequired)
                credential = credential.CreateScoped("https://www.googleapis.com/auth/cloud-platform");

            var token = Task.Run(() => credential.UnderlyingCredential.GetAccessTokenForRequestAsync()).Result;

            return token;
        }
    }
}