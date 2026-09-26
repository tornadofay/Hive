using Hive.Core;

namespace Hive.Agents;

public enum QuestionStatus
{
    Waiting,
    Answered,
    TimedOut,
    Cancelled
}

public sealed class Question
{
    private Question(
        ResourceEnvelope<QuestionId> resource,
        AgentId askedByAgentId,
        RuntimeId askedByRuntimeId,
        string prompt,
        DateTimeOffset expiresAtUtc,
        QuestionStatus status,
        string? answer,
        AgentId? answeredByAgentId,
        RuntimeId? answeredByRuntimeId)
    {
        Resource = resource;
        AskedByAgentId = askedByAgentId;
        AskedByRuntimeId = askedByRuntimeId;
        Prompt = prompt;
        ExpiresAtUtc = expiresAtUtc;
        Status = status;
        Answer = answer;
        AnsweredByAgentId = answeredByAgentId;
        AnsweredByRuntimeId = answeredByRuntimeId;
    }

    public ResourceEnvelope<QuestionId> Resource { get; }

    public QuestionId Id => Resource.Identity;

    public AgentId AskedByAgentId { get; }

    public RuntimeId AskedByRuntimeId { get; }

    public string Prompt { get; }

    public DateTimeOffset ExpiresAtUtc { get; }

    public QuestionStatus Status { get; }

    public string? Answer { get; }

    public AgentId? AnsweredByAgentId { get; }

    public RuntimeId? AnsweredByRuntimeId { get; }

    internal static Question Create(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        string prompt,
        TimeSpan timeout,
        DateTimeOffset askedAtUtc,
        CorrelationId correlationId)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                timeout,
                "Question timeout must be greater than zero.");

        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException(
                "Question prompt is required.",
                nameof(prompt));

        var expiresAtUtc = askedAtUtc.ToUniversalTime().Add(timeout);

        var provenance = new ResourceProvenance(
            accessContext.PrincipalId!.Value,
            askedAtUtc,
            correlationId);

        var resource = new ResourceEnvelope<QuestionId>(
            ResourceKind.Question,
            QuestionId.New(),
            accessContext.PrincipalId.Value,
            ResourceScope.Runtime(runtimeId),
            ResourceVersion.Initial,
            provenance,
            ResourceLifecycle.Active(askedAtUtc));

        return new Question(
            resource,
            agentId,
            runtimeId,
            prompt.Trim(),
            expiresAtUtc,
            QuestionStatus.Waiting,
            null,
            null,
            null);
    }

    internal Result<Question> ApplyAnswer(
        AgentId responderAgentId,
        RuntimeId responderRuntimeId,
        string answer,
        DateTimeOffset answeredAtUtc)
    {
        if (Status != QuestionStatus.Waiting)
            return InvalidTransition("answer");

        if (string.IsNullOrWhiteSpace(answer))
        {
            return Result<Question>.Failure(
                Error.Validation(
                    "hive.agent.question.answer-required",
                    "Question answer is required."));
        }

        if (responderAgentId == default || responderRuntimeId == default)
        {
            return Result<Question>.Failure(
                Error.Validation(
                    "hive.agent.question.responder-required",
                    "Question responder Agent and Runtime identities are required."));
        }

        return Result<Question>.Success(
            WithStatus(
                QuestionStatus.Answered,
                answeredAtUtc,
                answer.Trim(),
                responderAgentId,
                responderRuntimeId));
    }

    internal Result<Question> Timeout(DateTimeOffset timedOutAtUtc)
    {
        if (Status != QuestionStatus.Waiting)
            return InvalidTransition("timeout");

        if (timedOutAtUtc.ToUniversalTime() < ExpiresAtUtc)
        {
            return Result<Question>.Failure(
                Error.Validation(
                    "hive.agent.question.timeout-too-early",
                    "A Question cannot time out before its deadline."));
        }

        return Result<Question>.Success(
            WithStatus(
                QuestionStatus.TimedOut,
                timedOutAtUtc,
                null,
                null,
                null));
    }

    internal Result<Question> Cancel(DateTimeOffset cancelledAtUtc)
    {
        if (Status != QuestionStatus.Waiting)
            return InvalidTransition("cancel");

        return Result<Question>.Success(
            WithStatus(
                QuestionStatus.Cancelled,
                cancelledAtUtc,
                null,
                null,
                null));
    }

    private Result<Question> InvalidTransition(string operation) =>
        Result<Question>.Failure(
            Error.Validation(
                "hive.agent.question.invalid-transition",
                $"Cannot {operation} a question in status '{Status}'."));

    private Question WithStatus(
        QuestionStatus status,
        DateTimeOffset changedAtUtc,
        string? answer,
        AgentId? answeredByAgentId,
        RuntimeId? answeredByRuntimeId) =>
        new(
            Resource.TransitionLifecycle(
                ResourceLifecycleStatus.Active,
                changedAtUtc),
            AskedByAgentId,
            AskedByRuntimeId,
            Prompt,
            ExpiresAtUtc,
            status,
            answer,
            answeredByAgentId,
            answeredByRuntimeId);
}

