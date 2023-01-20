namespace OhMyGPA.Bot.Models.Interfaces;

public interface IBotClient
{
    public Task SendMessage(long chatId, string text, CancellationToken cancellationToken);
}