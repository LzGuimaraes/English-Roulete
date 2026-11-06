using EnglishRoulette.Domain;
using EnglishRoulette.Domain.Challenges;

namespace EnglishRoulette.UnitTests;

public sealed class ChallengeEvaluatorTests
{
    [Theory]
    [InlineData("a", true)]
    [InlineData("b", false)]
    [InlineData("c", false)]
    public void Evaluate_ReturnsCorrectnessAndExplanation(string optionKey, bool expected)
    {
        var result = ChallengeEvaluator.Evaluate(StoryFixture.Challenge(), optionKey);

        Assert.Equal(expected, result.IsCorrect);
        Assert.Equal("Mia found a blue notebook beside the station clock.", result.Explanation);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("")]
    [InlineData("A")]
    [InlineData(" a ")]
    public void Evaluate_RejectsOptionsThatDoNotBelongToTheChallenge(string optionKey)
    {
        var exception = Assert.Throws<GameRuleException>(() =>
            ChallengeEvaluator.Evaluate(StoryFixture.Challenge(), optionKey));

        Assert.Equal("INVALID_ANSWER", exception.Code);
    }

    [Fact]
    public void Challenge_RejectsCorrectAnswerOutsideOptions()
    {
        Assert.Throws<ArgumentException>(() => new MultipleChoiceChallenge(
            Guid.NewGuid(), "Question?", [new("a", "First"), new("b", "Second")], "c", "Explanation."));
    }

    [Fact]
    public void Challenge_RejectsDuplicateOptionKeys()
    {
        Assert.Throws<ArgumentException>(() => new MultipleChoiceChallenge(
            Guid.NewGuid(), "Question?", [new("a", "First"), new("a", "Second")], "a", "Explanation."));
    }

    [Fact]
    public void Challenge_RequiresAtLeastTwoOptions()
    {
        Assert.Throws<ArgumentException>(() => new MultipleChoiceChallenge(
            Guid.NewGuid(), "Question?", [new("a", "Only one")], "a", "Explanation."));
    }

    [Fact]
    public void Challenge_CopiesOptionsToProtectPublishedContent()
    {
        AnswerOption[] options = [new("a", "First"), new("b", "Second")];
        var challenge = new MultipleChoiceChallenge(Guid.NewGuid(), "Question?", options, "a", "Explanation.");

        options[0] = new("replaced", "Changed after creation");

        Assert.Equal("a", challenge.Options[0].Key);
        Assert.True(ChallengeEvaluator.Evaluate(challenge, "a").IsCorrect);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<AnswerOption>)challenge.Options)[0] = new("replaced", "Changed through cast"));
    }

    [Fact]
    public void Challenge_DoesNotExposeAnswerKeyOrExplanationAsPublicProperties()
    {
        var properties = typeof(MultipleChoiceChallenge).GetProperties().Select(property => property.Name);

        Assert.DoesNotContain("CorrectOptionKey", properties);
        Assert.DoesNotContain("Explanation", properties);
    }
}
