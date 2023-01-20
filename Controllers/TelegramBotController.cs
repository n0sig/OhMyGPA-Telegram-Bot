using Microsoft.AspNetCore.Mvc;
using OhMyGPA.Bot.Logics;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace OhMyGPA.Bot.Controllers;

public class TelegramBotController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Post(
        [FromBody] Update update,
        [FromServices] MessageHandler messageHandler,
        [FromServices] ILogger<TelegramBotController> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var handler = update switch
            {
                { Message: { } message } => messageHandler.BotOnMessageReceived(message.Chat.Id, message.Text,
                    cancellationToken),
                _ => messageHandler.UnknownUpdateHandlerAsync(update.Type.ToString(), cancellationToken)
            };
            await handler;
            return Ok();
        }
        catch (Exception e)
        {
            var errorMessage = e switch
            {
                ApiRequestException apiRequestException =>
                    $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => e.ToString()
            };
            logger.LogError("{}", errorMessage);
            return StatusCode(500);
        }
    }
}