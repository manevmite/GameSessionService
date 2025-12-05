# Concurrency Safety and Idempotency

This document explains how the Game Session Service handles concurrency and ensures idempotency.

## Concurrency Safety

### Problem
When multiple requests try to create a session for the same player and game simultaneously, we need to prevent duplicate sessions.

### Solution

The service implements a **multi-layered approach**:

#### 1. Pre-Check (Optimistic)
Before attempting to create a session, we check if an active session already exists:
```csharp
var hasActiveSession = await _repository.HasActiveSessionAsync(request.PlayerId, request.GameId);
```

#### 2. Semaphore Lock (Pessimistic)
A `SemaphoreSlim` ensures only one creation operation proceeds at a time:
```csharp
await _createLock.WaitAsync();
try
{
    // Creation logic
}
finally
{
    _createLock.Release();
}
```

#### 3. Double-Check Pattern
After acquiring the lock, we re-check for existing sessions (another thread might have created one while we were waiting):
```csharp
hasActiveSession = await _repository.HasActiveSessionAsync(request.PlayerId, request.GameId);
if (hasActiveSession)
{
    // Handle conflict
}
```

#### 4. Thread-Safe Repository
The repository uses `ConcurrentDictionary` for atomic operations:
```csharp
private readonly ConcurrentDictionary<string, Session> _sessions = new();
```

### Benefits
- **Prevents Race Conditions:** Semaphore ensures atomicity
- **Minimizes Lock Contention:** Pre-check reduces unnecessary locking
- **Handles Edge Cases:** Double-check catches late-arriving duplicates

## Idempotency

### Problem
Clients may retry requests due to network issues, timeouts, or other transient failures. We need to ensure that:
- Multiple identical requests produce the same result
- No side effects from duplicate requests

### Solution

#### For Session Creation

1. **Duplicate Detection:**
   - Check for existing active sessions before creation
   - Return existing session if found (idempotent behavior)

2. **Conflict Response:**
   - Return `409 Conflict` with clear message
   - Log the conflict for monitoring

3. **Safe Retries:**
   - Clients can safely retry the same request
   - Service returns consistent results

#### For External Provider Calls

When calling external services (e.g., payment providers, game engines), implement:

1. **Idempotency Key Generation:**
   ```csharp
   var idempotencyKey = $"operation:{playerId}:{gameId}:{timestamp}";
   ```

2. **Idempotency Store:**
   - Store request/response pairs with TTL
   - Use distributed cache (Redis) for multi-instance scenarios

3. **Request Deduplication:**
   ```csharp
   // Check if request was already processed
   var cachedResult = await _idempotencyStore.GetAsync(idempotencyKey);
   if (cachedResult != null)
   {
       return cachedResult; // Return cached result
   }
   
   // Process request
   var result = await _externalProvider.ProcessAsync(request);
   
   // Cache result
   await _idempotencyStore.SetAsync(idempotencyKey, result, TimeSpan.FromHours(24));
   
   return result;
   ```

4. **Idempotency Headers:**
   - Accept `Idempotency-Key` header from clients
   - Use it as the cache key

### Implementation Example

Here's a complete example for external provider idempotency:

```csharp
public class IdempotentExternalService
{
    private readonly IMemoryCache _cache;
    private readonly IExternalProvider _provider;
    private readonly ILogger<IdempotentExternalService> _logger;

    public async Task<Result> ProcessWithIdempotencyAsync(
        Request request, 
        string idempotencyKey)
    {
        // Check cache
        var cacheKey = $"idempotency:{idempotencyKey}";
        if (_cache.TryGetValue(cacheKey, out Result? cachedResult))
        {
            _logger.LogInformation("Returning cached result for idempotency key: {Key}", idempotencyKey);
            return cachedResult!;
        }

        // Process request
        try
        {
            var result = await _provider.ProcessAsync(request);
            
            // Cache result
            _cache.Set(cacheKey, result, TimeSpan.FromHours(24));
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing request with idempotency key: {Key}", idempotencyKey);
            throw;
        }
    }
}
```

## Best Practices

### 1. Use Distributed Locks for Multi-Instance
For production with multiple instances, use:
- **Redis Distributed Lock** (RedLock algorithm)
- **Database Pessimistic Locking** (SELECT FOR UPDATE)
- **Lease-based locking** (AWS DynamoDB, Azure Cosmos DB)

### 2. Idempotency Key Format
Use a consistent format:
```
{operation}:{entityId}:{timestamp}
```
Example: `create_session:P123:G100:20240115T103000Z`

### 3. TTL for Idempotency Records
- **Short operations:** 1 hour
- **Long operations:** 24 hours
- **Financial operations:** 30 days (compliance)

### 4. Monitoring
Log all idempotency checks:
- Cache hits (duplicate requests detected)
- Cache misses (new requests)
- Conflict resolutions

## Testing Concurrency

### Load Testing
Use tools like:
- **Apache Bench (ab)**
- **JMeter**
- **k6**
- **Locust**

Example:
```bash
# Send 100 concurrent requests
ab -n 100 -c 10 -p request.json -T application/json \
   http://localhost:5000/sessions/start
```

### Unit Testing
Test concurrent scenarios:
```csharp
[Fact]
public async Task CreateSession_ConcurrentRequests_OnlyOneCreated()
{
    var tasks = Enumerable.Range(0, 10)
        .Select(_ => _service.CreateSessionAsync(request, correlationId))
        .ToArray();
    
    var results = await Task.WhenAll(tasks);
    
    // Verify only one session was created
    var uniqueSessions = results.Select(r => r.SessionId).Distinct();
    Assert.Single(uniqueSessions);
}
```

## Summary

- ✅ **Concurrency Safety:** Semaphore + double-check pattern
- ✅ **Idempotency:** Duplicate detection + caching
- ✅ **Thread Safety:** ConcurrentDictionary in repository
- ✅ **External Calls:** Idempotency keys + result caching
- ✅ **Monitoring:** Structured logging with correlation IDs

