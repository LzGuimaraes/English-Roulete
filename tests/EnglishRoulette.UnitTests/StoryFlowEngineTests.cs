using EnglishRoulette.Domain;
using EnglishRoulette.Domain.Stories;

namespace EnglishRoulette.UnitTests;

public sealed class StoryFlowEngineTests
{
    [Fact]
    public void GetInitialScene_StartsAtPublishedEntryPoint()
    {
        var scene = StoryFlowEngine.GetInitialScene(StoryFixture.Story());

        Assert.Equal("platform", scene.Key);
    }

    [Fact]
    public void GetInitialScene_RejectsDraft_Eng001()
    {
        var exception = Assert.Throws<GameRuleException>(() =>
            StoryFlowEngine.GetInitialScene(StoryFixture.Story(PublicationStatus.Draft)));

        Assert.Equal("CONTENT_NOT_PUBLISHED", exception.Code);
    }

    [Fact]
    public void GetNextScene_FollowsThreeSceneStoryToItsEnding()
    {
        var story = StoryFixture.Story();

        var first = StoryFlowEngine.GetInitialScene(story);
        var second = StoryFlowEngine.GetNextScene(story, first.Key);
        Assert.NotNull(second);
        Assert.Equal("notebook", second.Key);
        var final = StoryFlowEngine.GetNextScene(story, second.Key, "continue");
        Assert.NotNull(final);
        Assert.Equal("reunion", final.Key);
        Assert.True(final.IsEnding);
        Assert.Null(StoryFlowEngine.GetNextScene(story, final.Key));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("missing")]
    [InlineData("library")]
    public void GetNextScene_RequiresValidTransitionAtBranch_Eng003(string? choice)
    {
        var story = BranchingStory();

        var exception = Assert.Throws<GameRuleException>(() =>
            StoryFlowEngine.GetNextScene(story, "platform", choice));

        Assert.Equal("INVALID_CHOICE", exception.Code);
    }

    [Theory]
    [InlineData("left", "library")]
    [InlineData("right", "cafe")]
    public void GetNextScene_FollowsOnlyTheChosenBranch(string choice, string expectedSceneKey)
    {
        var next = StoryFlowEngine.GetNextScene(BranchingStory(), "platform", choice);

        Assert.NotNull(next);
        Assert.Equal(expectedSceneKey, next.Key);
    }

    [Fact]
    public void GetNextScene_RejectsUnknownCurrentScene()
    {
        var exception = Assert.Throws<GameRuleException>(() =>
            StoryFlowEngine.GetNextScene(StoryFixture.Story(), "other-version-scene"));

        Assert.Equal("INVALID_SCENE", exception.Code);
    }

    [Fact]
    public void GetNextScene_RejectsChoiceOnEnding()
    {
        var exception = Assert.Throws<GameRuleException>(() =>
            StoryFlowEngine.GetNextScene(StoryFixture.Story(), "reunion", "continue"));

        Assert.Equal("INVALID_CHOICE", exception.Code);
    }

    [Fact]
    public void ValidateForPlay_RejectsCrossVersionScene_Eng002()
    {
        var foreignScene = new StoryScene(Guid.NewGuid(), "reunion", "An ending.", StoryFixture.Challenge(), true, []);
        var story = StoryFixture.WithScenes(
            StoryFixture.Scene("platform", transitions: [new("go", "Continue", "reunion")]), foreignScene);

        AssertInvalidGraph(story);
    }

    [Fact]
    public void ValidateForPlay_RejectsMissingTarget()
    {
        AssertInvalidGraph(StoryFixture.WithScenes(
            StoryFixture.Scene("platform", transitions: [new("go", "Continue", "missing")])));
    }

    [Fact]
    public void ValidateForPlay_RejectsMissingInitialScene()
    {
        AssertInvalidGraph(StoryFixture.WithScenes(StoryFixture.Scene("reunion", isEnding: true)));
    }

