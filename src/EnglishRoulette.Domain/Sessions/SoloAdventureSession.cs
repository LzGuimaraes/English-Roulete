using EnglishRoulette.Domain.Challenges;
using EnglishRoulette.Domain.Scoring;
using EnglishRoulette.Domain.Stories;

namespace EnglishRoulette.Domain.Sessions;

public sealed class SoloAdventureSession
{
    private readonly object _sync = new();
    private readonly Guid _id;
    private readonly Guid _playerId;
    private readonly StoryVersion _story;
    private readonly Dictionary<string, SceneTiming> _timings;
    private readonly Dictionary<Guid, SoloCommandReceipt> _receipts = [];
    private StoryScene _scene;
    private SoloAdventurePhase _phase = SoloAdventurePhase.Preloading;
    private long _stateVersion = 1;
    private Guid _questionInstanceId = Guid.NewGuid();
    private DateTimeOffset _questionOpensAtUtc;
    private DateTimeOffset _questionClosesAtUtc;
    private SoloAnswerResult? _answer;
    private SoloScoringRules? _scoringRules;
    private readonly List<HintUse> _hints = [];
    private readonly List<SoloRoundScore> _roundScores = [];

    public SoloAdventureSession(Guid id, Guid playerId, StoryVersion story, IEnumerable<SceneTiming> timings, SoloScoringRules? scoringRules = null)
    {
        if (id == Guid.Empty || playerId == Guid.Empty)
        {
            throw new ArgumentException("Session and player IDs must not be empty.");
        }

        ArgumentNullException.ThrowIfNull(timings);
        _scene = StoryFlowEngine.GetInitialScene(story);
        var snapshot = timings.ToArray();
        if (snapshot.Length != story.Scenes.Count || snapshot.Any(timing => timing is null ||
                string.IsNullOrWhiteSpace(timing.SceneKey) ||
                timing.NarrationDuration <= TimeSpan.Zero || timing.QuestionDuration <= TimeSpan.Zero) ||
            snapshot.Select(timing => timing.SceneKey).Distinct(StringComparer.Ordinal).Count() != snapshot.Length ||
            snapshot.Any(timing => !story.Scenes.Any(scene => scene.Key == timing.SceneKey)))
        {
            throw new ArgumentException("Provide one positive narration/question timing for each story scene.", nameof(timings));
        }
        foreach (var timing in snapshot)
        {
            var seconds = (int)timing.QuestionDuration.TotalSeconds;
            if (timing.QuestionDuration != TimeSpan.FromSeconds(seconds))
            { throw new GameRuleException("INVALID_QUESTION_TIME", "O tempo da pergunta deve ser informado em segundos inteiros."); }
            QuestionTiming.FromSeconds(seconds);
        }

        scoringRules?.Validate();
        _scoringRules = scoringRules;
        _id = id;
        _playerId = playerId;
        _story = story;
        _timings = snapshot.ToDictionary(timing => timing.SceneKey, StringComparer.Ordinal);
    }

    public SoloAdventureSnapshot GetSnapshot(Guid actorPlayerId, DateTimeOffset serverNowUtc)
    {
        lock (_sync)
        {
            EnsureParticipant(actorPlayerId);
            var now = serverNowUtc.ToUniversalTime();
            AdvanceDeadlines(now);
            return Snapshot(now);
        }
    }

