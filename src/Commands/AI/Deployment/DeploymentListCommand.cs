// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Commands.AI;
using AzureMcp.Models;
using AzureMcp.Models.AI;
using AzureMcp.Models.Option;
using AzureMcp.Options.AI.Deployment;
using AzureMcp.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AzureMcp.Commands.AI.Deployment;

public sealed class DeploymentListCommand(ILogger<DeploymentListCommand> logger) 
    : BaseAICommand<DeploymentListOptions>
{
    private const string CommandTitle = "List AI Model Deployments";
    private readonly ILogger<DeploymentListCommand> _logger = logger;
    private readonly Option<string> _modelOption = OptionDefinitions.AI.Model;

    public override string Name => "list";

    public override string Description =>
        """
        Lists all model deployments for the specified AI service.
        Returns deployment details including name, model, status, capacity, and timestamps.
        
        Required options:
        - service-name: The name of the AI service to query
        
        Optional filters:
        - model-name: Filter deployments by model name (partial match)
        """;

    public override string Title => CommandTitle;

    protected override void RegisterOptions(Command command)
    {
        base.RegisterOptions(command);
        command.AddOption(_modelOption);
    }

    protected override DeploymentListOptions BindOptions(ParseResult parseResult)
    {
        var options = base.BindOptions(parseResult);
        options.Model = parseResult.GetValueForOption(_modelOption);
        return options;
    }

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
            var results = await service.ListDeploymentsAsync(options);

            context.Response.Results = results?.Count > 0 ? 
                ResponseResult.Create(
                    new DeploymentListCommandResult(results),
                    AIJsonContext.Default.DeploymentListCommandResult) : 
                null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error listing AI deployments. Service: {Service}, Model: {Model}, Subscription: {Subscription}, Options: {@Options}", 
                options.Service, options.Model, options.Subscription, options);
            HandleException(context.Response, ex);
        }

        return context.Response;
    }
}

internal record DeploymentListCommandResult(List<AIDeployment> Deployments);
