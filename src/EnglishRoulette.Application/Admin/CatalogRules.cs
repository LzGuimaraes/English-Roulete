using System.Text.RegularExpressions;
using EnglishRoulette.Domain;
using EnglishRoulette.Domain.Stories;

namespace EnglishRoulette.Application.Admin;

public static partial class CatalogRules
{
    public static IReadOnlyList<string> DraftIssues(CatalogDraft draft)
    {
        var issues = new List<string>();
        if (draft.Metadata.Appearance is { } appearance && !appearance.IsValid())
        { issues.Add("Escolha cores válidas com contraste de texto legível, fonte e movimento permitidos."); }
        if (string.IsNullOrWhiteSpace(draft.Metadata.Title) || draft.Metadata.Title.Length > 200)
        { issues.Add("Informe um título de até 200 caracteres."); }
        if (draft.Metadata.Genre.Length > 120 || draft.Metadata.Difficulty.Length > 10 || draft.Metadata.LicenseType.Length > 40 ||
            draft.InitialSceneKey.Length > 80 || draft.Metadata.Synopsis.Length > 2000 || draft.Metadata.Author.Length > 200 ||
            draft.Metadata.LicenseEvidence.Length > 2000 || draft.Metadata.Attribution.Length > 2000)
        { issues.Add("Os metadados excedem o tamanho permitido."); }
        if (draft.Scenes.Count > 40) { issues.Add("Uma versão pode ter até 40 cenas."); }
        if (draft.Scenes.Select(scene => scene.Key).Distinct(StringComparer.Ordinal).Count() != draft.Scenes.Count)
        { issues.Add("As chaves das cenas precisam ser únicas."); }
        var ids = draft.Scenes.Where(scene => scene.Id != Guid.Empty).Select(scene => scene.Id).ToArray();
        if (ids.Distinct().Count() != ids.Length) { issues.Add("As cenas precisam ter identificadores únicos."); }
        if (draft.Scenes.Any(scene => scene.Options.Any(option => !SceneKey().IsMatch(option.Key)) ||
                scene.Options.Select(option => option.Key).Distinct(StringComparer.Ordinal).Count() != scene.Options.Count ||
                scene.Transitions.Any(transition => !SceneKey().IsMatch(transition.Key)) ||
                scene.Transitions.Select(transition => transition.Key).Distinct(StringComparer.Ordinal).Count() != scene.Transitions.Count))
        { issues.Add("Use chaves únicas e válidas nas alternativas e escolhas de cada cena."); }
        if (draft.Scenes.Any(scene => !SceneKey().IsMatch(scene.Key) || scene.Title.Length > 200 ||
                scene.Narration.Length > 10000 || scene.Prompt.Length > 1000 || scene.Explanation.Length > 3000 ||
                scene.CorrectOptionKey.Length > 40 || scene.Options.Count > 8 || scene.Transitions.Count > 8 ||
                scene.Options.Any(option => option.Key.Length > 40 || option.Text.Length > 1000) ||
                scene.Transitions.Any(transition => transition.Key.Length > 80 || transition.Label.Length > 300 || transition.TargetSceneKey.Length > 80)))
        { issues.Add("Revise as chaves, quantidades e tamanhos dos campos das cenas."); }
        return issues;
    }

    public static IReadOnlyList<string> PublicationIssues(CatalogDraft draft)
    {
        var issues = DraftIssues(draft).ToList();
        var metadata = draft.Metadata;
        if (string.IsNullOrWhiteSpace(metadata.Synopsis) || string.IsNullOrWhiteSpace(metadata.Genre) ||
            metadata.Difficulty is not ("A1" or "A2" or "B1" or "B2" or "C1"))
        { issues.Add("Preencha sinopse, gênero e um nível válido."); }
        if (string.IsNullOrWhiteSpace(metadata.Author) || string.IsNullOrWhiteSpace(metadata.Attribution) ||
            string.IsNullOrWhiteSpace(metadata.LicenseEvidence) ||
            metadata.LicenseType is not ("Original" or "PublicDomain" or "Authorized"))
        { issues.Add("Preencha autoria, créditos, categoria e evidência de direitos."); }
        if (draft.Scenes.Count == 0) { issues.Add("Cadastre ao menos uma cena."); }
        foreach (var scene in draft.Scenes)
        {
            if (string.IsNullOrWhiteSpace(scene.Title) || string.IsNullOrWhiteSpace(scene.Narration) ||
                string.IsNullOrWhiteSpace(scene.Prompt) || string.IsNullOrWhiteSpace(scene.Explanation) ||
                scene.Options.Count < 2 || scene.Options.Any(option => string.IsNullOrWhiteSpace(option.Key) || string.IsNullOrWhiteSpace(option.Text)) ||
                scene.Options.Select(option => option.Key).Distinct(StringComparer.Ordinal).Count() != scene.Options.Count ||
                !scene.Options.Any(option => option.Key == scene.CorrectOptionKey))
            { issues.Add($"Cena {scene.Key}: complete texto, pergunta, alternativas, gabarito e explicação."); }
            if (scene.Audio is null) { issues.Add($"Cena {scene.Key}: envie uma narração."); }
        }
        if (issues.Count == 0)
        {
            try
            {
                StoryFlowEngine.ValidateForPlay(ToStory(draft));
                if (HasCycle(draft)) { issues.Add("A publicação V1 não permite ciclos no percurso."); }
            }
            catch (ArgumentException) { issues.Add("O conteúdo de uma cena ou transição está incompleto."); }
            catch (GameRuleException) { issues.Add("Revise a cena inicial, destinos, cenas órfãs e finais do percurso."); }
        }
        return issues;
    }

    public static StoryVersion ToStory(CatalogDraft draft) => new(draft.Id, draft.Metadata.Title,
        PublicationStatus.Published, draft.InitialSceneKey, draft.Scenes.Select(scene => new StoryScene(
            draft.Id, scene.Key, scene.Narration,
            new(scene.Id, scene.Prompt, scene.Options.Select(option => new EnglishRoulette.Domain.Challenges.AnswerOption(option.Key, option.Text)),
                scene.CorrectOptionKey, scene.Explanation), scene.IsEnding,
            scene.Transitions.Select(transition => new SceneTransition(transition.Key, transition.Label, transition.TargetSceneKey)))));

    private static bool HasCycle(CatalogDraft draft)
    {
        var scenes = draft.Scenes.ToDictionary(scene => scene.Key);
        var visiting = new HashSet<string>();
        var visited = new HashSet<string>();
        bool Visit(string key)
        {
            if (visiting.Contains(key)) { return true; }
            if (!visited.Add(key)) { return false; }
            visiting.Add(key);
            if (scenes[key].Transitions.Any(transition => Visit(transition.TargetSceneKey))) { return true; }
            visiting.Remove(key);
            return false;
        }
        return Visit(draft.InitialSceneKey);
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,79}$")]
    private static partial Regex SceneKey();
}
