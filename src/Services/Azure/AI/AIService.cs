// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.CognitiveServices;
using Azure.ResourceManager.Resources;
using AzureMcp.Models.AI;
using AzureMcp.Options.AI.Account;
using AzureMcp.Options.AI.Deployment;
using AzureMcp.Options.AI.Model;
using AzureMcp.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AzureMcp.Services.Azure.AI;

public sealed class AIService(
    ILogger<AIService> logger,
    ArmClient armClient) : IAIService
{
    private readonly ILogger<AIService> _logger = logger;
    private readonly ArmClient _armClient = armClient;

    public async Task<List<AIAccount>> ListAccountsAsync(AccountListOptions options)
    {
        _logger.LogInformation("Listing AI accounts for subscription: {Subscription}", options.Subscription);

        var subscription = await _armClient.GetDefaultSubscriptionAsync();
        var accounts = new List<AIAccount>();

        await foreach (var resourceGroup in subscription.GetResourceGroups())
        {
            await foreach (var cognitiveAccount in resourceGroup.GetCognitiveServicesAccounts())
            {
                var accountData = cognitiveAccount.Data;
                
                // Filter by service name if specified
                if (!string.IsNullOrEmpty(options.Service) && 
                    !accountData.Name.Contains(options.Service, StringComparison.OrdinalIgnoreCase))
                    continue;

                accounts.Add(new AIAccount
                {
                    Name = accountData.Name,
                    Location = accountData.Location.Name,
                    ResourceGroup = resourceGroup.Data.Name,
                    Kind = accountData.Kind ?? "Unknown",
                    Sku = accountData.Sku?.Name ?? "Unknown",
                    Endpoint = accountData.Properties?.Endpoint ?? string.Empty,
                    Tags = accountData.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, string>()
                });
            }
        }

        _logger.LogInformation("Found {Count} AI accounts", accounts.Count);
        return accounts;
    }    public async Task<List<AIModel>> ListModelsAsync(ModelListOptions options)
    {
        _logger.LogInformation("Listing AI models for service: {Service}", options.Service);

        if (string.IsNullOrEmpty(options.Service))
        {
            throw new ArgumentException("Service name is required for listing models", nameof(options.Service));
        }

        var subscription = await _armClient.GetDefaultSubscriptionAsync();
        var models = new List<AIModel>();

        // Find the cognitive services account
        await foreach (var resourceGroup in subscription.GetResourceGroups())
        {
            await foreach (var cognitiveAccount in resourceGroup.GetCognitiveServicesAccounts())
            {
                if (cognitiveAccount.Data.Name.Equals(options.Service, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        // For cognitive services, we can return common model information
                        // The actual available models depend on the service kind
                        var kind = cognitiveAccount.Data.Kind ?? "Unknown";
                        
                        // Add a placeholder model based on the service kind
                        models.Add(new AIModel
                        {
                            Name = $"{kind} Model",
                            Version = "Latest",
                            Publisher = "Microsoft",
                            Description = $"AI model for {kind} service",
                            Capabilities = [kind],
                            Properties = new Dictionary<string, object>
                            {
                                ["ServiceKind"] = kind,
                                ["AccountName"] = cognitiveAccount.Data.Name
                            }
                        });
                    }
                    catch (RequestFailedException ex)
                    {
                        _logger.LogWarning("Failed to retrieve models for account {Account}: {Error}", 
                            options.Service, ex.Message);
                    }
                    break;
                }
            }
        }

        _logger.LogInformation("Found {Count} AI models", models.Count);
        return models;
    }

    public async Task<List<AIDeployment>> ListDeploymentsAsync(DeploymentListOptions options)
    {
        _logger.LogInformation("Listing AI deployments for service: {Service}", options.Service);

        if (string.IsNullOrEmpty(options.Service))
        {
            throw new ArgumentException("Service name is required for listing deployments", nameof(options.Service));
        }

        var subscription = await _armClient.GetDefaultSubscriptionAsync();
        var deployments = new List<AIDeployment>();

        // Find the cognitive services account
        await foreach (var resourceGroup in subscription.GetResourceGroups())
        {
            await foreach (var cognitiveAccount in resourceGroup.GetCognitiveServicesAccounts())
            {
                if (cognitiveAccount.Data.Name.Equals(options.Service, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        // Get deployments for this account
                        await foreach (var deployment in cognitiveAccount.GetCognitiveServicesAccountDeployments())
                        {
                            var deploymentData = deployment.Data;
                            
                            // Filter by model name if specified
                            if (!string.IsNullOrEmpty(options.Model) && 
                                !deploymentData.Properties?.Model?.Name?.Contains(options.Model, StringComparison.OrdinalIgnoreCase) == true)
                                continue;                            deployments.Add(new AIDeployment
                            {
                                Name = deploymentData.Name,
                                ModelName = deploymentData.Properties?.Model?.Name ?? "Unknown",
                                ModelVersion = deploymentData.Properties?.Model?.Version ?? "Unknown",
                                Status = deploymentData.Properties?.ProvisioningState?.ToString() ?? "Unknown",
                                Capacity = deploymentData.Properties?.CurrentCapacity,
                                SkuName = deploymentData.Sku?.Name ?? "Unknown",
                                CreatedAt = deploymentData.SystemData?.CreatedOn?.DateTime ?? DateTime.MinValue,
                                UpdatedAt = deploymentData.SystemData?.LastModifiedOn?.DateTime ?? DateTime.MinValue,
                                Properties = new Dictionary<string, object>
                                {
                                    ["ProvisioningState"] = deploymentData.Properties?.ProvisioningState?.ToString() ?? "Unknown",
                                    ["ScaleType"] = deploymentData.Properties?.ScaleSettings?.ScaleType?.ToString() ?? "Unknown"
                                }
                            });
                        }
                    }
                    catch (RequestFailedException ex)
                    {
                        _logger.LogWarning("Failed to retrieve deployments for account {Account}: {Error}", 
                            options.Service, ex.Message);
                    }
                    break;
                }
            }
        }

        _logger.LogInformation("Found {Count} AI deployments", deployments.Count);
        return deployments;
    }
}
