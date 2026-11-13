namespace EnglishRoulette.Domain.Stories;

public static class StoryDifficulty
{
    public static string? Require(string? difficulty)
    {
        if (difficulty is not null && difficulty is not ("A1" or "A2" or "B1" or "B2" or "C1"))
        { throw new GameRuleException("INVALID_DIFFICULTY", "Escolha um nível válido para a roleta."); }
        return difficulty;
    }
}
