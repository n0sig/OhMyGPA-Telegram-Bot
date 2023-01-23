using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;

namespace OhMyGPA.Bot.Models;

// Periodical check only
public partial class UserManager : IUserManager
{
    private readonly AesCrypto _aes;
    private ConcurrentDictionary<string, byte[]> _subscribeUsers;
    private readonly ILogger<UserManager> _logger;

    public UserManager(AesCrypto aes, ILogger<UserManager> logger)
    {
        _aes = aes;
        _logger = logger;
        _subscribeUsers = new ConcurrentDictionary<string, byte[]>();
    }

    public bool AddSubscribeUser(long chatId, SubscribeUser user, CancellationToken cancellationToken)
    {
        var chatIdHash = Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(chatId.ToString())));
        var value = _aes.Encrypt(JsonConvert.SerializeObject(user));
        _subscribeUsers.TryRemove(chatIdHash, out _);
        return _subscribeUsers.TryAdd(chatIdHash, value);
    }

    public bool RemoveSubscribeUser(long chatId, CancellationToken cancellationToken)
    {
        var chatIdHash = Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(chatId.ToString())));
        return _subscribeUsers.TryRemove(chatIdHash, out _);
    }
    
    public bool CompareAddSubscribeUser(KeyValuePair<string, byte[]> oldUser, SubscribeUser user, CancellationToken cancellationToken)
    {
        var value = _aes.Encrypt(JsonConvert.SerializeObject(user));
        return _subscribeUsers.TryUpdate(oldUser.Key, value, oldUser.Value);
    }

    public bool CompareRemoveSubscribeUser(KeyValuePair<string, byte[]> oldUser, CancellationToken cancellationToken)
    {
        return _subscribeUsers.TryRemove(oldUser);
    }

    public async Task LoadAllSubscribeUsers()
    {
        try
        {
            var userListString = _aes.Decrypt(await File.ReadAllBytesAsync("./subscribes.bin"));
            var subscribeUsers = JsonConvert.DeserializeObject<ConcurrentDictionary<string, byte[]>>(userListString) ??
                                 new ConcurrentDictionary<string, byte[]>();
            _subscribeUsers = subscribeUsers;
            _logger.LogInformation("Subscribe users loaded");
        }
        catch
        {
            _logger.LogWarning("Subscribe users load failed");
        }
    }

    public async Task SaveAllSubscribeUsers()
    {
        await File.WriteAllBytesAsync("./subscribes.bin", _aes.Encrypt(JsonConvert.SerializeObject(_subscribeUsers)));
        _logger.LogInformation("Saved all subscribe users");
    }

    public ConcurrentDictionary<string, byte[]> GetAllSubscribeUsers(CancellationToken cancellationToken)
    {
        var inst = _subscribeUsers.GetType().GetMethod("MemberwiseClone",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (inst != null)
            return (ConcurrentDictionary<string, byte[]>?)inst.Invoke(_subscribeUsers, null) ?? new ConcurrentDictionary<string, byte[]>();
        return new ConcurrentDictionary<string, byte[]>();
    }

    public SubscribeUser? DecryptSubscribeUser(byte[] value, CancellationToken cancellationToken)
    {
        return JsonConvert.DeserializeObject<SubscribeUser>(_aes.Decrypt(value));
    }
}

// For Dialog Support. Remove if dialog is not needed.
// In such case, you need to add subscribe user during initialization.
public partial class UserManager
{
    private readonly MemoryCache _dialogUsers = new(new MemoryCacheOptions());
    public DialogUser GetDialogUser(long chatId, CancellationToken cancellationToken)
    {
        var chatIdHash = Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(chatId.ToString())));
        var userStateString = _aes.Decrypt(_dialogUsers.Get<byte[]>(chatIdHash));
        return JsonConvert.DeserializeObject<DialogUser>(userStateString) ?? new DialogUser();
    }

    public byte[] SaveDialogUser(long chatId, DialogUser user, CancellationToken cancellationToken)
    {
        var chatIdHash = Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(chatId.ToString())));
        var userStateString = _aes.Encrypt(JsonConvert.SerializeObject(user));
        return _dialogUsers.Set(chatIdHash, userStateString, TimeSpan.FromMinutes(15));
    }

    public void RemoveDialogUser(long chatId, CancellationToken cancellationToken)
    {
        var chatIdHash = Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(chatId.ToString())));
        _dialogUsers.Remove(chatIdHash);
    }

    public SubscribeUser? GetSubscribeUser(long chatId, CancellationToken cancellationToken)
    {
        var chatIdHash = Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(chatId.ToString())));
        _subscribeUsers.TryGetValue(chatIdHash, out var value);
        return JsonConvert.DeserializeObject<SubscribeUser>(_aes.Decrypt(value));
    }
}