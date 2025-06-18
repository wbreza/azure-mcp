// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Models.AI;
using AzureMcp.Options.AI.Account;
using AzureMcp.Options.AI.Deployment;
using AzureMcp.Options.AI.Model;

namespace AzureMcp.Services.Interfaces;

public interface IAIService
{
    Task<List<AIAccount>> ListAccountsAsync(AccountListOptions options);
    Task<List<AIModel>> ListModelsAsync(ModelListOptions options);
    Task<List<AIDeployment>> ListDeploymentsAsync(DeploymentListOptions options);
}
