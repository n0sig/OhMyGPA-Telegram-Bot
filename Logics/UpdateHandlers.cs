using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using OhMyGPA.Telegram.Bot.Models;

namespace OhMyGPA.Telegram.Bot.Logics;

public class UpdateHandlers
{
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<UpdateHandlers> _logger;
    private readonly BotUser _users;

    public UpdateHandlers(ITelegramBotClient botClient, ILogger<UpdateHandlers> logger, BotUser users)
    {
        _botClient = botClient;
        _logger = logger;
        _users = users;
    }

    public async Task HandleUpdateAsync(Update update, CancellationToken cancellationToken)
    {
        var handler = update switch
        {
            { Message: { } message } => BotOnMessageReceived(message, cancellationToken),
            _ => UnknownUpdateHandlerAsync(update, cancellationToken)
        };
        await handler;
    }

    private async Task BotOnMessageReceived(Message message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Receive message type: {MessageType}", message.Type);
        if (message.Text is not { } messageText)
            return;

        // Restore UserInfo from cache
        var chatId = message.Chat.Id;
        var dialogUser = await _users.GetDialogUser(chatId, cancellationToken);

        switch (Cmd: dialogUser.CmdType, Msg: dialogUser.RcvMsgType, messageText)
        {
            case (CmdType.None, _, "/once"):
            {
                if (_users.IsSubscribeUser(chatId, cancellationToken))
                {
                    var subscribeUser = _users.GetSubscribeUser(chatId, cancellationToken);
                    await SendMessage(_botClient, message, cancellationToken, Reply.SubscribeQuerying);
                    try
                    {
                        var cjcxJson = await ZjuApi.Cjcx.GetTranscript(subscribeUser.Cookie);
                        var transcript = new Transcript(cjcxJson);
                        await SendMessage(_botClient, message, cancellationToken, Reply.QuerySuccess + transcript);
                        subscribeUser.LastQueryCourseCount = transcript.CourseCount;
                        await _users.AddSubscribeUser(chatId, subscribeUser, cancellationToken);
                    }
                    catch (Exception e)
                    {
                        await SendMessage(_botClient, message, cancellationToken, Reply.QueryFail + e.Message);
                    }
                    await _users.RemoveDialogUser(chatId, cancellationToken);
                }
                else
                {
                    await SendMessage(_botClient, message, cancellationToken, Reply.VerifyMethodUsage);
                    dialogUser.CmdType = CmdType.Query;
                    dialogUser.RcvMsgType = RcvMsgType.Normal;
                    await _users.SaveDialogUser(chatId, dialogUser, cancellationToken);
                }
                break;
            }
            case (CmdType.None, _, "/sub"):
            {
                await SendMessage(_botClient, message, cancellationToken, Reply.VerifyMethodUsage);
                dialogUser.CmdType = CmdType.Subscribe;
                dialogUser.RcvMsgType = RcvMsgType.Normal;
                await _users.SaveDialogUser(chatId, dialogUser, cancellationToken);
                break;
            }
            case (CmdType.None, _, "/unsub"):
            {
                await SendMessage(_botClient, message, cancellationToken, "正在取消订阅……");
                if(await _users.RemoveSubscribeUser(chatId, cancellationToken))
                    await SendMessage(_botClient, message, cancellationToken, "取消订阅成功");
                else
                    await SendMessage(_botClient, message, cancellationToken, "您似乎没有订阅");
                await _users.RemoveDialogUser(chatId, cancellationToken);
                break;
            }
            case (CmdType.Query or CmdType.Subscribe, RcvMsgType.Normal, "/zjuam"):
            {
                await SendMessage(_botClient, message, cancellationToken, Reply.EnterUsername);
                dialogUser.RcvMsgType = RcvMsgType.Username;
                await _users.SaveDialogUser(chatId, dialogUser, cancellationToken);
                break;
            }
            case (CmdType.Query or CmdType.Subscribe, RcvMsgType.Normal, "/cookie"):
            {
                await SendMessage(_botClient, message, cancellationToken, Reply.EnterCookie);
                dialogUser.RcvMsgType = RcvMsgType.Cookie;
                await _users.SaveDialogUser(chatId, dialogUser, cancellationToken);
                break;
            }
            case (CmdType.Query or CmdType.Subscribe, RcvMsgType.Username, _):
            {
                await SendMessage(_botClient, message, cancellationToken, Reply.EnterPassword);
                dialogUser.CachedUsername = messageText;
                dialogUser.RcvMsgType = RcvMsgType.Password;
                await _users.SaveDialogUser(chatId, dialogUser, cancellationToken);
                break;
            }
            case (CmdType.Query or CmdType.Subscribe, RcvMsgType.Password or RcvMsgType.Cookie, _):
            {
                await SendMessage(_botClient, message, cancellationToken, Reply.Querying);
                try
                {
                    string cookie;
                    if (dialogUser.RcvMsgType == RcvMsgType.Password)
                    {
                        if (dialogUser.CachedUsername is null) throw new Exception("用户名为空，可能是数据库故障");
                        cookie = TrimCookie(await ZjuApi.ZjuAm.GetCookie(dialogUser.CachedUsername, messageText));
                    }
                    else
                    {
                        cookie = TrimCookie(messageText);
                    }

                    var cjcxJson = await ZjuApi.Cjcx.GetTranscript(cookie);
                    var transcript = new Transcript(cjcxJson);
                    await SendMessage(_botClient, message, cancellationToken, Reply.QuerySuccess + transcript);
                    if (dialogUser.CmdType == CmdType.Subscribe)
                    {
                        await _users.AddSubscribeUser(chatId, new BotUser.SubscribeUser
                        {
                            ChatId = message.Chat.Id,
                            Cookie = cookie,
                            LastQueryCourseCount = transcript.CourseCount,
                        }, cancellationToken);
                        await SendMessage(_botClient, message, cancellationToken, Reply.SubSuccess);
                    }
                }
                catch (Exception e)
                {
                    await SendMessage(_botClient, message, cancellationToken, Reply.QueryFail + e.Message);
                }

                await _users.RemoveDialogUser(chatId, cancellationToken);
                break;
            }
            default:
            {
                await SendMessage(_botClient, message, cancellationToken, Reply.Usage);
                await _users.RemoveDialogUser(chatId, cancellationToken);
                break;
            }
        }

        static async Task<Message> SendMessage(ITelegramBotClient botClient, Message message,
            CancellationToken cancellationToken, string text)
        {
            return await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: text,
                disableNotification: false,
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: cancellationToken);
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

    private Task UnknownUpdateHandlerAsync(Update update, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Unknown update type: {UpdateType}", update.Type);
        return Task.CompletedTask;
    }

    public Task HandleErrorAsync(Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException =>
                $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        _logger.LogInformation("HandleError: {ErrorMessage}", errorMessage);
        return Task.CompletedTask;
    }
    
    private static class Reply
    {
        public const string Usage = "欢迎使用GPA机器人，它可以：\n\n" +
                                    "/once - 通过浙大钉API查询一次成绩\n" +
                                    "/sub - 订阅成绩变动，服务器将每15分钟查询一次成绩\n" +
                                    "/unsub - 取消订阅\n" +
                                    "\n机器人可通过两种方式登录教务网：\n" +
                                    "1. 学号 + 统一身份认证密码\n" +
                                    "2. Cookies\n" +
                                    "\n一次查询模式下，机器人不会记录任何数据。订阅模式下，机器人不会记录您的学号和密码，但是会记录Cookies，以供定时查询。当Cookie失效时，机器人将通知您。";
        public const string VerifyMethodUsage = "请选择身份验证方式：\n" +
                                                "/zjuam 统一身份认证账号和密码\n" +
                                                "/cookie 名为iPlanetDirectoryPro的Cookie";
        public const string EnterUsername = "好的，请输入您的学号：";
        public const string EnterPassword = "收到，请继续输入统一身份认证密码：";
        public const string EnterCookie = "好的，请输入您的Cookie，其开头包含：" +
                                          "\"iPlanetDirectoryPro=\"，内容不含空格，并以分号结尾：";
        public const string Querying = "收到，正在尝试获取成绩……";
        public const string SubscribeQuerying = "您已有订阅，正在尝试获取成绩……";
        public const string QuerySuccess = "统一身份认证成功，您的成绩为：\n";
        public const string QueryFail = "获取成绩失败，错误信息：\n";
        public const string SubSuccess = "订阅成功，您将在成绩有变动时收到通知。此外，您还可以通过 /once 来主动获取成绩。";
    }
}