using EnglishRoulette.Domain.Challenges;
using EnglishRoulette.Domain.Stories;

namespace EnglishRoulette.Api.Demo;

internal static class DemoStory
{
    private static readonly Guid VersionId = Guid.Parse("47289e5a-d5a2-4bf2-874f-f820fa0ef026");

    public static StoryVersion Create() => new(VersionId, "The Notebook at Platform Three",
        PublicationStatus.Published, "platform",
        [
            new(VersionId, "platform",
                "The last train had already left when Mia noticed a blue notebook beneath the station clock. " +
                "On its cover, someone had written: 'If you find this, please follow the map.' " +
                "Inside, a tiny drawing pointed to the old bookshop across the street.",
                new(Guid.Parse("93164bf4-2771-48c7-871e-b548b4db2bb9"), "Where did Mia find the notebook?",
                    [new("a", "Inside the last train"), new("b", "Beneath the station clock"), new("c", "In the old bookshop")],
                    "b", "Mia noticed the notebook beneath the station clock, after the last train had left."),
                false, [new("follow", "Seguir o mapa até a livraria", "bookshop")]),
            new(VersionId, "bookshop",
                "A warm light shone through the bookshop window. The owner recognised the notebook immediately. " +
                "'It belongs to my sister, Nora,' he said. 'She writes stories about this town.' " +
                "He turned a page. There were two possible places to find her: the riverside café or the quiet library.",
                new(Guid.Parse("e897c22f-49f7-42e5-ae73-e2d31176c675"), "What does Nora write about?",
                    [new("a", "Stories about the town"), new("b", "Train timetables"), new("c", "Recipes for the café")],
                    "a", "The owner says Nora writes stories about this town."),
                false, [new("cafe", "Procurar Nora no café", "cafe"), new("library", "Procurar Nora na biblioteca", "library")]),
            new(VersionId, "cafe",
                "Mia found Nora sitting by the café window, watching the river. When she saw the blue notebook, " +
                "her face lit up. 'My new story was in there! Thank you for bringing it back.' " +
                "Nora bought Mia a hot chocolate and asked if she could write about their unexpected meeting.",
                new(Guid.Parse("785eae45-d86f-4332-bb4c-a9ad5b04ae2d"), "Why was the notebook important to Nora?",
                    [new("a", "It contained a train ticket"), new("b", "It was a gift for Mia"), new("c", "Her new story was inside")],
                    "c", "Nora explains that her new story was in the notebook."), true, []),
            new(VersionId, "library",
                "At the library, Nora was reading to a small group of children. Mia waited until the story ended, " +
                "then handed her the notebook. 'You found my missing ending!' Nora laughed. " +
                "She invited Mia to stay. That evening, the children heard a brand-new story about a kind traveller and a blue notebook.",
                new(Guid.Parse("9019d5b4-f545-49ce-979d-a5fd1eedc6cd"), "What was Nora doing when Mia arrived?",
                    [new("a", "Reading to children"), new("b", "Looking for a train"), new("c", "Closing the café")],
                    "a", "Nora was reading to a small group of children when Mia arrived."), true, [])
        ]);

    public static (string Title, int Number) Describe(string key) => key switch
    {
        "platform" => ("O encontro na estação", 1),
        "bookshop" => ("A pista na livraria", 2),
        "cafe" => ("Um encontro à beira do rio", 3),
        "library" => ("Uma história para contar", 3),
        _ => throw new ArgumentOutOfRangeException(nameof(key))
    };
}