    public SoloAdventureSnapshot Execute(Guid actorPlayerId, SoloAdventureCommand command, DateTimeOffset serverNowUtc)
    {
        lock (_sync)
        {
            EnsureParticipant(actorPlayerId);
            ArgumentNullException.ThrowIfNull(command);
            if (command.RequestId == Guid.Empty)
            {
                throw new ArgumentException("A command must have a request ID.", nameof(command));
            }

            // Replays precede phase/version checks: the aggregate may already be on another scene.
            if (_receipts.TryGetValue(command.RequestId, out var receipt))
            {
                if (receipt.Command != command)
                {
                    throw new GameRuleException("IDEMPOTENCY_CONFLICT", "The request ID was used for a different command.");
                }

                return receipt.Snapshot;
            }

            var now = serverNowUtc.ToUniversalTime();
            AdvanceDeadlines(now);
            if (command is SubmitSoloAnswer answer)
            {
                ValidateAnswerAttempt(answer, now);
            }

            if (command.ExpectedStateVersion != _stateVersion)
            {
                throw new GameRuleException("STATE_CONFLICT", "Fetch the latest state before sending a new command.");
            }

            switch (command)
            {
                case StartSoloNarration:
                    StartNarration(now);
                    break;
                case SubmitSoloAnswer submitted:
                    var evaluation = ChallengeEvaluator.Evaluate(_scene.Challenge, submitted.OptionKey);
                    _answer = new(_questionInstanceId, submitted.OptionKey, evaluation.IsCorrect, false, now, evaluation.Explanation);
                    RecordScore();
                    SetPhase(SoloAdventurePhase.QuestionReveal);
                    break;
                case UseSoloHint hint:
                    UseHint(hint, now);
                    break;
                case ContinueSoloAdventure:
                    Continue();
                    break;
                case ChooseSoloPath choice:
                    EnsurePhase(SoloAdventurePhase.DecisionOpen);
                    MoveToNextScene(choice.TransitionKey);
                    break;
                default:
                    throw new ArgumentException("Unsupported solo command.", nameof(command));
            }

            var result = Snapshot(now);
            _receipts.Add(command.RequestId, new(command, result));
            return result;
        }
    }

    private void StartNarration(DateTimeOffset now)
    {
        EnsurePhase(SoloAdventurePhase.Preloading);
        var timing = _timings[_scene.Key];
        var opensAt = now + timing.NarrationDuration;
        var closesAt = opensAt + timing.QuestionDuration;
        _questionOpensAtUtc = opensAt;
        _questionClosesAtUtc = closesAt;
        SetPhase(SoloAdventurePhase.Narrating);
    }

    private void AdvanceDeadlines(DateTimeOffset now)
    {
        if (_phase == SoloAdventurePhase.Narrating && now >= _questionOpensAtUtc)
        {
            SetPhase(SoloAdventurePhase.QuestionOpen);
        }

        if (_phase == SoloAdventurePhase.QuestionOpen && now >= _questionClosesAtUtc)
        {
            _answer = new(_questionInstanceId, null, false, true, _questionClosesAtUtc, _scene.Challenge.Explanation);
            RecordScore();
            SetPhase(SoloAdventurePhase.QuestionReveal);
        }
    }

    private void ValidateAnswerAttempt(SubmitSoloAnswer command, DateTimeOffset now)
    {
        if (_phase is not (SoloAdventurePhase.QuestionOpen or SoloAdventurePhase.QuestionReveal))
        {
            throw new GameRuleException("INVALID_GAME_PHASE", "Answers are accepted only during the current question.");
        }

        if (command.QuestionInstanceId != _questionInstanceId)
        {
            throw new GameRuleException("INVALID_QUESTION", "The question instance does not belong to the current scene visit.");
        }

        if (_answer is not null)
        {
            throw _answer.TimedOut
                ? new GameRuleException("DEADLINE_EXPIRED", "The question deadline has elapsed.")
                : new GameRuleException("ALREADY_ANSWERED", "The first accepted answer consumes the attempt.");
        }

        if (now < _questionOpensAtUtc)
        {
            throw new GameRuleException("INVALID_GAME_PHASE", "The question has not opened yet.");
        }
    }

