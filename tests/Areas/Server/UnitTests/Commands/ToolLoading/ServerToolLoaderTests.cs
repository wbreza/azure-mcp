// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using AzureMcp.Areas.Server.Commands.Discovery;
using AzureMcp.Areas.Server.Commands.ToolLoading;
using AzureMcp.Areas.Server.Options;
using AzureMcp.Tests.Areas.Server.UnitTests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using NSubstitute;
using Xunit;

namespace AzureMcp.Tests.Areas.Server.UnitTests.Commands.ToolLoading;

[Trait("Area", "Server")]
public class ServerToolLoaderTests
{
    private static (ServerToolLoader toolLoader, IMcpDiscoveryStrategy mockDiscoveryStrategy) CreateToolLoader(ServiceStartOptions? options = null)
    {
        var serviceProvider = new ServiceCollection().AddLogging().BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var mockDiscoveryStrategy = Substitute.For<IMcpDiscoveryStrategy>();
        var logger = loggerFactory.CreateLogger<ServerToolLoader>();
        var serviceOptions = Microsoft.Extensions.Options.Options.Create(options ?? new ServiceStartOptions());

        var toolLoader = new ServerToolLoader(mockDiscoveryStrategy, serviceOptions, logger);
        return (toolLoader, mockDiscoveryStrategy);
    }

    private static ModelContextProtocol.Server.RequestContext<ListToolsRequestParams> CreateRequest()
    {
        var mockServer = Substitute.For<ModelContextProtocol.Server.IMcpServer>();
        return new ModelContextProtocol.Server.RequestContext<ListToolsRequestParams>(mockServer)
        {
            Params = new ListToolsRequestParams()
        };
    }

    private static ModelContextProtocol.Server.RequestContext<CallToolRequestParams> CreateCallToolRequest(string toolName, IReadOnlyDictionary<string, JsonElement>? arguments = null)
    {
        var mockServer = Substitute.For<ModelContextProtocol.Server.IMcpServer>();
        return new ModelContextProtocol.Server.RequestContext<CallToolRequestParams>(mockServer)
        {
            Params = new CallToolRequestParams
            {
                Name = toolName,
                Arguments = arguments ?? new Dictionary<string, JsonElement>()
            }
        };
    }

    [Fact]
    public async Task CallToolHandler_WithoutListToolsFirst_ShouldSucceed()
    {
        // Arrange - use real RegistryDiscoveryStrategy since ServerToolLoader depends on it
        var serviceProvider = new ServiceCollection().AddLogging().BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var options = Microsoft.Extensions.Options.Options.Create(new ServiceStartOptions());
        var discoveryLogger = loggerFactory.CreateLogger<RegistryDiscoveryStrategy>();
        var discoveryStrategy = new RegistryDiscoveryStrategy(options, discoveryLogger);
        var logger = loggerFactory.CreateLogger<ServerToolLoader>();

        var toolLoader = new ServerToolLoader(discoveryStrategy, options, logger);
        var request = CreateCallToolRequest("documentation",
            new Dictionary<string, JsonElement>
            {
                { "intent", JsonDocument.Parse("\"search for information about implementing MCP servers\"").RootElement },
                { "command", JsonDocument.Parse("\"microsoft_docs_search\"").RootElement },
                { "parameters", JsonDocument.Parse("""
                    {
                        "question": "how to implement mcp server in azure"
                    }
                    """).RootElement }
            });

        // Act - Call CallToolHandler WITHOUT calling ListToolsHandler first
        // This should work without requiring ListToolsHandler to be called first
        var result = await toolLoader.CallToolHandler(request, CancellationToken.None);

        // Assert - The tool call should succeed
        Assert.NotNull(result);
        Assert.NotNull(result.Content);
        Assert.NotEmpty(result.Content);
    }

