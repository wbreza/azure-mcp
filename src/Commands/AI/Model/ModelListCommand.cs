// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Commands.AI;
using AzureMcp.Models;
using AzureMcp.Models.AI;
using AzureMcp.Options.AI.Model;
using AzureMcp.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AzureMcp.Commands.AI.Model;

public sealed class ModelListCommand(ILogger<ModelListCommand> logger) 
    : BaseAICommand<ModelListOptions>
{
    private const string CommandTitle = "List AI Models";
    private readonly ILogger<ModelListCommand> _logger = logger;

    public override string Name => "list";

    public override string Description =>
        """
        Lists all available AI models for the specified AI service.
        Returns model details including name, version, publisher, and capabilities.
        
        Required options:
        - service-name: The name of the AI service to query
        """;

    public override string Title => CommandTitle;

    [McpServerTool(
        Destructive = false,
        ReadOnly = true,
        Title = CommandTitle)]
    public override async Task<CommandResponse> ExecuteAsync(CommandContext context, ParseResult parseResult)
    {
        var options = BindOptions(parseResult);

        try
        {
            if (!Validate(parseResult.CommandResult, context.Response).IsValid)
            {
                return context.Response;
            }

            var service = context.GetService<IAIService>();
            var results = await service.ListModelsAsync(options);

            context.Response.Results = results?.Count > 0 ? 
                ResponseResult.Create(
                    new ModelListCommandResult(results),
                    AIJsonContext.Default.ModelListCommandResult) : 
                null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error listing AI models. Service: {Service}, Subscription: {Subscription}, Options: {@Options}", 
                options.Service, options.Subscription, options);
            HandleException(context.Response, ex);
        }

        return context.Response;
    }
}

internal record ModelListCommandResult(List<AIModel> Models);
