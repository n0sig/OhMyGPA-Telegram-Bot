using OhMyGPA.Bot.Models;

namespace OhMyGPA.Bot.Logics;

public class PeriodicalCheck : IHostedService, IDisposable
{
    private readonly IBotClient _botClient;
    private readonly ILogger<PeriodicalCheck> _logger;
    private readonly IUserManager _userManager;
    private CancellationToken _cancellationToken;
    private Timer? _timer;

    public PeriodicalCheck(ILogger<PeriodicalCheck> logger, IUserManager userManager, IBotClient botClient)
    {
        _logger = logger;
        _userManager = userManager;
        _botClient = botClient;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        await _userManager.LoadAllSubscribeUsers();
        _timer = new Timer(DoCheck, null, TimeSpan.Zero,
            TimeSpan.FromMinutes(2));
        _logger.LogInformation("Periodical check service has started");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        _timer?.Change(Timeout.Infinite, 0);
        await _userManager.SaveAllSubscribeUsers();
        _logger.LogInformation("Periodical check service has stopped");
    }

    private async void DoCheck(object? state)
    {
        _logger.LogInformation(
            "Doing periodical check");

        var userList = _userManager.GetAllSubscribeUsers(_cancellationToken);

        Parallel.ForEach(userList, userEncrypted =>
        {
            _logger.LogError("Checking user {ChatIdHash}", userEncrypted.Key);
            var user = _userManager.DecryptSubscribeUser(userEncrypted.Value, _cancellationToken);
            if (user == null)
            {
                _userManager.CompareRemoveSubscribeUser(userEncrypted, _cancellationToken);
                return;
            }
            
            try
            {
                var transcript = ZjuApi.Cjcx.GetTranscript(user.Cookie).Result;
                if (transcript.CourseCount == user.LastQueryCourseCount) return;
                user.LastQueryCourseCount = transcript.CourseCount;
                if (_userManager.CompareAddSubscribeUser(userEncrypted, user, _cancellationToken))
                {
                    _botClient.SendMessage(
                        user.ChatId,
                        "成绩变动通知：\n" + transcript,
                        cancellationToken: _cancellationToken);
                }
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Error when checking user {ChatId}", user.ChatId);
                if (_userManager.CompareRemoveSubscribeUser(userEncrypted, _cancellationToken))
                {
                    _botClient.SendMessage(
                        user.ChatId,
                        "查询失败，可能是Cookie过期，已为您取消订阅",
                        cancellationToken: _cancellationToken);
                }
            }
        });

        await _userManager.SaveAllSubscribeUsers();

        _logger.LogInformation("Periodical check completed");
    }
}