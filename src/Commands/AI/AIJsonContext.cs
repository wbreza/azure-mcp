// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using AzureMcp.Commands.AI.Account;
using AzureMcp.Commands.AI.Deployment;
using AzureMcp.Commands.AI.Model;

namespace AzureMcp.Commands.AI;

[JsonSerializable(typeof(AccountListCommandResult))]
[JsonSerializable(typeof(ModelListCommandResult))]
[JsonSerializable(typeof(DeploymentListCommandResult))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class AIJsonContext : JsonSerializerContext
{
}
