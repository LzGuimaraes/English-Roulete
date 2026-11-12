namespace EnglishRoulette.Domain.Sessions;

public static class QuestionTiming
{
    public const int SoloDefaultSeconds = 20;
    public const int MultiplayerDefaultSeconds = 15;
    public const int MinimumSeconds = 10;
    public const int MaximumSeconds = 60;

    public static TimeSpan FromSeconds(int seconds)
    {
        if (seconds is < MinimumSeconds or > MaximumSeconds)
        { throw new GameRuleException("INVALID_QUESTION_TIME", $"O tempo para responder deve ficar entre {MinimumSeconds} e {MaximumSeconds} segundos."); }

        return TimeSpan.FromSeconds(seconds);
    }
}
