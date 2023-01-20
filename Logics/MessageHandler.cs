using OhMyGPA.Bot.Models;
using ZjuApi;

namespace OhMyGPA.Bot.Logics;

public class MessageHandler
{
    private readonly IBotClient _botClient;
    private readonly IUserManager _userManager;
    private readonly ILogger<MessageHandler> _logger;

    public MessageHandler(IBotClient botClient, IUserManager userManager, ILogger<MessageHandler> logger)
    {
        _botClient = botClient;
        _userManager = userManager;
        _logger = logger;
    }
    
    public async Task BotOnMessageReceived(long chatId, string? messageText, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Receive text message from: {ChatId}", chatId);
        if (messageText == null) return;

        // Read dialog user status from database
        var dialogUser = await _userManager.GetDialogUser(chatId, cancellationToken);

        switch (Cmd: dialogUser.CmdType, Msg: dialogUser.RcvMsgType, messageText)
        {
            case (CmdType.None, _, "/once"):
            {
                if (_userManager.IsSubscribeUser(chatId, cancellationToken))
                {
                    var subscribeUser = _userManager.GetSubscribeUser(chatId, cancellationToken);
                    await _botClient.SendMessage(chatId, Reply.SubscribeQuerying, cancellationToken);
                    try
                    {
                        var transcript = await Cjcx.GetTranscript(subscribeUser.Cookie);
                        await _botClient.SendMessage(chatId, Reply.QuerySuccess + transcript, cancellationToken);
                        subscribeUser.LastQueryCourseCount = transcript.CourseCount;
                        await _userManager.AddSubscribeUser(chatId, subscribeUser, cancellationToken);
                    }
                    catch (Exception e)
                    {
                        await _botClient.SendMessage(chatId, Reply.QueryFail + e.Message, cancellationToken);
                    }

                    await _userManager.RemoveDialogUser(chatId, cancellationToken);
                }
                else
                {
                    await _botClient.SendMessage(chatId, Reply.VerifyMethodSelect, cancellationToken);
                    dialogUser.CmdType = CmdType.Query;
                    dialogUser.RcvMsgType = RcvMsgType.Normal;
                    await _userManager.SaveDialogUser(chatId, dialogUser, cancellationToken);
                }

                break;
            }
            case (CmdType.None, _, "/sub"):
            {
                await _botClient.SendMessage(chatId, Reply.VerifyMethodSelect, cancellationToken);
                dialogUser.CmdType = CmdType.Subscribe;
                dialogUser.RcvMsgType = RcvMsgType.Normal;
                await _userManager.SaveDialogUser(chatId, dialogUser, cancellationToken);
                break;
            }
            case (CmdType.None, _, "/unsub"):
            {
                await _botClient.SendMessage(chatId, "正在取消订阅……", cancellationToken);
                if (await _userManager.RemoveSubscribeUser(chatId, cancellationToken))
                    await _botClient.SendMessage(chatId, "取消订阅成功", cancellationToken);
                else
                    await _botClient.SendMessage(chatId, "您似乎没有订阅", cancellationToken);
                await _userManager.RemoveDialogUser(chatId, cancellationToken);
                break;
            }
            case (CmdType.Query or CmdType.Subscribe, RcvMsgType.Normal, "/zjuam"):
            {
                await _botClient.SendMessage(chatId, Reply.EnterUsername, cancellationToken);
                dialogUser.RcvMsgType = RcvMsgType.Username;
                await _userManager.SaveDialogUser(chatId, dialogUser, cancellationToken);
                break;
            }
            case (CmdType.Query or CmdType.Subscribe, RcvMsgType.Normal, "/cookie"):
            {
                await _botClient.SendMessage(chatId, Reply.EnterCookie, cancellationToken);
                dialogUser.RcvMsgType = RcvMsgType.Cookie;
                await _userManager.SaveDialogUser(chatId, dialogUser, cancellationToken);
                break;
            }
            case (CmdType.Query or CmdType.Subscribe, RcvMsgType.Username, _):
            {
                await _botClient.SendMessage(chatId, Reply.EnterPassword, cancellationToken);
                dialogUser.CachedUsername = messageText;
                dialogUser.RcvMsgType = RcvMsgType.Password;
                await _userManager.SaveDialogUser(chatId, dialogUser, cancellationToken);
                break;
            }
            case (CmdType.Query or CmdType.Subscribe, RcvMsgType.Password or RcvMsgType.Cookie, _):
            {
                await _botClient.SendMessage(chatId, Reply.Querying, cancellationToken);
                try
                {
                    string cookie;
                    if (dialogUser.RcvMsgType == RcvMsgType.Password)
                    {
                        if (dialogUser.CachedUsername is null) throw new Exception("用户名为空，可能是数据库故障");
                        cookie = TrimCookie(await ZjuAm.GetCookie(dialogUser.CachedUsername, messageText));
                    }
                    else
                    {
                        cookie = TrimCookie(messageText);
                    }
                    
                    var transcript = await Cjcx.GetTranscript(cookie);
                    await _botClient.SendMessage(chatId, Reply.QuerySuccess + transcript, cancellationToken);
                    if (dialogUser.CmdType == CmdType.Subscribe)
                    {
                        await _userManager.AddSubscribeUser(chatId, new SubscribeUser
                        {
                            ChatId = chatId,
                            Cookie = cookie,
                            LastQueryCourseCount = transcript.CourseCount
                        }, cancellationToken);
                        await _botClient.SendMessage(chatId, Reply.SubscribeSuccess, cancellationToken);
                    }
                }
                catch (Exception e)
                {
                    await _botClient.SendMessage(chatId, Reply.QueryFail + e.Message, cancellationToken);
                }

                await _userManager.RemoveDialogUser(chatId, cancellationToken);
                break;
            }
            default:
            {
                await _botClient.SendMessage(chatId, Reply.Usage, cancellationToken);
                await _userManager.RemoveDialogUser(chatId, cancellationToken);
                break;
            }
        }
        
        static string TrimCookie(string cookie)
        {
            cookie = cookie.Trim();
            if (cookie.StartsWith("iPlanetDirectoryPro="))
                cookie = cookie.Substring(20);
            if (cookie.EndsWith(';')) cookie = cookie.Substring(0, cookie.Length - 1);
            return cookie;
        }
    }
    
