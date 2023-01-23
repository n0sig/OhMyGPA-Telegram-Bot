using System.Collections.Concurrent;

namespace OhMyGPA.Bot.Models;

// Without Dialog Support
public class SubscribeUser
{
    public long ChatId;
    public string Cookie = "";
    public int LastQueryCourseCount = 0;
}

public partial interface IUserManager
{
    public bool AddSubscribeUser(long chatId, SubscribeUser user, CancellationToken cancellationToken);
    public bool RemoveSubscribeUser(long chatId, CancellationToken cancellationToken);
    public bool CompareAddSubscribeUser(KeyValuePair<string, byte[]> oldUser, SubscribeUser user, CancellationToken cancellationToken);
    public bool CompareRemoveSubscribeUser(KeyValuePair<string, byte[]> oldUser, CancellationToken cancellationToken);
    public ConcurrentDictionary<string, byte[]> GetAllSubscribeUsers(CancellationToken cancellationToken);
    public SubscribeUser? DecryptSubscribeUser(byte[] value, CancellationToken cancellationToken);
    public Task SaveAllSubscribeUsers();
    public Task LoadAllSubscribeUsers();
}

// For Dialog Support. Remove if dialog is not needed.
// In such case, you need to add subscribe user during initialization.
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
    
    public CmdType CmdType;
    public RcvMsgType RcvMsgType;

    public DialogUser()
    {
        CmdType = CmdType.None;
        RcvMsgType = RcvMsgType.Normal;
    }
}

public partial interface IUserManager
{
    public DialogUser GetDialogUser(long chatId, CancellationToken cancellationToken);
    public byte[] SaveDialogUser(long chatId, DialogUser user, CancellationToken cancellationToken);
    public void RemoveDialogUser(long chatId, CancellationToken cancellationToken);
    public SubscribeUser? GetSubscribeUser(long chatId, CancellationToken cancellationToken);
}
