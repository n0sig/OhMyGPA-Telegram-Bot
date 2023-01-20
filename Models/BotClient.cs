using Telegram.Bot;

namespace OhMyGPA.Bot.Models;

public interface IBotClient
{
    public Task SendMessage(long chatId, string text, CancellationToken cancellationToken);
}

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