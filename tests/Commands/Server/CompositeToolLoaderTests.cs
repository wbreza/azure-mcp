// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using NSubstitute;
using Xunit;
using ModelContextProtocol.Server;
using AzureMcp.Commands;
using Microsoft.Extensions.DependencyInjection;
using AzureMcp.Commands.Server;
using AzureMcp.Commands.Server.Tools;

namespace AzureMcp.Tests.Commands.Server;

public class CompositeToolLoaderCapabilityTests
{
    [Fact]
    public async Task ListToolsHandler_ReturnsCombinedToolsFromAllLoaders()
    {
        // Arrange
        var logger = Substitute.For<ILogger<CompositeToolLoader>>();
        var server = Substitute.For<IMcpServer>();
        var request = new RequestContext<ListToolsRequestParams>(server);
        var cancellationToken = CancellationToken.None;

        var commandFactoryLogger = Substitute.For<ILogger<CommandFactory>>();
        var serviceProvider = new ServiceCollection().AddLogging().BuildServiceProvider();
        var commandFactory = new CommandFactory(serviceProvider, commandFactoryLogger);

        var commandGroupLoaderLogger = Substitute.For<ILogger<CommandFactoryToolLoader>>();
        var commandGroupToolLoader = new CommandFactoryToolLoader(serviceProvider, commandFactory, commandGroupLoaderLogger);

        var registryLoader = new RegistryLoader();
        var registryLoaderLogger = Substitute.For<ILogger<RegistryToolLoader>>();
        var registryToolLoader = new RegistryToolLoader(registryLoader, registryLoaderLogger);

        var compositeToolLoader = new CompositeToolLoader(logger);
        compositeToolLoader.AddToolLoader(registryToolLoader);
        compositeToolLoader.AddToolLoader(commandGroupToolLoader);

        // Act
        var result = await compositeToolLoader.ListToolsHandler(request, cancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Tools);
    }
}