    private void UseHint(UseSoloHint command, DateTimeOffset now)
    {
        if (_scoringRules is null || _phase is not (SoloAdventurePhase.Narrating or SoloAdventurePhase.QuestionOpen) || _answer is not null)
        { throw new GameRuleException("HINT_UNAVAILABLE", "As dicas ficam disponíveis durante a cena e antes de responder, dentro do prazo."); }
        if (command.SceneInstanceId != _questionInstanceId)
        { throw new GameRuleException("INVALID_SCENE", "Esta dica pertence a outra cena. Atualize a aventura."); }
        if (_scene.Support?.Vocabulary.Any(hint => hint.Key == command.HintKey) is not true)
        { throw new GameRuleException("INVALID_HINT", "Escolha uma palavra disponível nesta cena."); }
        if (!_hints.Any(hint => hint.SceneInstanceId == _questionInstanceId && hint.Key == command.HintKey))
        { _hints.Add(new(_playerId, _questionInstanceId, command.HintKey, now)); }
        // Persist the receipt even when a second request opens the same paid word.
        _stateVersion++;
    }

    private void RecordScore()
    {
        if (_scoringRules is null || _answer is null || _roundScores.Any(round => round.SceneInstanceId == _questionInstanceId)) { return; }
        var basePoints = _scoringRules.Award(_answer.IsCorrect, _questionOpensAtUtc, _questionClosesAtUtc, _answer.ResolvedAtUtc);
        var count = _hints.Count(hint => hint.SceneInstanceId == _questionInstanceId);
        var deduction = _scoringRules.Hints.Discount(basePoints, count);
        _roundScores.Add(new(_questionInstanceId, _scene.Key, _answer.IsCorrect, _answer.TimedOut, basePoints, count, deduction, basePoints - deduction));
    }

    private void Continue()
    {
        EnsurePhase(SoloAdventurePhase.QuestionReveal);
        if (_scene.IsEnding)
        {
            SetPhase(SoloAdventurePhase.StoryResult);
        }
        else if (_scene.Transitions.Count > 1)
        {
            SetPhase(SoloAdventurePhase.DecisionOpen);
        }
        else
        {
            MoveToNextScene();
        }
    }

    private void MoveToNextScene(string? transitionKey = null)
    {
        var next = StoryFlowEngine.GetNextScene(_story, _scene.Key, transitionKey)
            ?? throw new GameRuleException("INVALID_STORY_GRAPH", "A non-final scene must lead to another scene.");
        _scene = next;
        _questionInstanceId = Guid.NewGuid();
        _answer = null;
        _questionOpensAtUtc = default;
        _questionClosesAtUtc = default;
        SetPhase(SoloAdventurePhase.Preloading);
    }

    private void EnsureParticipant(Guid actorPlayerId)
    {
        if (actorPlayerId != _playerId)
        {
            throw new GameRuleException("NOT_PARTICIPANT", "The player does not own this solo session.");
        }
    }

    private void EnsurePhase(SoloAdventurePhase expected)
    {
        if (_phase != expected)
        {
            throw new GameRuleException("INVALID_GAME_PHASE", "The command is not allowed in this phase.");
        }
    }

    private void SetPhase(SoloAdventurePhase phase)
    {
        _phase = phase;
        _stateVersion++;
    }

    private SoloAdventureSnapshot Snapshot(DateTimeOffset now)
    {
        var question = _phase is SoloAdventurePhase.QuestionOpen or SoloAdventurePhase.QuestionReveal
            ? new SoloQuestion(_questionInstanceId, _scene.Challenge.Prompt, _scene.Challenge.Options,
                _questionOpensAtUtc, _questionClosesAtUtc)
            : null;
        DateTimeOffset? deadline = _phase switch
        {
            SoloAdventurePhase.Narrating => _questionOpensAtUtc,
            SoloAdventurePhase.QuestionOpen => _questionClosesAtUtc,
            _ => null
        };

        return new(_id, _playerId, _story.Id, _story.Title, _scene.Key, _scene.Narration,
            _stateVersion, _phase, now, deadline, question, _answer,
            _phase == SoloAdventurePhase.DecisionOpen ? _scene.Transitions : Array.Empty<SceneTransition>(),
            _questionInstanceId, _scoringRules, Array.AsReadOnly(_hints.Where(hint => hint.SceneInstanceId == _questionInstanceId).Select(hint => hint.Key).ToArray()),
            Array.AsReadOnly(_roundScores.ToArray()));
    }

