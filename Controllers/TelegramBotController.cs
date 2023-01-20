using Microsoft.AspNetCore.Mvc;
using OhMyGPA.Bot.Logics;
using Telegram.Bot.Types;

namespace OhMyGPA.Bot.Controllers;

public class TelegramBotController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Post(
        [FromBody] Update update,
        [FromServices] TelegramMessageHandler handleUpdateService,
        CancellationToken cancellationToken)
    {
        await handleUpdateService.HandleUpdateAsync(update, cancellationToken);
        return Ok();
    }
}