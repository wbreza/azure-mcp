// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace AzureMcp.Models.AI;

public class AIAccount
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public IDictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();
}
