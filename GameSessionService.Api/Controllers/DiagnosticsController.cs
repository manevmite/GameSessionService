using System.Diagnostics;
using GameSessionService.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameSessionService.Api.Controllers;

/// <summary>
/// Controller for diagnostics and performance testing
/// </summary>
[ApiController]
[Route("[controller]")]
public class DiagnosticsController : ControllerBase
{
    private readonly ISessionService _sessionService;
    private readonly ILogger<DiagnosticsController> _logger;

    public DiagnosticsController(
        ISessionService sessionService,
        ILogger<DiagnosticsController> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <summary>
    /// Performance test endpoint that benchmarks session retrieval
    /// GET /api/diagnostics/perf-test?iterations=1000
    /// </summary>
    /// <param name="iterations">Number of iterations to run (default: 1000)</param>
    /// <returns>Performance timing summary</returns>
    [HttpGet("perf-test")]
    [ProducesResponseType(typeof(PerformanceTestResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<PerformanceTestResult>> PerformanceTest([FromQuery] int iterations = 1000)
    {
        if (iterations <= 0 || iterations > 100000)
        {
            return BadRequest("Iterations must be between 1 and 100,000.");
        }

        var correlationId = HttpContext.Items["CorrelationId"]?.ToString() ?? string.Empty;
        _logger.LogInformation("Starting performance test with {Iterations} iterations, CorrelationId: {CorrelationId}", 
            iterations, correlationId);

        // First, create a test session to retrieve
        var testRequest = new Models.CreateSessionRequest
        {
            PlayerId = "PERF_TEST_PLAYER",
            GameId = "PERF_TEST_GAME"
        };

        var testSession = await _sessionService.CreateSessionAsync(testRequest, correlationId);
        var sessionId = testSession.SessionId;

        // Clear cache for first iteration to ensure we test both cache miss and cache hit scenarios
        // Note: In a real scenario, we'd have a way to clear cache, but for this test we'll just measure

        var overallStopwatch = Stopwatch.StartNew();
        var timings = new List<long>(iterations);
        var cacheHits = 0;
        var cacheMisses = 0;

        // Run iterations
        for (int i = 0; i < iterations; i++)
        {
            var iterationStopwatch = Stopwatch.StartNew();
            var (session, fromCache) = await _sessionService.GetSessionAsync(sessionId, $"{correlationId}-{i}");
            iterationStopwatch.Stop();

            timings.Add(iterationStopwatch.ElapsedMilliseconds);

            if (fromCache)
            {
                cacheHits++;
            }
            else
            {
                cacheMisses++;
            }
        }

        overallStopwatch.Stop();

        // Calculate statistics
        var totalTime = overallStopwatch.ElapsedMilliseconds;
        var averageTime = timings.Average();
        var minTime = timings.Min();
        var maxTime = timings.Max();
        var medianTime = CalculateMedian(timings);
        var p95Time = CalculatePercentile(timings, 95);
        var p99Time = CalculatePercentile(timings, 99);

        var result = new PerformanceTestResult
        {
            Iterations = iterations,
            TotalTimeMs = totalTime,
            AverageTimeMs = Math.Round(averageTime, 2),
            MinTimeMs = minTime,
            MaxTimeMs = maxTime,
            MedianTimeMs = medianTime,
            P95TimeMs = p95Time,
            P99TimeMs = p99Time,
            CacheHits = cacheHits,
            CacheMisses = cacheMisses,
            RequestsPerSecond = Math.Round(iterations / (totalTime / 1000.0), 2)
        };

        _logger.LogInformation(
            "Performance test completed. Iterations: {Iterations}, TotalTime: {TotalTime}ms, AvgTime: {AvgTime}ms, CacheHitRate: {CacheHitRate}%, CorrelationId: {CorrelationId}",
            iterations, totalTime, averageTime, (cacheHits * 100.0 / iterations), correlationId);

        return Ok(result);
    }

    /// <summary>
    /// Calculates the median value from a list of timings
    /// </summary>
    private static long CalculateMedian(List<long> values)
    {
        var sorted = values.OrderBy(x => x).ToList();
        var count = sorted.Count;
        
        if (count == 0) return 0;
        if (count % 2 == 0)
        {
            return (sorted[count / 2 - 1] + sorted[count / 2]) / 2;
        }
        
        return sorted[count / 2];
    }

    /// <summary>
    /// Calculates the percentile value from a list of timings
    /// </summary>
    private static long CalculatePercentile(List<long> values, int percentile)
    {
        var sorted = values.OrderBy(x => x).ToList();
        var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
    }
}

/// <summary>
/// Result model for performance test
/// </summary>
public class PerformanceTestResult
{
    public int Iterations { get; set; }
    public long TotalTimeMs { get; set; }
    public double AverageTimeMs { get; set; }
    public long MinTimeMs { get; set; }
    public long MaxTimeMs { get; set; }
    public long MedianTimeMs { get; set; }
    public long P95TimeMs { get; set; }
    public long P99TimeMs { get; set; }
    public int CacheHits { get; set; }
    public int CacheMisses { get; set; }
    public double RequestsPerSecond { get; set; }
}