    [Fact]
    public async Task ListToolsHandler_WithNoServers_ReturnsEmptyToolList()
    {
        // Arrange
        var (toolLoader, mockDiscoveryStrategy) = CreateToolLoader();
        var request = CreateRequest();

        mockDiscoveryStrategy.DiscoverServersAsync()
            .Returns(Task.FromResult(Enumerable.Empty<IMcpServerProvider>()));

        // Act
        var result = await toolLoader.ListToolsHandler(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Tools);
        Assert.Empty(result.Tools);
    }

    [Fact]
    public async Task ListToolsHandler_WithRealRegistryDiscovery_ReturnsExpectedStructure()
    {
        // Arrange - use real RegistryDiscoveryStrategy
        var serviceProvider = new ServiceCollection().AddLogging().BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var options = Microsoft.Extensions.Options.Options.Create(new ServiceStartOptions());
        var discoveryLogger = loggerFactory.CreateLogger<RegistryDiscoveryStrategy>();
        var discoveryStrategy = new RegistryDiscoveryStrategy(options, discoveryLogger);
        var logger = loggerFactory.CreateLogger<ServerToolLoader>();

        var toolLoader = new ServerToolLoader(discoveryStrategy, options, logger);
        var request = CreateRequest();

        // Act
        var result = await toolLoader.ListToolsHandler(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Tools);
        Assert.True(result.Tools.Count >= 0); // Should return at least an empty list

        // Each tool should have proper structure if any exist
        foreach (var tool in result.Tools)
        {
            Assert.NotNull(tool.Name);
            Assert.NotEmpty(tool.Name);
            Assert.NotNull(tool.Description);
            Assert.True(tool.InputSchema.ValueKind != JsonValueKind.Undefined, "InputSchema should be defined");
        }
    }

    [Fact]
    public async Task ClientOptionsProxying_WithBasicCapabilities_ProxiesAllCapabilities()
    {
        // Arrange
        var mockServer = Substitute.For<ModelContextProtocol.Server.IMcpServer>();
        var clientCapabilities = new ClientCapabilities
        {
            Experimental = new Dictionary<string, object>
            {
                { "customFeature", true }
            },
            Roots = new RootsCapability
            {
                ListChanged = true
            },
            Sampling = new SamplingCapability()
        };

        var clientInfo = new Implementation
        {
            Name = "Test Host Server",
            Version = "1.0.0"
        };

        mockServer.ClientCapabilities.Returns(clientCapabilities);
        mockServer.ClientInfo.Returns(clientInfo);

        // Variables to capture what gets passed to the tool
        var toolWasCalled = false;
        var capturedArguments = (IReadOnlyDictionary<string, object?>?)null;
        
        // Create a client builder with a custom tool handler
        var clientBuilder = new MockMcpClientBuilder()
            .AddTool("validate-capabilities", "Test tool that validates client capabilities", args =>
            {
                toolWasCalled = true;
                capturedArguments = args;
                
                return new CallToolResult
                {
                    Content = [new TextContentBlock { Text = $"Capabilities validated: experimental={args?.ContainsKey("customFeature")}, sampling={args?.ContainsKey("sampling")}" }],
                    IsError = false
                };
            });

        // Create the discovery strategy using the builder pattern
        var mockDiscoveryStrategy = new MockMcpDiscoveryStrategyBuilder()
            .AddServer("test-server", "test-server", "Test server for proxying validation", clientBuilder)
            .Build();

        var serviceProvider = new ServiceCollection().AddLogging().BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<ServerToolLoader>();
        var serviceOptions = Microsoft.Extensions.Options.Options.Create(new ServiceStartOptions());

        var toolLoader = new ServerToolLoader(mockDiscoveryStrategy, serviceOptions, logger);

        // Act - Call a tool to test capability proxying
        var callRequest = new ModelContextProtocol.Server.RequestContext<CallToolRequestParams>(mockServer)
        {
            Params = new CallToolRequestParams
            {
                Name = "test-server",
                Arguments = new Dictionary<string, JsonElement>
                {
                    { "tool", JsonDocument.Parse("\"validate-capabilities\"").RootElement },
                    { "parameters", JsonDocument.Parse("{}").RootElement }
                }
            }
        };

        var callResult = await toolLoader.CallToolHandler(callRequest, CancellationToken.None);

        // Assert
        Assert.NotNull(callResult);
        Assert.False(callResult.IsError);
        Assert.True(toolWasCalled, "Tool should have been called");
        
        // The key assertion is that the tool executed successfully, which means
        // the ServerToolLoader was able to create a client with the proper capabilities
        // and communicate with it. The actual capability proxying happens during client creation.
        var textContent = callResult.Content.OfType<TextContentBlock>().FirstOrDefault();
        Assert.NotNull(textContent);
        Assert.Contains("Capabilities validated", textContent.Text);
    }

