namespace OhMyGPA.Bot.Models;

public interface IBotClient
{
    public Task SendMessage(long chatId, string text, CancellationToken cancellationToken);
}