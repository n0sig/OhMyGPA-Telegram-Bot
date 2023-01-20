using Microsoft.Extensions.Options;
using OhMyGPA.Telegram.Bot.Controllers;
using OhMyGPA.Telegram.Bot.Logics;
using OhMyGPA.Telegram.Bot.Models;
using StackExchange.Redis;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

// Telegram Bot
var botConfigurationSection = builder.Configuration.GetSection(BotConfiguration.Configuration);
builder.Services.Configure<BotConfiguration>(botConfigurationSection);
var botConfiguration = botConfigurationSection.Get<BotConfiguration>();
builder.Services.AddHttpClient("telegram_bot_client")
    .AddTypedClient<ITelegramBotClient>((httpClient, sp) =>
    {
        var botConfig = sp.GetConfiguration<BotConfiguration>();
        TelegramBotClientOptions options = new(botConfig.BotToken);
        return new TelegramBotClient(options, httpClient);
    });
builder.Services.AddControllers().AddNewtonsoftJson();

// Redis
var redisConfigurationSection = builder.Configuration.GetSection(RedisConfiguration.Configuration);
builder.Services.Configure<RedisConfiguration>(redisConfigurationSection);
builder.Services.AddSingleton<IDatabaseAsync>(sp =>
{
    var redisConfig = sp.GetConfiguration<RedisConfiguration>();
    var configurationOptions = new ConfigurationOptions();
    configurationOptions.EndPoints.Add(redisConfig.Host, redisConfig.Port);
    configurationOptions.Password = redisConfig.Password;
    configurationOptions.DefaultDatabase = redisConfig.DefaultDatabase;
    var redis = ConnectionMultiplexer.Connect(configurationOptions);
    return redis.GetDatabase();
});

// AES Encryption
var cryptoConfigurationSection = builder.Configuration.GetSection(EncryptionConfiguration.Configuration);
builder.Services.Configure<EncryptionConfiguration>(cryptoConfigurationSection);
builder.Services.AddSingleton<AesEncryption>(sp =>
{
    var cryptoConfig = sp.GetConfiguration<EncryptionConfiguration>();
    return new AesEncryption(cryptoConfig.Key, cryptoConfig.IV);
});

// User Management
builder.Services.AddSingleton<BotUser>();

// Logics
builder.Services.AddScoped<UpdateHandlers>();
builder.Services.AddHostedService<ConfigureWebhook>();
builder.Services.AddHostedService<PeriodicalCheck>();

// Build and run!
var app = builder.Build();
app.MapBotWebhookRoute<BotController>(botConfiguration.Route);
app.MapControllers();
app.Run();


// I want to do so
public class BotConfiguration
{
    public static readonly string Configuration = "Bot";
    public string BotToken { get; init; } = default!;
    public string HostAddress { get; init; } = default!;
    public string Route { get; init; } = default!;
}

public class RedisConfiguration
{
    public static readonly string Configuration = "Redis";
    public string Host { get; init; } = default!;
    public int Port { get; init; }
    public string? Password { get; init; }
    public int DefaultDatabase { get; init; }
}

public class EncryptionConfiguration
{
    public static readonly string Configuration = "Encryption";
    public string Key { get; init; } = default!;
    public string IV { get; init; } = default!;
}

public static class Extensions
{
    public static T GetConfiguration<T>(this IServiceProvider serviceProvider)
        where T : class
    {
        var o = serviceProvider.GetService<IOptions<T>>();
        if (o is null)
            throw new ArgumentNullException(nameof(T));

        return o.Value;
    }
    
    public static ControllerActionEndpointConventionBuilder MapBotWebhookRoute<T>(
        this IEndpointRouteBuilder endpoints,
        string route)
    {
        var controllerName = typeof(T).Name.Replace("Controller", "");
        var actionName = typeof(T).GetMethods()[0].Name;

        return endpoints.MapControllerRoute(
            name: "bot_webhook",
            pattern: route,
            defaults: new { controller = controllerName, action = actionName });
    }
}