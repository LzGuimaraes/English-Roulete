using EnglishRoulette.Domain;
using Microsoft.AspNetCore.Diagnostics;

namespace EnglishRoulette.Api;

internal sealed class DomainRuleExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not GameRuleException rule) { return false; }
        await Results.Problem(statusCode: rule.Code == "MEDIA_UNAVAILABLE" ? 503 : 409,
            title: rule.Code == "MEDIA_UNAVAILABLE" ? "Narração indisponível. Tente novamente." : "O estado da aventura mudou.",
            detail: rule.Code).ExecuteAsync(context);
        return true;
    }
}
