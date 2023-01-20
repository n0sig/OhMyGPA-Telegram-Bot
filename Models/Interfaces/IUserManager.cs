namespace OhMyGPA.Bot.Models.Interfaces;

public enum CmdType
{
    None,
    Query,
    Subscribe
}

public enum RcvMsgType
{
    Normal,
    Username,
    Password,
    Cookie
}

public class DialogUser
{
    public string? CachedUsername;

    // Bot command
    public CmdType CmdType;
    public RcvMsgType RcvMsgType;

    public DialogUser()
    {
        CmdType = CmdType.None;
        RcvMsgType = RcvMsgType.Normal;
    }
}

public class SubscribeUser
{
    public long ChatId;
    public string Cookie = "";
    public int LastQueryCourseCount = 0;
}

public interface IUserManager
{
    public Task<DialogUser> GetDialogUser(long chatId, CancellationToken cancellationToken);
    public Task SaveDialogUser(long chatId, DialogUser user, CancellationToken cancellationToken);
    public Task RemoveDialogUser(long chatId, CancellationToken cancellationToken);
    public Task AddSubscribeUser(long chatId, SubscribeUser user, CancellationToken cancellationToken);
    public Task<bool> RemoveSubscribeUser(long chatId, CancellationToken cancellationToken);
    public Task<bool> RemoveSubscribeUser(string chatIdHash, CancellationToken cancellationToken);
    public Task UpdateSubscribeUser(string key, byte[] value, CancellationToken cancellationToken);
    public SubscribeUser GetSubscribeUser(long chatId, CancellationToken cancellationToken);
    public bool IsSubscribeUser(long chatId, CancellationToken cancellationToken);
    public Dictionary<string, byte[]> GetAllSubscribeUsers(CancellationToken cancellationToken);
    public SubscribeUser? DecryptSubscribeUser(byte[] value);
}