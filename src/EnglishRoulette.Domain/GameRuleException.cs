namespace EnglishRoulette.Domain;

public sealed class GameRuleException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
