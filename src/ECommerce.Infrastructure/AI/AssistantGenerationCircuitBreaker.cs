namespace ECommerce.Infrastructure.AI;

/// <summary>A process-local, single-probe circuit breaker for answer generation.</summary>
public sealed class AssistantGenerationCircuitBreaker
{
    private readonly object _sync = new();
    private readonly int _failureThreshold;
    private readonly TimeSpan _openDuration;
    private readonly TimeProvider _timeProvider;
    private int _consecutiveFailures;
    private DateTimeOffset? _openUntil;
    private bool _halfOpenProbeInFlight;

    public AssistantGenerationCircuitBreaker(GeminiOptions options, TimeProvider? timeProvider = null)
    {
        _failureThreshold = Math.Clamp(options.GenerationFailureThreshold, 1, 20);
        _openDuration = TimeSpan.FromSeconds(Math.Clamp(options.GenerationCircuitOpenSeconds, 1, 3600));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool TryAcquire(out bool isHalfOpenProbe)
    {
        lock (_sync)
        {
            isHalfOpenProbe = false;
            if (_openUntil is null) return true;
            if (_timeProvider.GetUtcNow() < _openUntil.Value) return false;
            if (_halfOpenProbeInFlight) return false;
            _halfOpenProbeInFlight = true;
            isHalfOpenProbe = true;
            return true;
        }
    }

    public void RecordSuccess()
    {
        lock (_sync)
        {
            _consecutiveFailures = 0;
            _openUntil = null;
            _halfOpenProbeInFlight = false;
        }
    }

    public void AbandonHalfOpenProbe(bool wasHalfOpenProbe)
    {
        if (!wasHalfOpenProbe) return;
        lock (_sync) _halfOpenProbeInFlight = false;
    }

    public void RecordFailure(bool wasHalfOpenProbe)
    {
        lock (_sync)
        {
            _consecutiveFailures++;
            _halfOpenProbeInFlight = false;
            if (wasHalfOpenProbe || _consecutiveFailures >= _failureThreshold)
            {
                _openUntil = _timeProvider.GetUtcNow() + _openDuration;
                _consecutiveFailures = 0;
            }
        }
    }
}