public interface IQuestionTransport
{
    Result<Question> Ask(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        string prompt,
        TimeSpan timeout,
        DateTimeOffset askedAtUtc);

    Task<Result<Question>> WaitAsync(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        QuestionId questionId,
        CancellationToken cancellationToken = default);

    Result<Question> Answer(
        ResourceAccessContext responderContext,
        QuestionId questionId,
        AgentId responderAgentId,
        RuntimeId responderRuntimeId,
        string answer,
        DateTimeOffset answeredAtUtc);

    Result<Question> Cancel(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        QuestionId questionId,
        DateTimeOffset cancelledAtUtc);

    int ExpireDue();
}

public sealed class QuestionTransport : IQuestionTransport
{
    private readonly object _sync = new();
    private readonly Dictionary<QuestionId, Question> _questions = new();
    private readonly Dictionary<QuestionId, TaskCompletionSource<Result<Question>>> _waiters = new();
    private readonly IClock _clock;

    public QuestionTransport(IClock? clock = null)
    {
        _clock = clock ?? SystemClock.Instance;
    }

    public Result<Question> Ask(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        string prompt,
        TimeSpan timeout,
        DateTimeOffset askedAtUtc)
    {
        var ownership = RuntimeProtocolGuard.Validate(
            accessContext,
            agentId,
            runtimeId);

        if (ownership.IsFailure)
            return Result<Question>.Failure(ownership.Error!);

        if (timeout <= TimeSpan.Zero)
        {
            return Result<Question>.Failure(
                Error.Validation(
                    "hive.agent.question.invalid-timeout",
                    "Question timeout must be greater than zero."));
        }

        var question = Question.Create(
            accessContext,
            agentId,
            runtimeId,
            prompt,
            timeout,
            askedAtUtc,
            CorrelationId.New());

        lock (_sync)
        {
            _questions.Add(question.Id, question);
            _waiters.Add(
                question.Id,
                new TaskCompletionSource<Result<Question>>(
                    TaskCreationOptions.RunContinuationsAsynchronously));
        }

        return Result<Question>.Success(question);
    }

    public async Task<Result<Question>> WaitAsync(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        QuestionId questionId,
        CancellationToken cancellationToken = default)
    {
        var ownership = RuntimeProtocolGuard.Validate(
            accessContext,
            agentId,
            runtimeId);

        if (ownership.IsFailure)
            return Result<Question>.Failure(ownership.Error!);

        Task<Result<Question>> waitTask;

        lock (_sync)
        {
            if (!_questions.TryGetValue(questionId, out var question))
            {
                return Result<Question>.Failure(
                    new Error(
                        "hive.agent.question.not-found",
                        ErrorCategory.NotFound,
                        "The requested Question does not exist in this RuntimeInstance."));
            }

            if (question.AskedByAgentId != agentId ||
                question.AskedByRuntimeId != runtimeId)
            {
                return Result<Question>.Failure(
                    new Error(
                        "hive.agent.question.runtime-mismatch",
                        ErrorCategory.Forbidden,
                        "The requested Question belongs to another runtime boundary."));
            }

            if (question.Status != QuestionStatus.Waiting)
                return Result<Question>.Success(question);

            waitTask = _waiters[questionId].Task;
        }

        try
        {
            return await waitTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Result<Question>.Failure(
                Error.Cancelled(
                    "hive.agent.question.wait-cancelled",
                    "Waiting for the Question answer was cancelled."));
        }
    }