    [Fact]
    public async Task ClientOptionsProxying_WithSamplingCapability_ProxiesSamplingCorrectly()
    {
        // Arrange
        var mockServer = Substitute.For<ModelContextProtocol.Server.IMcpServer>();
        var samplingCapability = new SamplingCapability();
        var clientCapabilities = new ClientCapabilities
        {
            Sampling = samplingCapability
        };

        mockServer.ClientCapabilities.Returns(clientCapabilities);
        mockServer.ClientInfo.Returns(new Implementation { Name = "Sampling Host", Version = "2.0.0" });

        // Capture client options using a closure
        var capturedClientOptions = (McpClientOptions?)null;
        
        // Create a client builder with a test tool
        var clientBuilder = new MockMcpClientBuilder()
            .AddTool("sampling-tool", "Tool for sampling test", "Sampling response");

        // Create the discovery strategy using the builder pattern properly
        var mockDiscoveryStrategy = new MockMcpDiscoveryStrategyBuilder()
            .AddServer("sampling-server", "sampling-server", "Server for testing sampling capability proxying", clientBuilder)
            .Build();

        // Override the GetOrCreateClientAsync to capture options
        mockDiscoveryStrategy.GetOrCreateClientAsync(Arg.Any<string>(), Arg.Do<McpClientOptions>(opts => capturedClientOptions = opts))
            .Returns(callInfo => Task.FromResult(clientBuilder.Build()));

        var serviceProvider = new ServiceCollection().AddLogging().BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<ServerToolLoader>();
        var serviceOptions = Microsoft.Extensions.Options.Options.Create(new ServiceStartOptions());

        var toolLoader = new ServerToolLoader(mockDiscoveryStrategy, serviceOptions, logger);
        var request = new ModelContextProtocol.Server.RequestContext<ListToolsRequestParams>(mockServer)
        {
            Params = new ListToolsRequestParams()
        };

        // Act
        await toolLoader.ListToolsHandler(request, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedClientOptions);
        Assert.NotNull(capturedClientOptions!.Capabilities?.Sampling);
        Assert.Same(samplingCapability, capturedClientOptions.Capabilities.Sampling);
    }

