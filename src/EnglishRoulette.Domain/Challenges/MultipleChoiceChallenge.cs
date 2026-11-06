namespace EnglishRoulette.Domain.Challenges;

public sealed record AnswerOption(string Key, string Text);

public sealed class MultipleChoiceChallenge
{
    public MultipleChoiceChallenge(
        Guid id,
        string prompt,
        IEnumerable<AnswerOption> options,
        string correctOptionKey,
        string explanation)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(correctOptionKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(explanation);

        if (id == Guid.Empty)
        {
            throw new ArgumentException("A challenge must have an ID.", nameof(id));
        }

        var snapshot = options.ToArray();
        if (snapshot.Length < 2 || snapshot.Any(option =>
                option is null || string.IsNullOrWhiteSpace(option.Key) || string.IsNullOrWhiteSpace(option.Text)))
        {
            throw new ArgumentException("Provide at least two options with keys and text.", nameof(options));
        }

        if (snapshot.Select(option => option.Key).Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
        {
            throw new ArgumentException("Option keys must be unique.", nameof(options));
        }

        if (!snapshot.Any(option => option.Key == correctOptionKey))
        {
            throw new ArgumentException("The correct option must belong to the challenge.", nameof(correctOptionKey));
        }

        Id = id;
        Prompt = prompt;
        Options = Array.AsReadOnly(snapshot);
        CorrectOptionKey = correctOptionKey;
        Explanation = explanation;
    }

    public Guid Id { get; }
    public string Prompt { get; }
    public IReadOnlyList<AnswerOption> Options { get; }
    internal string CorrectOptionKey { get; }
    internal string Explanation { get; }
}
