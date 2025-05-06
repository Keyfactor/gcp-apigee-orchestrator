using Google;
using Keyfactor.Extensions.Orchestrator.GcpApigee.Client;
using Keyfactor.Extensions.Orchestrator.GcpApigee.Models;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Keyfactor.Extensions.Orchestrator.GcpApigee.Jobs
{
    public class Inventory : JobBase, IInventoryJobExtension
    {
        public Inventory(IPAMSecretResolver resolver)
        {
            _resolver = resolver;
        }

        private readonly ILogger<Inventory> _logger;

        public Inventory(ILogger<Inventory> logger)
        {
            _logger = logger;
        }

        public JobResult ProcessJob(InventoryJobConfiguration jobConfiguration,
            SubmitInventoryUpdate submitInventoryUpdate)
        {
            try
            {
                _logger.MethodEntry();
                return PerformInventory(jobConfiguration, submitInventoryUpdate);
            }
            catch (Exception e)
            {
                _logger.LogError($"Error occured in Inventory.ProcessJob: {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        private JobResult PerformInventory(InventoryJobConfiguration config, SubmitInventoryUpdate submitInventory)
        {
            try
            {
                _logger.MethodEntry(LogLevel.Debug);
                _logger.LogTrace($"Inventory Config {JsonConvert.SerializeObject(config)}");
                _logger.LogTrace(
                    $"Client Machine: {config.CertificateStoreDetails.ClientMachine} ApiKey: {config.ServerPassword}");

                var storeProps = JsonConvert.DeserializeObject<StorePath>(config.CertificateStoreDetails.Properties,
                    new JsonSerializerSettings {DefaultValueHandling = DefaultValueHandling.Populate});
                
                SetPAMSecrets(storeProps.JsonKey, _logger);

                _logger.LogTrace($"Store Properties: {JsonConvert.SerializeObject(storeProps)}");

                GcpApigeeClient client;
                try
                {
                    _logger.LogTrace("Creating Api Client...");
                    SetPAMSecrets(storeProps.JsonKey, _logger);
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

                var warningFlag = false;
                var sb = new StringBuilder();
                sb.Append("");
                List<Certificate> certs = client.List(config.CertificateStoreDetails.StorePath);
                var inventoryItems = new List<CurrentInventoryItem>();

                inventoryItems.AddRange(certs.Select(
                    c =>
                    {
                        try
                        {
                            _logger.LogTrace(
                                $"Building Cert List Inventory Item Alias: {c.AliasName} Pem: {c.Certificates}");
                            return BuildInventoryItem(c.AliasName, c, true);
                        }
                        catch
                        {
                            _logger.LogWarning(
                                $"Could not fetch the certificate: {c?.AliasName} associated with description {c?.Certificates}.");
                            sb.Append(
                                $"Could not fetch the certificate: {c?.AliasName} associated with issuer {c?.Certificates}.{Environment.NewLine}");
                            warningFlag = true;
                            return new CurrentInventoryItem();
                        }
                    }).Where(acsii => acsii?.Certificates != null).ToList());

                

                _logger.LogTrace("Submitting Inventory To Keyfactor via submitInventory.Invoke");
                submitInventory.Invoke(inventoryItems);
                _logger.LogTrace("Submitted Inventory To Keyfactor via submitInventory.Invoke");

                _logger.MethodExit(LogLevel.Debug);
                if (warningFlag)
                {
                    _logger.LogTrace("Found Warning");
                    return new JobResult
                    {
                        Result = OrchestratorJobStatusJobResult.Warning,
                        JobHistoryId = config.JobHistoryId,
                        FailureMessage = sb.ToString()
                    };
                }

                _logger.LogTrace("Return Success");
                return new JobResult
                {
                    Result = OrchestratorJobStatusJobResult.Success,
                    JobHistoryId = config.JobHistoryId,
                    FailureMessage = sb.ToString()
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
                        $"Inventory {googleError}"
                };
            }
            catch (Exception e)
            {
                _logger.LogError($"PerformInventory Error: {LogHandler.FlattenException(e)}");
                throw;
            }
        }

        protected virtual CurrentInventoryItem BuildInventoryItem(string alias,Certificate cert, bool privateKey)
        {
            try
            {
                _logger.MethodEntry();
                _logger.LogTrace($"Alias: {alias} Pem: {cert.Certificates} PrivateKey: {privateKey}");

                _logger.LogTrace($"Got modAlias: {alias}, certAttributes and mapSettings");

                var acsi = new CurrentInventoryItem
                {
                    Alias = alias,
                    Certificates =  cert.Certificates.ToList(),
                    ItemStatus = OrchestratorInventoryItemStatus.Unknown,
                    PrivateKeyEntry = privateKey,
                    UseChainLevel = false
                };

                _logger.MethodExit();
                return acsi;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error Occurred in Inventory.BuildInventoryItem: {LogHandler.FlattenException(e)}");
                throw;
            }
        }

    }
}