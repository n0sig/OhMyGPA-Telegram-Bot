using Newtonsoft.Json;
using OhMyGPA.Bot.Models.Implements;
using OhMyGPA.Bot.Models.Interfaces;

namespace OhMyGPA.Bot.Logics;

public class PeriodicalCheck : IHostedService, IDisposable
{
    private readonly AesCrypto _aes;
    private readonly IBotClient _botClient;
    private readonly ILogger<PeriodicalCheck> _logger;
    private readonly IUserManager _userManager;
    private CancellationToken _cancellationToken;
    private Timer? _timer;

    public PeriodicalCheck(ILogger<PeriodicalCheck> logger, IUserManager userManager, AesCrypto aes, IBotClient botClient)
    {
        _logger = logger;
        _userManager = userManager;
        _aes = aes;
        _botClient = botClient;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        _timer = new Timer(DoCheck, null, TimeSpan.Zero,
            TimeSpan.FromMinutes(5));
        _logger.LogInformation("Periodical check service has started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        _timer?.Change(Timeout.Infinite, 0);
        _logger.LogInformation("Periodical check service has started");
        return Task.CompletedTask;
    }

    private async void DoCheck(object? state)
    {
        _logger.LogInformation(
            "Doing periodical check");

        var toUpdateUsers = new Dictionary<string, byte[]>();
        var toDeleteUsers = new List<string>();

        var userList = _userManager.GetAllSubscribeUsers(_cancellationToken);

        foreach (var userEncrypted in userList)
        {
            var user = _userManager.DecryptSubscribeUser(userEncrypted.Value);
            if (user == null)
            {
                toDeleteUsers.Add(userEncrypted.Key);
                continue;
            }
            try
            {
                var transcript = await ZjuApi.Cjcx.GetTranscript(user.Cookie);
                if (transcript.CourseCount != user.LastQueryCourseCount)
                {
                    user.LastQueryCourseCount = transcript.CourseCount;
                    toUpdateUsers.Add(userEncrypted.Key, _aes.Encrypt(JsonConvert.SerializeObject(user)));
                    await _botClient.SendMessage(
                        user.ChatId,
                        "成绩变动通知：\n" + transcript,
                        cancellationToken: _cancellationToken);
                }
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Error when checking user {0}", user.ChatId);
                toDeleteUsers.Add(userEncrypted.Key);
                await _botClient.SendMessage(
                    user.ChatId,
                    "查询失败，已为您取消订阅\n错误信息：\n" + e.Message,
                    cancellationToken: _cancellationToken);
            }
        }

        foreach (var user in toUpdateUsers) await _userManager.UpdateSubscribeUser(user.Key, user.Value, _cancellationToken);

        foreach (var user in toDeleteUsers) await _userManager.RemoveSubscribeUser(user, _cancellationToken);

        _logger.LogInformation("Periodical check completed");
    }
}