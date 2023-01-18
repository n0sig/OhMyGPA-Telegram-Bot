using Microsoft.AspNetCore.Mvc;
using OhMyGPA.Telegram.Bot.Logics;
using Telegram.Bot.Types;

namespace OhMyGPA.Telegram.Bot.Controllers;

public class BotController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Post(
        [FromBody] Update update,
        [FromServices] UpdateHandlers handleUpdateService,
        CancellationToken cancellationToken)
    {
        await handleUpdateService.HandleUpdateAsync(update, cancellationToken);
        return Ok();
    }
}