// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using AzureMcp.Models.Option;

namespace AzureMcp.Options.AI.Deployment;

public class DeploymentListOptions : BaseAIOptions
{
    [JsonPropertyName(OptionDefinitions.AI.ModelName)]
    public string? Model { get; set; }
}
