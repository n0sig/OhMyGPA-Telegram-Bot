using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using StackExchange.Redis;
using OhMyGPA.Bot.Models.Interfaces;

namespace OhMyGPA.Bot.Models.Implements;

public class UserManagerWithDb: IUserManager
{
    private readonly AesCrypto _aes;
    private readonly IDatabaseAsync _db;
    private readonly Dictionary<string, byte[]> _subscribeUsers;

    public UserManagerWithDb(IDatabaseAsync db, AesCrypto aes)
    {
        _db = db;
        _aes = aes;
        var userListString = _aes.Decrypt(_db.StringGetAsync("subscribes").Result);
        _subscribeUsers = JsonConvert.DeserializeObject<Dictionary<string, byte[]>>(userListString) ??
                          new Dictionary<string, byte[]>();
    }

    public async Task<DialogUser> GetDialogUser(long chatId,
        CancellationToken cancellationToken)
    {
        var chatIdHash = SHA512.HashData(Encoding.UTF8.GetBytes(chatId.ToString()));
        var userStateString = _aes.Decrypt(await _db.StringGetAsync(chatIdHash));
        return JsonConvert.DeserializeObject<DialogUser>(userStateString) ?? new DialogUser();
    }

    public async Task SaveDialogUser(long chatId, DialogUser user,
        CancellationToken cancellationToken)
    {
        var chatIdHash = SHA512.HashData(Encoding.UTF8.GetBytes(chatId.ToString()));
        var userStateString = JsonConvert.SerializeObject(user);
        await _db.StringSetAsync(chatIdHash, _aes.Encrypt(userStateString), TimeSpan.FromMinutes(15));
    }

    public async Task RemoveDialogUser(long chatId,
        CancellationToken cancellationToken)
    {
        var chatIdHash = SHA512.HashData(Encoding.UTF8.GetBytes(chatId.ToString()));
        await _db.KeyDeleteAsync(chatIdHash);
    }

    public async Task AddSubscribeUser(long chatId, SubscribeUser user,
        CancellationToken cancellationToken)
    {
        var chatIdHash = Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(chatId.ToString())));
        if (_subscribeUsers.ContainsKey(chatIdHash)) _subscribeUsers.Remove(chatIdHash);
        _subscribeUsers.Add(chatIdHash, _aes.Encrypt(JsonConvert.SerializeObject(user)));
        await _db.StringSetAsync("subscribes", _aes.Encrypt(JsonConvert.SerializeObject(_subscribeUsers)));
    }

    public async Task<bool> RemoveSubscribeUser(long chatId,
        CancellationToken cancellationToken)
    {
        var chatIdHash = Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(chatId.ToString())));
        bool hasSubscription;
        if (_subscribeUsers.ContainsKey(chatIdHash))
        {
            _subscribeUsers.Remove(chatIdHash);
            hasSubscription = true;
        }
        else
        {
            hasSubscription = false;
        }

        await _db.StringSetAsync("subscribes", _aes.Encrypt(JsonConvert.SerializeObject(_subscribeUsers)));
        return hasSubscription;
    }

    public async Task<bool> RemoveSubscribeUser(string chatIdHash,
        CancellationToken cancellationToken)
    {
        bool hasSubscription;
        if (_subscribeUsers.ContainsKey(chatIdHash))
        {
            _subscribeUsers.Remove(chatIdHash);
            hasSubscription = true;
        }
        else
        {
            hasSubscription = false;
        }

        await _db.StringSetAsync("subscribes", _aes.Encrypt(JsonConvert.SerializeObject(_subscribeUsers)));
        return hasSubscription;
    }

    public async Task UpdateSubscribeUser(string key, byte[] value,
        CancellationToken cancellationToken)
    {
        if (_subscribeUsers.ContainsKey(key)) _subscribeUsers.Remove(key);
        _subscribeUsers.Add(key, value);
        await _db.StringSetAsync("subscribes", _aes.Encrypt(JsonConvert.SerializeObject(_subscribeUsers)));
    }

    public SubscribeUser GetSubscribeUser(long chatId,
        CancellationToken cancellationToken)
    {
        var chatIdHash = Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(chatId.ToString())));
        if (_subscribeUsers.ContainsKey(chatIdHash))
            return JsonConvert.DeserializeObject<SubscribeUser>(_aes.Decrypt(_subscribeUsers[chatIdHash])) ??
                   new SubscribeUser();
        return new SubscribeUser();
    }

    public bool IsSubscribeUser(long chatId,
        CancellationToken cancellationToken)
    {
        var chatIdHash = Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(chatId.ToString())));
        return _subscribeUsers.ContainsKey(chatIdHash);
    }

    public Dictionary<string, byte[]> GetAllSubscribeUsers(
        CancellationToken cancellationToken)
    {
        return _subscribeUsers;
    }
    
    public SubscribeUser? DecryptSubscribeUser(byte[] value)
    {
        return JsonConvert.DeserializeObject<SubscribeUser>(_aes.Decrypt(value));
    }
    
}