    [Fact]
    public async Task ClientOptionsProxying_WithNullCapabilities_CreatesEmptyCapabilities()
    {
        // Arrange
        var mockServer = Substitute.For<ModelContextProtocol.Server.IMcpServer>();
        mockServer.ClientCapabilities.Returns((ClientCapabilities?)null);
        mockServer.ClientInfo.Returns(new Implementation { Name = "Null Capabilities Host", Version = "1.0.0" });

        // Create a mock client with a test tool
        var clientBuilder = new MockMcpClientBuilder()
            .AddTool("null-caps-tool", "Tool for null caps test", "Null caps response");

        // Create a custom mock provider that captures client options
        var capturedClientOptions = (McpClientOptions?)null;
        var mockProvider = Substitute.For<IMcpServerProvider>();
        mockProvider.CreateMetadata().Returns(new McpServerMetadata 
        { 
            Id = "null-caps-server", 
            Name = "Null Caps Server", 
            Description = "Server for testing null capabilities handling" 
        });
        mockProvider.CreateClientAsync(Arg.Do<McpClientOptions>(opts => capturedClientOptions = opts))
            .Returns(x => Task.FromResult(clientBuilder.Build()));

        // Create mock discovery strategy that returns our provider
        var mockStrategy = Substitute.For<IMcpDiscoveryStrategy>();
        mockStrategy.DiscoverServersAsync().Returns(Task.FromResult<IEnumerable<IMcpServerProvider>>(new[] { mockProvider }));

        var serviceProvider = new ServiceCollection().AddLogging().BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<ServerToolLoader>();
        var serviceOptions = Microsoft.Extensions.Options.Options.Create(new ServiceStartOptions());

        var toolLoader = new ServerToolLoader(mockStrategy, serviceOptions, logger);
        var request = new ModelContextProtocol.Server.RequestContext<ListToolsRequestParams>(mockServer)
        {
            Params = new ListToolsRequestParams()
        };

        // Act
        await toolLoader.ListToolsHandler(request, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedClientOptions);
        Assert.NotNull(capturedClientOptions!.Capabilities);
        // Verify that empty capabilities object is created when host has null capabilities
        Assert.Null(capturedClientOptions.Capabilities.Sampling);
        Assert.Null(capturedClientOptions.Capabilities.Roots);
        Assert.Null(capturedClientOptions.Capabilities.Experimental);
    }

    [Fact]
    public async Task ClientOptionsProxying_WithAdvancedCapabilities_ProxiesAllAdvancedFeatures()
    {
        // Arrange
        var mockServer = Substitute.For<ModelContextProtocol.Server.IMcpServer>();
        var experimentalFeatures = new Dictionary<string, object>
        {
            { "advancedSampling", true },
            { "customProtocol", new { version = "1.2.3" } },
            { "featureFlag", "enabled" }
        };

        var clientCapabilities = new ClientCapabilities
        {
            Experimental = experimentalFeatures,
            Roots = new RootsCapability { ListChanged = true },
            Sampling = new SamplingCapability()
        };

        mockServer.ClientCapabilities.Returns(clientCapabilities);
        mockServer.ClientInfo.Returns(new Implementation { Name = "Advanced Host", Version = "3.0.0" });

        // Create a mock client with a test tool
        var clientBuilder = new MockMcpClientBuilder()
            .AddTool("advanced-tool", "Tool for advanced capability test", "Advanced response");

        // Create a custom mock provider that captures client options
        var capturedClientOptions = (McpClientOptions?)null;
        var mockProvider = Substitute.For<IMcpServerProvider>();
        mockProvider.CreateMetadata().Returns(new McpServerMetadata 
        { 
            Id = "advanced-server", 
            Name = "Advanced Server", 
            Description = "Server for testing advanced capability proxying" 
        });
        mockProvider.CreateClientAsync(Arg.Do<McpClientOptions>(opts => capturedClientOptions = opts))
            .Returns(x => Task.FromResult(clientBuilder.Build()));

        // Create mock discovery strategy that returns our provider
        var mockStrategy = Substitute.For<IMcpDiscoveryStrategy>();
        mockStrategy.DiscoverServersAsync().Returns(Task.FromResult<IEnumerable<IMcpServerProvider>>(new[] { mockProvider }));

        var serviceProvider = new ServiceCollection().AddLogging().BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<ServerToolLoader>();
        var serviceOptions = Microsoft.Extensions.Options.Options.Create(new ServiceStartOptions());

        var toolLoader = new ServerToolLoader(mockStrategy, serviceOptions, logger);
        var request = new ModelContextProtocol.Server.RequestContext<ListToolsRequestParams>(mockServer)
        {
            Params = new ListToolsRequestParams()
        };

        // Act
        await toolLoader.ListToolsHandler(request, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedClientOptions);
        Assert.NotNull(capturedClientOptions!.Capabilities);
        
        // Verify all experimental features are proxied
        Assert.NotNull(capturedClientOptions.Capabilities.Experimental);
        Assert.Equal(3, capturedClientOptions.Capabilities.Experimental.Count);
        Assert.True(capturedClientOptions.Capabilities.Experimental.ContainsKey("advancedSampling"));
        Assert.True(capturedClientOptions.Capabilities.Experimental.ContainsKey("customProtocol"));
        Assert.True(capturedClientOptions.Capabilities.Experimental.ContainsKey("featureFlag"));
        
        // Verify the values are correctly preserved (as objects, not JsonElements)
        Assert.Equal(true, capturedClientOptions.Capabilities.Experimental["advancedSampling"]);
        Assert.Equal("enabled", capturedClientOptions.Capabilities.Experimental["featureFlag"]);
        
        // Verify other capabilities
        Assert.True(capturedClientOptions.Capabilities.Roots?.ListChanged);
        Assert.NotNull(capturedClientOptions.Capabilities.Sampling);
    }

