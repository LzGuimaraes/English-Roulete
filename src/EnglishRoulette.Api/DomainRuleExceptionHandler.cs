using EnglishRoulette.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace EnglishRoulette.Api;

internal sealed class DomainRuleExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var admin = context.Request.Path.StartsWithSegments("/api/admin");
        if (admin && exception is DbUpdateConcurrencyException)
        {
            await Results.Problem(statusCode: 409, title: "O conteúdo mudou.", detail: "Recarregue antes de salvar novamente.").ExecuteAsync(context);
            return true;
        }
        if (admin && exception is DbUpdateException)
        {
            await Results.Problem(statusCode: 409, title: "Não foi possível salvar.", detail: "Revise chaves únicas e recarregue o conteúdo.").ExecuteAsync(context);
            return true;
        }
        if (exception is not GameRuleException rule) { return false; }
        if (admin)
        {
            await Results.Problem(statusCode: rule.Code == "NOT_FOUND" ? 404 : rule.Code is "INVALID_CONTENT" or "INVALID_AUDIO" ? 400 : 409,
                title: "Revise o conteúdo antes de continuar.", detail: rule.Message,
                extensions: new Dictionary<string, object?> { ["code"] = rule.Code }).ExecuteAsync(context);
            return true;
        }
        await Results.Problem(statusCode: rule.Code == "MEDIA_UNAVAILABLE" ? 503 : 409,
            title: rule.Code == "MEDIA_UNAVAILABLE" ? "Narração indisponível. Tente novamente." : "O estado da aventura mudou.",
            detail: rule.Code).ExecuteAsync(context);
        return true;
    }
}
