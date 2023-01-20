using Microsoft.Extensions.Options;
using OhMyGPA.Bot.Models.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace OhMyGPA.Bot.Models.Implements;

public class TelegramBot : IBotClient
{
    private readonly ITelegramBotClient _bot;
    
    public TelegramBot(ITelegramBotClient botClient)
    {
        _bot = botClient;
    }
    
    public async Task SendMessage(long chatId, string text,
        CancellationToken cancellationToken)
    {
        await _bot.SendTextMessageAsync(
            chatId: chatId,
            text: text,
            cancellationToken: cancellationToken);
    }
}

public class TelegramBotConfigure : IHostedService
{
    private readonly BotConfiguration _botConfig;
    private readonly ILogger<TelegramBotConfigure> _logger;
    private readonly IServiceProvider _serviceProvider;

    public TelegramBotConfigure(
        ILogger<TelegramBotConfigure> logger,
        IServiceProvider serviceProvider,
        IOptions<BotConfiguration> botOptions)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _botConfig = botOptions.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var botClient = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();

        // Configure custom endpoint per Telegram API recommendations:
        // https://core.telegram.org/bots/api#setwebhook
        // If you'd like to make sure that the webhook was set by you, you can specify secret data
        // in the parameter secret_token. If specified, the request will contain a header
        // "X-Telegram-Bot-Api-Secret-Token" with the secret token as content.
        var webhookAddress = $"{_botConfig.HostAddress}{_botConfig.Route}";
        _logger.LogInformation("Setting webhook: {WebhookAddress}", webhookAddress);
        await botClient.SetWebhookAsync(
            webhookAddress,
            allowedUpdates: Array.Empty<UpdateType>(),
            cancellationToken: cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var botClient = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();

        // Remove webhook on app shutdown
        _logger.LogInformation("Removing webhook");
        await botClient.DeleteWebhookAsync(cancellationToken: cancellationToken);
    }
}