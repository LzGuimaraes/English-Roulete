namespace EnglishRoulette.Domain.Scoring;

// Frozen into each new game. Old checkpoints without a policy retain their original rules.
public sealed record AssistanceRules(int PercentPerWord, int MaximumPercent, int PointsPerWord = 0)
{
    public void Validate()
    {
        if (PercentPerWord is < 0 or > 100 || MaximumPercent is < 0 or > 100 ||
            PointsPerWord is < 0 or > 1000 || (PercentPerWord > 0) == (PointsPerWord > 0) ||
            PercentPerWord > MaximumPercent)
        { throw new ArgumentException("Choose a positive percentage (with a cap) or a fixed hint cost."); }
    }

    public int Percentage(int words) => Math.Min(MaximumPercent, checked(PercentPerWord * Math.Max(0, words)));
    // Integer arithmetic: discounts round up, so a fraction never silently makes a hint free.
    public int Discount(int basePoints, int words) => Math.Min(Math.Max(0, basePoints), PointsPerWord > 0
        ? checked(PointsPerWord * Math.Max(0, words))
        : (checked(Math.Max(0, basePoints) * Percentage(words)) + 99) / 100);
}

public sealed record HintUse(Guid PlayerId, Guid SceneInstanceId, string Key, DateTimeOffset UsedAtUtc);
public sealed record SoloRoundScore(Guid SceneInstanceId, string SceneKey, bool Correct, bool TimedOut,
    int BasePoints, int HintCount, int Deduction, int Points);
public sealed record SoloScoringRules(int FastPoints, int MediumPoints, int SlowPoints, AssistanceRules Hints)
{
    public void Validate()
    {
        Hints.Validate();
        if (FastPoints is < 1 or > 10000 || MediumPoints is < 1 or > 10000 || SlowPoints is < 1 or > 10000 ||
            FastPoints < MediumPoints || MediumPoints < SlowPoints)
        { throw new ArgumentException("Solo scores must be positive, ordered and bounded."); }
    }

    public int Award(bool correct, DateTimeOffset opens, DateTimeOffset closes, DateTimeOffset received)
    {
        if (!correct || received < opens || received >= closes) { return 0; }
        var remaining = (closes - received).Ticks;
        var duration = (closes - opens).Ticks;
        return remaining * 10 >= duration * 7 ? FastPoints : remaining * 10 >= duration * 4 ? MediumPoints : SlowPoints;
    }
}
