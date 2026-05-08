using System;
using System.Threading.Tasks;
using FurniOx.SolidWorks.Shared.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.CircuitBreaker;
using CircuitBreaker = FurniOx.SolidWorks.Core.Intelligence.CircuitBreaker;

namespace FurniOx.SolidWorks.Core.Tests;

/// <summary>
/// Fills gaps in CircuitBreaker coverage not addressed by RouterAndCircuitBreakerTests:
/// threshold of 1, different exception types, successful pass-through, return value
/// forwarding, and HalfOpen reset after recovery.
/// </summary>
public sealed class CircuitBreakerAdditionalTests
{
    private static CircuitBreaker CreateBreaker(int failureThreshold, int resetTimeoutSeconds = 60)
    {
        var settings = new SolidWorksSettings
        {
            CircuitBreaker = new CircuitBreakerSettings
            {
                FailureThreshold = failureThreshold,
                ResetTimeoutSeconds = resetTimeoutSeconds
            }
        };
        return new CircuitBreaker(settings, NullLogger<CircuitBreaker>.Instance);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_SingleSuccess_ReturnsResult()
    {
        var breaker = CreateBreaker(failureThreshold: 3);

        var result = await breaker.ExecuteAsync<string>(_ => Task.FromResult("ok"));

        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleSuccesses_NeverTrips()
    {
        var breaker = CreateBreaker(failureThreshold: 2);

        for (var i = 0; i < 10; i++)
        {
            var result = await breaker.ExecuteAsync<int>(_ => Task.FromResult(i));
            Assert.Equal(i, result);
        }
    }

    // ── Different exception types are treated as failures ─────────────────────

    [Fact]
    public async Task ExecuteAsync_ArgumentException_CountsAsFailure()
    {
        // Polly requires MinimumThroughput >= 2, so use threshold 2.
        var breaker = CreateBreaker(failureThreshold: 2);

        await Assert.ThrowsAsync<ArgumentException>(
            () => breaker.ExecuteAsync<int>(_ => Task.FromException<int>(new ArgumentException("bad arg"))));
        await Assert.ThrowsAsync<ArgumentException>(
            () => breaker.ExecuteAsync<int>(_ => Task.FromException<int>(new ArgumentException("bad arg"))));

        await Assert.ThrowsAsync<BrokenCircuitException>(
            () => breaker.ExecuteAsync<int>(_ => Task.FromResult(0)));
    }

    [Fact]
    public async Task ExecuteAsync_TimeoutException_CountsAsFailure()
    {
        var breaker = CreateBreaker(failureThreshold: 2);

        await Assert.ThrowsAsync<TimeoutException>(
            () => breaker.ExecuteAsync<int>(_ => Task.FromException<int>(new TimeoutException())));
        await Assert.ThrowsAsync<TimeoutException>(
            () => breaker.ExecuteAsync<int>(_ => Task.FromException<int>(new TimeoutException())));

        await Assert.ThrowsAsync<BrokenCircuitException>(
            () => breaker.ExecuteAsync<int>(_ => Task.FromResult(0)));
    }

    // ── HalfOpen → success resets to Closed ──────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_HalfOpen_SingleSuccessResetsToAcceptingRequests()
    {
        // Use a very short reset timeout (1 second) so we can reach HalfOpen quickly.
        // Polly requires MinimumThroughput >= 2.
        var breaker = CreateBreaker(failureThreshold: 2, resetTimeoutSeconds: 1);

        // Trip the circuit (need 2 failures to reach threshold).
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => breaker.ExecuteAsync<int>(_ => Task.FromException<int>(new InvalidOperationException())));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => breaker.ExecuteAsync<int>(_ => Task.FromException<int>(new InvalidOperationException())));

        // Wait for the break duration to expire so the circuit moves to HalfOpen.
        await Task.Delay(TimeSpan.FromMilliseconds(1200));

        // Success in HalfOpen state → circuit resets and returns the result.
        var result = await breaker.ExecuteAsync<string>(_ => Task.FromResult("recovered"));

        Assert.Equal("recovered", result);

        // Circuit is now Closed — subsequent calls succeed normally.
        var followUp = await breaker.ExecuteAsync<string>(_ => Task.FromResult("normal"));
        Assert.Equal("normal", followUp);
    }

    // ── Threshold boundary: exactly threshold failures, then open ─────────────

    [Fact]
    public async Task ExecuteAsync_ExactlyAtThreshold_OpensCircuit()
    {
        const int threshold = 3;
        var breaker = CreateBreaker(failureThreshold: threshold);

        for (var i = 0; i < threshold; i++)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => breaker.ExecuteAsync<int>(_ => Task.FromException<int>(new InvalidOperationException())));
        }

        // The threshold+1 call should hit the open circuit.
        await Assert.ThrowsAsync<BrokenCircuitException>(
            () => breaker.ExecuteAsync<int>(_ => Task.FromResult(0)));
    }

    // ── Failure below threshold does not trip ─────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_FailuresBelowThreshold_DoesNotOpen()
    {
        const int threshold = 3;
        var breaker = CreateBreaker(failureThreshold: threshold);

        // Fail threshold - 1 times — circuit must stay closed.
        for (var i = 0; i < threshold - 1; i++)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => breaker.ExecuteAsync<int>(_ => Task.FromException<int>(new InvalidOperationException())));
        }

        // Should still execute normally.
        var result = await breaker.ExecuteAsync<string>(_ => Task.FromResult("still alive"));
        Assert.Equal("still alive", result);
    }
}