    public Task UnknownUpdateHandlerAsync(string type, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Unknown update type {}", type);
        return Task.CompletedTask;
    }

    private static class Reply
    {
        public const string Usage = "欢迎使用机器人，它可以：\n" +
                                    "\n/once - 通过浙大钉API查询一次成绩\n" +
                                    "/sub - 订阅成绩变动，服务器将每5分钟查询一次成绩\n" +
                                    "/unsub - 取消订阅\n" +
                                    "\n机器人可通过两种方式登录教务网：\n" +
                                    "1. 学号 + 统一身份认证密码\n" +
                                    "2. Cookies\n" +
                                    "\n一次查询模式下，机器人不会记录任何数据。订阅模式下，机器人不会记录您的学号和密码，但是会记录Cookies，以便定时查询。当Cookie失效时，机器人将通知您。";
        public const string VerifyMethodSelect = "请选择身份验证方式：\n" +
                                                "/zjuam 统一身份认证账号和密码\n" +
                                                "/cookie 名为iPlanetDirectoryPro的Cookie";

        public const string EnterUsername = "好的，请输入您的学号：";
        public const string EnterPassword = "收到，请继续输入统一身份认证密码：";
        public const string EnterCookie = "好的，请输入您的Cookie，其开头包含：" +
                                          "\"iPlanetDirectoryPro=\"，内容不含空格，并以分号结尾：";

        public const string Querying = "收到，正在尝试获取成绩……";
        public const string SubscribeQuerying = "您已有订阅，正在尝试获取成绩……";
        public const string QuerySuccess = "认证成功，您的成绩为：\n";
        public const string QueryFail = "获取成绩失败，错误信息：\n";
        public const string SubscribeSuccess = "订阅成功，您将在成绩变动时收到通知。\n此外，您还可以通过 /once 来主动获取成绩。";
    }
}