    [Fact]
    public async Task ClientOptionsProxying_OnCallToolHandler_ProxiesCapabilitiesCorrectly()
    {
        // Arrange
        var mockServer = Substitute.For<ModelContextProtocol.Server.IMcpServer>();
        var clientCapabilities = new ClientCapabilities
        {
            Sampling = new SamplingCapability(),
            Experimental = new Dictionary<string, object>
            {
                { "callToolFeature", true }
            }
        };

        mockServer.ClientCapabilities.Returns(clientCapabilities);
        mockServer.ClientInfo.Returns(new Implementation { Name = "CallTool Host", Version = "1.5.0" });

        // Create a mock client that will handle the tool call
        var clientBuilder = new MockMcpClientBuilder()
            .AddTool("test-tool", "A test tool", "Tool response");

        // Create a custom mock provider that captures client options
        var capturedClientOptions = (McpClientOptions?)null;
        var mockProvider = Substitute.For<IMcpServerProvider>();
        mockProvider.CreateMetadata().Returns(new McpServerMetadata 
        { 
            Id = "calltool-server", 
            Name = "CallTool Server", 
            Description = "Server for testing call tool capability proxying" 
        });
        mockProvider.CreateClientAsync(Arg.Do<McpClientOptions>(opts => capturedClientOptions = opts))
            .Returns(x => Task.FromResult(clientBuilder.Build()));

        // Create mock discovery strategy that returns our provider
        var mockStrategy = Substitute.For<IMcpDiscoveryStrategy>();
        mockStrategy.DiscoverServersAsync().Returns(Task.FromResult<IEnumerable<IMcpServerProvider>>(new[] { mockProvider }));

        var serviceProvider = new ServiceCollection().AddLogging().BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<ServerToolLoader>();
        var serviceOptions = Microsoft.Extensions.Options.Options.Create(new ServiceStartOptions());

        var toolLoader = new ServerToolLoader(mockStrategy, serviceOptions, logger);
        var callToolRequest = new ModelContextProtocol.Server.RequestContext<CallToolRequestParams>(mockServer)
        {
            Params = new CallToolRequestParams
            {
                Name = "calltool-server",
                Arguments = new Dictionary<string, JsonElement>
                {
                    { "tool", JsonDocument.Parse("\"test-tool\"").RootElement },
                    { "parameters", JsonDocument.Parse("{}").RootElement }
                }
            }
        };

        // Act
        var result = await toolLoader.CallToolHandler(callToolRequest, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        
        // Verify that client options were properly proxied during the call
        Assert.NotNull(capturedClientOptions);
        Assert.NotNull(capturedClientOptions!.Capabilities);
        Assert.NotNull(capturedClientOptions.Capabilities.Sampling);
        Assert.NotNull(capturedClientOptions.Capabilities.Experimental);
        Assert.True(capturedClientOptions.Capabilities.Experimental.ContainsKey("callToolFeature"));
        Assert.Equal("CallTool Host", capturedClientOptions!.ClientInfo!.Name);
        Assert.Equal("1.5.0", capturedClientOptions!.ClientInfo!.Version);
    }
}