    public SoloAdventureCheckpoint Capture()
    {
        lock (_sync)
        {
            return new(_id, _playerId, StoryDefinition.Capture(_story), Array.AsReadOnly(_timings.Values.ToArray()),
                _scene.Key, _phase, _stateVersion, _questionInstanceId, _questionOpensAtUtc,
                _questionClosesAtUtc, _answer, Array.AsReadOnly(_receipts.Values.ToArray()),
                _scoringRules, Array.AsReadOnly(_hints.ToArray()), Array.AsReadOnly(_roundScores.ToArray()));
        }
    }

    public static SoloAdventureSession Restore(SoloAdventureCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        var story = checkpoint.Story.Restore();
        var session = new SoloAdventureSession(checkpoint.Id, checkpoint.PlayerId, story, checkpoint.Timings, checkpoint.ScoringRules);
        var scene = story.Scenes.SingleOrDefault(candidate => candidate.Key == checkpoint.SceneKey);
        if (scene is null || !Enum.IsDefined(checkpoint.Phase) || checkpoint.StateVersion < 1 ||
            checkpoint.QuestionInstanceId == Guid.Empty ||
            (checkpoint.Phase is SoloAdventurePhase.Narrating or SoloAdventurePhase.QuestionOpen &&
                (checkpoint.QuestionClosesAtUtc <= checkpoint.QuestionOpensAtUtc || checkpoint.Answer is not null)) ||
            (checkpoint.Phase == SoloAdventurePhase.QuestionReveal && checkpoint.Answer is null) ||
            (checkpoint.Answer is { } answer && answer.QuestionInstanceId != checkpoint.QuestionInstanceId) ||
            (checkpoint.Phase == SoloAdventurePhase.StoryResult && !scene.IsEnding) ||
            (checkpoint.Phase == SoloAdventurePhase.DecisionOpen && scene.Transitions.Count < 2))
        {
            throw new InvalidOperationException("The solo checkpoint is inconsistent.");
        }

        session._scene = scene;
        session._phase = checkpoint.Phase;
        session._stateVersion = checkpoint.StateVersion;
        session._questionInstanceId = checkpoint.QuestionInstanceId;
        session._questionOpensAtUtc = checkpoint.QuestionOpensAtUtc;
        session._questionClosesAtUtc = checkpoint.QuestionClosesAtUtc;
        session._answer = checkpoint.Answer;
        session._hints.AddRange(checkpoint.Hints ?? []);
        session._roundScores.AddRange(checkpoint.RoundScores ?? []);
        foreach (var receipt in checkpoint.Receipts)
        {
            if (receipt.Command.RequestId == Guid.Empty || receipt.Snapshot.SessionId != checkpoint.Id ||
                receipt.Snapshot.PlayerId != checkpoint.PlayerId || receipt.Snapshot.StoryVersionId != story.Id ||
                !session._receipts.TryAdd(receipt.Command.RequestId, receipt with
                {
                    Snapshot = receipt.Snapshot with
                    {
                        Question = receipt.Snapshot.Question is { } question
                            ? question with { Options = Array.AsReadOnly(question.Options.ToArray()) } : null,
                        Choices = Array.AsReadOnly(receipt.Snapshot.Choices.ToArray()),
                        UsedHintKeys = Array.AsReadOnly((receipt.Snapshot.UsedHintKeys ?? []).ToArray()),
                        RoundScores = Array.AsReadOnly((receipt.Snapshot.RoundScores ?? []).ToArray())
                    }
                }))
            {
                throw new InvalidOperationException("The solo checkpoint contains an invalid command receipt.");
            }
        }

        return session;
    }
}
