// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace AzureMcp.Models.AI;

public class AIDeployment
{
    public string Name { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? Capacity { get; set; }
    public string SkuName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
}
