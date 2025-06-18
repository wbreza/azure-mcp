// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Commands.AI;
using AzureMcp.Models;
using AzureMcp.Models.AI;
using AzureMcp.Options.AI.Account;
using AzureMcp.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AzureMcp.Commands.AI.Account;

public sealed class AccountListCommand(ILogger<AccountListCommand> logger) 
    : BaseAICommand<AccountListOptions>
{
    private const string CommandTitle = "List AI Service Accounts";
    private readonly ILogger<AccountListCommand> _logger = logger;

    public override string Name => "list";

    public override string Description =>
        """
        Lists all Azure AI service accounts in the subscription.
        Returns account details including name, location, kind, SKU, and endpoint information.
        
        Optional filters:
        - service-name: Filter accounts by name (partial match)
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
            var results = await service.ListAccountsAsync(options);

            context.Response.Results = results?.Count > 0 ? 
                ResponseResult.Create(
                    new AccountListCommandResult(results),
                    AIJsonContext.Default.AccountListCommandResult) : 
                null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error listing AI accounts. Service: {Service}, Subscription: {Subscription}, Options: {@Options}", 
                options.Service, options.Subscription, options);
            HandleException(context.Response, ex);
        }

        return context.Response;
    }
}

internal record AccountListCommandResult(List<AIAccount> Accounts);
