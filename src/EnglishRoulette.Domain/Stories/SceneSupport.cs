using System.Text.RegularExpressions;

namespace EnglishRoulette.Domain.Stories;

// Editorial content held by the server. Vocabulary translations are paid assistance,
// so public game contracts must project only hints consumed by the current player.
public sealed record VocabularyHint(string Key, string Term, string Translation);
public sealed record DialogueCue(string Key, string Speaker, string Text, int StartMilliseconds, int EndMilliseconds);

public sealed partial record SceneSupport
{
    public IReadOnlyList<VocabularyHint> Vocabulary { get; init; } = [];
    public IReadOnlyList<DialogueCue> Dialogue { get; init; } = [];

    public SceneSupport Freeze(string narration)
    {
        Validate(narration);
        return this with
        {
            Vocabulary = Array.AsReadOnly(Vocabulary.ToArray()),
            Dialogue = Array.AsReadOnly(Dialogue.ToArray())
        };
    }

    public void Validate(string narration, int? audioDurationMilliseconds = null)
    {
        if (Vocabulary is null || Dialogue is null || Vocabulary.Count > 12 || Dialogue.Count > 40 ||
            Vocabulary.Any(hint => hint is null || !KeyPattern().IsMatch(hint.Key ?? "") ||
                string.IsNullOrWhiteSpace(hint.Term) || hint.Term.Length > 80 || hint.Term.Any(char.IsControl) ||
                string.IsNullOrWhiteSpace(hint.Translation) || hint.Translation.Length > 300 ||
                !ContainsTerm(narration, hint.Term)) ||
            Vocabulary.Select(hint => hint.Key).Distinct(StringComparer.Ordinal).Count() != Vocabulary.Count ||
            Vocabulary.Select(hint => Normalize(hint.Term)).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Vocabulary.Count)
        { throw Invalid("Use até 12 palavras/expressões distintas presentes na cena, com chave e tradução contextual."); }

        if (Dialogue.Any(cue => cue is null || !KeyPattern().IsMatch(cue.Key ?? "") ||
                string.IsNullOrWhiteSpace(cue.Speaker) || cue.Speaker.Length > 60 || cue.Speaker.Any(char.IsControl) ||
                string.IsNullOrWhiteSpace(cue.Text) || cue.Text.Length > 3000 || cue.StartMilliseconds < 0 ||
                cue.EndMilliseconds <= cue.StartMilliseconds ||
                audioDurationMilliseconds is { } duration && cue.EndMilliseconds > duration) ||
            Dialogue.Select(cue => cue.Key).Distinct(StringComparer.Ordinal).Count() != Dialogue.Count)
        { throw Invalid("Revise personagens, textos e intervalos das falas; todos devem caber na narração."); }
        for (var index = 1; index < Dialogue.Count; index++)
        {
            if (Dialogue[index].StartMilliseconds < Dialogue[index - 1].EndMilliseconds)
            { throw Invalid("As falas devem estar ordenadas e não podem se sobrepor."); }
        }
        if (Dialogue.Count > 0 && Normalize(string.Join(' ', Dialogue.Select(cue => cue.Text))) != Normalize(narration))
        { throw Invalid("As falas, em sequência, devem reproduzir todo o texto da narração, sem omissões ou acréscimos."); }
    }

    public static bool ContainsTerm(string narration, string term)
    {
        var text = Normalize(narration);
        term = Normalize(term);
        if (term.Length == 0) { return false; }
        var start = 0;
        while (start < text.Length)
        {
            var index = text.IndexOf(term, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0) { return false; }
            var end = index + term.Length;
            if ((index == 0 || !char.IsLetterOrDigit(text[index - 1])) &&
                (end == text.Length || !char.IsLetterOrDigit(text[end]))) { return true; }
            start = index + 1;
        }
        return false;
    }

    private static string Normalize(string value) => Whitespace().Replace(value.Trim(), " ");
    private static GameRuleException Invalid(string message) => new("INVALID_SCENE_SUPPORT", message);
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,79}$")] private static partial Regex KeyPattern();
    [GeneratedRegex("\\s+")] private static partial Regex Whitespace();
}
