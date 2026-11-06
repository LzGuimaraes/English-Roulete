namespace EnglishRoulette.Domain.Challenges;

public sealed record AnswerEvaluation(bool IsCorrect, string Explanation);

public static class ChallengeEvaluator
{
    // Call only after the coordinator validates identity, phase and deadline.
    public static AnswerEvaluation Evaluate(MultipleChoiceChallenge challenge, string optionKey)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        if (!challenge.Options.Any(option => option.Key == optionKey))
        {
            throw new GameRuleException("INVALID_ANSWER", "The option does not belong to this challenge.");
        }

        return new AnswerEvaluation(optionKey == challenge.CorrectOptionKey, challenge.Explanation);
    }
}
