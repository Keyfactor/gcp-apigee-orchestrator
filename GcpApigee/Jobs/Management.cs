using System;
using System.Collections.Generic;
using Google;
using Keyfactor.Extensions.Orchestrator.GcpApigee.Client;
using Keyfactor.Extensions.Orchestrator.GcpApigee.Models;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.GcpApigee.Jobs
{
    public class Management : JobBase, IManagementJobExtension
    {
        public Management(IPAMSecretResolver resolver)
        {
            _resolver = resolver;
            _logger = LogHandler.GetClassLogger(this.GetType());
        }

        private readonly ILogger _logger;

        public Management(ILogger<Management> logger)
        {
            _logger = logger;
        }

        public JobResult ProcessJob(ManagementJobConfiguration jobConfiguration)
        {
            try
            {
                _logger.MethodEntry();
                _logger.MethodExit();
                return PerformManagement(jobConfiguration);
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occurred in Management.ProcessJob: {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        private JobResult PerformManagement(ManagementJobConfiguration config)
        {
            try
            {
                _logger.MethodEntry();
                var complete = new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage =
                        "Invalid Management Operation"
                };

                var storeProperties =
                    JsonConvert.DeserializeObject<Dictionary<string, string>>(config.CertificateStoreDetails.Properties);

                SetPAMSecrets(storeProperties["jsonKey"], _logger);

                switch (config.OperationType.ToString())
                {
                    case "Add":
                        _logger.LogTrace("Adding...");
                        _logger.LogTrace($"Add Config Json {JsonConvert.SerializeObject(config)}");
                        complete = PerformAddition(config);
                        break;
                    case "Remove":
                        _logger.LogTrace("Removing...");
                        _logger.LogTrace($"Remove Config Json {JsonConvert.SerializeObject(config)}");
                        complete = PerformRemoval(config);
                        break;
                    case "Create":
                        _logger.LogTrace("Creating...");
                        _logger.LogTrace($"Remove Config Json {JsonConvert.SerializeObject(config)}");
                        complete = PerformCreation(config);
                        break;
                }

                _logger.MethodExit();
                return complete;
            }
            catch (GoogleApiException e)
            {
                var googleError = e.Error.ErrorResponseContent;
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage =
                        $"Management {googleError}"
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occurred in Management.PerformManagement: {LogHandler.FlattenException(e)}");
                throw;
            }
        }


        private JobResult PerformRemoval(ManagementJobConfiguration config)
        {
            try
            {
                _logger.MethodEntry();
                _logger.LogTrace(
                    $"Credentials JSON: Url: {config.CertificateStoreDetails.ClientMachine} Password: {config.ServerPassword}");

                var storeProps = JsonConvert.DeserializeObject<StorePath>(config.CertificateStoreDetails.Properties,
                    new JsonSerializerSettings {DefaultValueHandling = DefaultValueHandling.Populate});
                _logger.LogTrace($"Store Properties: {JsonConvert.SerializeObject(storeProps)}");
                GcpApigeeClient client = null;
                if (storeProps != null)
                {
                    try
                    {
                        _logger.LogTrace("Creating Api Client...");
                        client = new GcpApigeeClient(config, JsonKey);
                        _logger.LogTrace("ApiClient Created...");
                    }
                    catch (Exception ex)
                    {
                        return new JobResult
                        {
                            Result = OrchestratorJobStatusJobResult.Failure,
                            JobHistoryId = config.JobHistoryId,
                            FailureMessage =
                                $"Inventory Could Not Create Api Client {LogHandler.FlattenException(ex)}"
                        };
                    }
                }

                var r1 = client?.Remove(config.JobCertificate.Alias, config.CertificateStoreDetails.StorePath);

                if (r1 != null && r1.Status != ApiStatus.StatusCode.Success)
                {
                    _logger.MethodExit();
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Failure,
                        JobHistoryId = config.JobHistoryId,
                        FailureMessage =
                            "An Error Occured during the Remove Process"
                    };
                }

                _logger.MethodExit();
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Success,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage = ""
                };
            }
            catch (GoogleApiException e)
            {
                var googleError = e.Error.ErrorResponseContent;
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage =
                        $"Management/Remove {googleError}"
                };
            }
            catch (Exception e)
            {
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage = $"PerformRemoval: {LogHandler.FlattenException(e)}"
                };
            }
        }

        private JobResult PerformCreation(ManagementJobConfiguration config)
        {
            //Temporarily only performing additions
            try
            {
                _logger.MethodEntry();
                _logger.LogTrace(
                    $"Credentials JSON: Url: {config.CertificateStoreDetails.ClientMachine} Password: {config.ServerPassword}");

                var storeProps = JsonConvert.DeserializeObject<StorePath>(config.CertificateStoreDetails.Properties,
                    new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });
                _logger.LogTrace($"Store Properties: {JsonConvert.SerializeObject(storeProps)}");

                GcpApigeeClient client = null;
                if (storeProps != null)
                {
                    try
                    {
                        _logger.LogTrace("Creating Api Client...");
                        client = new GcpApigeeClient(config, JsonKey);
                        _logger.LogTrace("ApiClient Created...");
                    }
                    catch (Exception ex)
                    {
                        return new JobResult
                        {
                            Result = OrchestratorJobStatusJobResult.Failure,
                            JobHistoryId = config.JobHistoryId,
                            FailureMessage =
                                $"Creation Operation Could Not Create Api Client {LogHandler.FlattenException(ex)}"
                        };
                    }

                }

                var r1 = client?.Create(config.CertificateStoreDetails.StorePath,false);

                if (r1 != null && r1.Status != ApiStatus.StatusCode.Success)
                {
                    _logger.MethodExit();
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Failure,
                        JobHistoryId = config.JobHistoryId,
                        FailureMessage =
                            "An Error Occured during the Create Process"
                    };
                }

                _logger.MethodExit();
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Success,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage = ""
                };

            }
            catch (GoogleApiException e)
            {
                var googleError = e.Error.ErrorResponseContent;
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage =
                        $"Management/Add {googleError}"
                };
            }
            catch (Exception e)
            {
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage = $"Management/Add {LogHandler.FlattenException(e)}"
                };
            }
        }

        private JobResult PerformAddition(ManagementJobConfiguration config)
        {
            //Temporarily only performing additions
            try
            {
                _logger.MethodEntry();

                _logger.LogTrace(
                    $"Credentials JSON: Url: {config.CertificateStoreDetails.ClientMachine} Password: {config.ServerPassword}");

                var storeProps = JsonConvert.DeserializeObject<StorePath>(config.CertificateStoreDetails.Properties,
                    new JsonSerializerSettings {DefaultValueHandling = DefaultValueHandling.Populate});
                _logger.LogTrace($"Store Properties: {JsonConvert.SerializeObject(storeProps)}");

                GcpApigeeClient client = null;
                if (storeProps != null)
                {
                    try
                    {
                        _logger.LogTrace("Creating Api Client...");
                        client = new GcpApigeeClient(config, JsonKey);
                        _logger.LogTrace("ApiClient Created...");
                    }
                    catch (Exception ex)
                    {
                        return new JobResult
                        {
                            Result = OrchestratorJobStatusJobResult.Failure,
                            JobHistoryId = config.JobHistoryId,
                            FailureMessage =
                                $"Addition Operation Could Not Create Api Client {LogHandler.FlattenException(ex)}"
                        };
                    }

                }

                var r1 = client?.Add(config.JobCertificate.Contents, config.JobCertificate.PrivateKeyPassword,
                    config.JobCertificate.Alias, config.Overwrite);

                if (r1 != null && r1.Status != ApiStatus.StatusCode.Success)
                {
                    _logger.MethodExit();
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Failure,
                        JobHistoryId = config.JobHistoryId,
                        FailureMessage =r1.Message
                    };
                }

                _logger.MethodExit();
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Success,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage = ""
                };

            }
            catch (GoogleApiException e)
            {
                var googleError = e.Error.ErrorResponseContent;
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage =
                        $"Management/Add {googleError}"
                };
            }
            catch (Exception e)
            {
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Failure,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage = $"Management/Add {LogHandler.FlattenException(e)}"
                };
            }
        }

    }
}