    public Result<Question> Answer(
        ResourceAccessContext responderContext,
        QuestionId questionId,
        AgentId responderAgentId,
        RuntimeId responderRuntimeId,
        string answer,
        DateTimeOffset answeredAtUtc)
    {
        var ownership = RuntimeProtocolGuard.Validate(
            responderContext,
            responderAgentId,
            responderRuntimeId);

        if (ownership.IsFailure)
            return Result<Question>.Failure(ownership.Error!);

        lock (_sync)
        {
            if (!_questions.TryGetValue(questionId, out var question))
            {
                return Result<Question>.Failure(
                    new Error(
                        "hive.agent.question.not-found",
                        ErrorCategory.NotFound,
                        "The requested Question does not exist in this RuntimeInstance."));
            }

            if (question.AskedByAgentId != responderAgentId ||
                question.AskedByRuntimeId != responderRuntimeId)
            {
                return Result<Question>.Failure(
                    new Error(
                        "hive.agent.question.responder-mismatch",
                        ErrorCategory.Forbidden,
                        "Only the Question's owning RuntimeInstance may answer it."));
            }

            var result = question.ApplyAnswer(
                responderAgentId,
                responderRuntimeId,
                answer,
                answeredAtUtc);

            if (result.IsFailure)
                return result;

            _questions[questionId] = result.Value!;
            CompleteWaiter(questionId, result);
            return result;
        }
    }

    public Result<Question> Cancel(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        QuestionId questionId,
        DateTimeOffset cancelledAtUtc)
    {
        var ownership = RuntimeProtocolGuard.Validate(
            accessContext,
            agentId,
            runtimeId);

        if (ownership.IsFailure)
            return Result<Question>.Failure(ownership.Error!);

        lock (_sync)
        {
            if (!_questions.TryGetValue(questionId, out var question))
            {
                return Result<Question>.Failure(
                    new Error(
                        "hive.agent.question.not-found",
                        ErrorCategory.NotFound,
                        "The requested Question does not exist in this RuntimeInstance."));
            }

            if (question.AskedByAgentId != agentId ||
                question.AskedByRuntimeId != runtimeId)
            {
                return Result<Question>.Failure(
                    new Error(
                        "hive.agent.question.runtime-mismatch",
                        ErrorCategory.Forbidden,
                        "The requested Question belongs to another runtime boundary."));
            }

            var result = question.Cancel(cancelledAtUtc);

            if (result.IsFailure)
                return result;

            _questions[questionId] = result.Value!;
            CompleteWaiter(questionId, result);
            return result;
        }
    }

    public int ExpireDue()
    {
        lock (_sync)
        {
            var now = _clock.UtcNow;
            var expired = 0;

            foreach (var question in _questions.Values.ToArray())
            {
                if (question.Status != QuestionStatus.Waiting ||
                    question.ExpiresAtUtc > now)
                {
                    continue;
                }

                var result = question.Timeout(now);
                if (result.IsFailure)
                    continue;

                _questions[question.Id] = result.Value!;
                CompleteWaiter(question.Id, result);
                expired++;
            }

            return expired;
        }
    }

    private void CompleteWaiter(
        QuestionId questionId,
        Result<Question> result)
    {
        if (_waiters.Remove(questionId, out var waiter))
            waiter.TrySetResult(result);
    }
}