    [Fact]
    public void ValidateForPlay_RejectsEmptyGraph()
    {
        AssertInvalidGraph(StoryFixture.WithScenes());
    }

    [Fact]
    public void ValidateForPlay_RejectsUnreachableScene()
    {
        AssertInvalidGraph(StoryFixture.WithScenes(
            StoryFixture.Scene("platform", isEnding: true), StoryFixture.Scene("unreachable", isEnding: true)));
    }

    [Fact]
    public void ValidateForPlay_RejectsCycleWithoutReachableEnding_Eng024()
    {
        AssertInvalidGraph(StoryFixture.WithScenes(
            StoryFixture.Scene("platform", transitions: [new("go", "Continue", "loop")]),
            StoryFixture.Scene("loop", transitions: [new("back", "Return", "platform")])));
    }

    [Fact]
    public void ValidateForPlay_RejectsTrappedBranchEvenWhenAnotherBranchEnds_Eng024()
    {
        AssertInvalidGraph(StoryFixture.WithScenes(
            StoryFixture.Scene("platform", transitions: [new("go", "Finish", "ending"), new("trap", "Loop", "loop")]),
            StoryFixture.Scene("ending", isEnding: true),
            StoryFixture.Scene("loop", transitions: [new("again", "Again", "loop")])));
    }

    [Fact]
    public void ValidateForPlay_RejectsEndingWithOutgoingTransition()
    {
        AssertInvalidGraph(StoryFixture.WithScenes(
            StoryFixture.Scene("platform", isEnding: true, transitions: [new("go", "Continue", "ending")]),
            StoryFixture.Scene("ending", isEnding: true)));
    }

    [Fact]
    public void ValidateForPlay_RejectsNonEndingWithoutTransition()
    {
        AssertInvalidGraph(StoryFixture.WithScenes(StoryFixture.Scene("platform")));
    }

    [Fact]
    public void Story_RejectsDuplicateSceneKeys()
    {
        Assert.Throws<ArgumentException>(() => StoryFixture.WithScenes(
            StoryFixture.Scene("platform", isEnding: true), StoryFixture.Scene("platform", isEnding: true)));
    }

    [Fact]
    public void Scene_RejectsDuplicateTransitionKeys()
    {
        Assert.Throws<ArgumentException>(() => StoryFixture.Scene("platform", transitions:
            [new("go", "Library", "library"), new("go", "Cafe", "cafe")]));
    }

    [Fact]
    public void StoryAndScene_CopyCollectionsToProtectPublishedContent_Eng020()
    {
        SceneTransition[] transitions = [new("go", "Continue", "ending")];
        var first = StoryFixture.Scene("platform", transitions: transitions);
        StoryScene[] scenes = [first, StoryFixture.Scene("ending", isEnding: true)];
        var story = StoryFixture.WithScenes(scenes);

        transitions[0] = new("changed", "Changed", "missing");
        scenes[0] = StoryFixture.Scene("replacement", isEnding: true);

        Assert.Equal("platform", StoryFlowEngine.GetInitialScene(story).Key);
        Assert.Equal("ending", StoryFlowEngine.GetNextScene(story, "platform")?.Key);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<StoryScene>)story.Scenes)[0] = scenes[0]);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<SceneTransition>)first.Transitions)[0] = transitions[0]);
    }

    private static StoryVersion BranchingStory() => StoryFixture.WithScenes(
        StoryFixture.Scene("platform", transitions:
            [new("left", "Visit the library", "library"), new("right", "Visit the cafe", "cafe")]),
        StoryFixture.Scene("library", isEnding: true),
        StoryFixture.Scene("cafe", isEnding: true));

    private static void AssertInvalidGraph(StoryVersion story)
    {
        var exception = Assert.Throws<GameRuleException>(() => StoryFlowEngine.ValidateForPlay(story));

        Assert.Equal("INVALID_STORY_GRAPH", exception.Code);
    }
}
