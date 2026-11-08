using EnglishRoulette.Contracts;
using EnglishRoulette.Domain.Scoring;
using EnglishRoulette.Domain.Stories;

namespace EnglishRoulette.Api;

// Economic defaults are configurable and frozen by the server for each new game.
// Product decision remains documented in VOCABULARY_DIALOGUE_SCORING.md.
internal static class GameAssistance
{
    public static SoloScoringRules Rules(IConfiguration configuration)
    {
        var hints = new AssistanceRules(configuration.GetValue("Gameplay:Hints:PercentPerWord", 10),
            configuration.GetValue("Gameplay:Hints:MaximumPercent", 50), configuration.GetValue("Gameplay:Hints:PointsPerWord", 0));
        var rules = new SoloScoringRules(configuration.GetValue("Gameplay:Solo:FastPoints", 150),
            configuration.GetValue("Gameplay:Solo:MediumPoints", 125), configuration.GetValue("Gameplay:Solo:SlowPoints", 100), hints);
        rules.Validate();
        return rules;
    }

    public static IReadOnlyList<WordHintView> Words(SceneSupport? support, IReadOnlyList<string> consumed, AssistanceRules? rules) =>
        rules is null ? [] : (support?.Vocabulary ?? []).Select(hint =>
            new WordHintView(hint.Key, hint.Term, consumed.Contains(hint.Key, StringComparer.Ordinal) ? hint.Translation : null)).ToArray();

    public static HintCostView? Cost(AssistanceRules? rules, int used, bool canUse) => rules is null ? null :
        new(rules.PointsPerWord > 0 ? $"−{rules.PointsPerWord} pontos por palavra" : rules.Percentage(used) >= rules.MaximumPercent ? "Sem desconto adicional" : $"−{rules.PercentPerWord}% por palavra",
            rules.PointsPerWord > 0 ? "Desconto limitado aos pontos desta rodada." : $"Desconto máximo: {rules.MaximumPercent}% nesta rodada.",
            used, rules.PointsPerWord > 0 ? $"−{rules.PointsPerWord * used} pontos, limitado ao ganho da rodada" : $"−{rules.Percentage(used)}% nesta rodada", canUse);

    public static string SoloDescription(SoloScoringRules rules) => rules.FastPoints == rules.SlowPoints
        ? $"{rules.FastPoints} pontos por acerto. Erro ou prazo encerrado: 0."
        : $"Acertos com pelo menos 70% do tempo restante: {rules.FastPoints}; de 40% a menos de 70%: {rules.MediumPoints}; abaixo de 40%: {rules.SlowPoints}. Erro ou prazo encerrado: 0.";
}
