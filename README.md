# OhMyGPA-Telegram-Bot
基于ASP.NET 6的Telegram机器人服务端，可用于查询三本大学生精神状态，更支持在发生变化的15分钟内推送通知。

## 在自己的电脑上部署

### 依赖
* 运行时 _.NET 6 Runtime for server apps_：https://dotnet.microsoft.com/en-us/download/dotnet/6.0/runtime
* 数据库 _Reids_：https://github.com/MicrosoftArchive/redis/releases
* 内网穿透工具 _Ngrok_：https://ngrok.com/download

### 申请机器人
你需要在Telegram中申请一个自己的机器人，拿到token即可。主要内容就是和官方机器人 @BotFather 聊天，教程很多。

### 配置 Ngrok
Ngrok是一个一个内网穿透工具，它会为你分配一个2小时内有效的域名，通过这个域名就能访问你的电脑。

运行命令`/ngrok http 5000`，这将申请一个域名，并将对于这个域名的请求转发到本地的5000端口上——机器人将监听这个端口。如此一来，外网的请求便能被机器人收到了。

你将得到一个域名，如`https://b91b-240e-390-e40-f9b0-2415-2909-6bc1-854e.ap.ngrok.io`。

### 配置 appsettings.json
把`BotToken`的值改为你的Token，把`HostAddress`的值改为你得到的域名即可。如果对加密有要求，可以修改AES的Key和IV值。

### 运行程序
不出意外的话，程序已经可以运行了。如果运行出错，请确保Redis可以正常访问，并且其中没有名为"subscribes"的键。

### 其他
由于 Ngrok 所给的域名有效期只有2小时，而机器人接收消息需要这个域名——所以在2小时之内你是可以和机器人聊天的，2小时之后就只能机器人给你发消息了。
