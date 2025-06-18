// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using AzureMcp.Models.Option;

namespace AzureMcp.Options.AI;

public class BaseAIOptions : SubscriptionOptions
{
    [JsonPropertyName(OptionDefinitions.AI.ServiceName)]
    public string? Service { get; set; }
}
