// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace AzureMcp.Models.AI;

public class AIModel
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string[] Capabilities { get; set; } = [];
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
}
