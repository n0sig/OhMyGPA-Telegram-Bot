using System.Security.Cryptography;
using Newtonsoft.Json;
using OhMyGPA.Telegram.Bot.Models;
using Telegram.Bot;

namespace OhMyGPA.Telegram.Bot.Logics;

public class PeriodicalCheck : IHostedService, IDisposable
{
    private readonly ILogger<PeriodicalCheck> _logger;
    private readonly BotUser _users;
    private readonly AesCrypto _aes;
    private readonly ITelegramBotClient _botClient;
    private Timer? _timer;
    private CancellationToken _cancellationToken;

    public PeriodicalCheck(ILogger<PeriodicalCheck> logger, BotUser users, AesCrypto aes, ITelegramBotClient botClient)
    {
        _logger = logger;
        _users = users;
        _aes = aes;
        _botClient = botClient;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Periodical check service started");
        _cancellationToken = cancellationToken;
        _timer = new Timer(DoCheck, null, TimeSpan.Zero,
            TimeSpan.FromMinutes(15));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Periodical check service is stopping");
        _cancellationToken = cancellationToken;
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }

    private async void DoCheck(object? state)
    {
        _logger.LogInformation(
            "Doing periodical check");
        
        var toUpdateUsers = new Dictionary<string, byte[]>();
        var toDeleteUsers = new List<string>();

        var userList = _users.GetAllSubscribeUsers(_cancellationToken);

        foreach (var userEncrypted in userList)
        {
            var user = JsonConvert.DeserializeObject<BotUser.SubscribeUser>(_aes.Decrypt(userEncrypted.Value));
            if (user == null)
            {
                toDeleteUsers.Add(userEncrypted.Key);
                continue;
            }
            try
            {
                var transcriptStr = await ZjuApi.Cjcx.GetTranscript(user.Cookie);
                var transcript = new Transcript(transcriptStr);
                if (transcript.CourseCount != user.LastQueryCourseCount)
                {
                    user.LastQueryCourseCount = transcript.CourseCount;
                    toUpdateUsers.Add(userEncrypted.Key, _aes.Encrypt(JsonConvert.SerializeObject(user)));
                    await _botClient.SendTextMessageAsync(
                        chatId: user.ChatId,
                        text: "成绩变动通知：\n" + transcript,
                        cancellationToken: _cancellationToken);
                }
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Error when checking user {0}", user.ChatId);
                toDeleteUsers.Add(userEncrypted.Key);
                await _botClient.SendTextMessageAsync(
                    chatId: user.ChatId,
                    text: "查询失败，已为您取消订阅\n错误信息：\n" + e.Message,
                    cancellationToken: _cancellationToken);
            }
        }
        
        foreach (var user in toUpdateUsers)
        {
            await _users.UpdateSubscribeUser(user.Key, user.Value, _cancellationToken);
        }

        foreach (var user in toDeleteUsers)
        {
            await _users.RemoveSubscribeUser(user, _cancellationToken);
        }

        _logger.LogInformation("Periodical check completed");
    }
}