using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Inventory.Application.Common;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Idempotency;

/// <summary>See <see cref="IIdempotencyService"/>.</summary>
public sealed class IdempotencyService : IIdempotencyService
{
    private static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    private readonly InventoryDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public IdempotencyService(InventoryDbContext context, ICurrentUserService currentUserService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<IdempotencyCheckResult> CheckAsync(string idempotencyKey, string endpoint, object requestBody, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        string requestHash = ComputeHash(endpoint, requestBody);
        Guid userId = _currentUserService.UserId;

        IdempotencyRecord? existing = await _context.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.UserId == userId && r.IdempotencyKey == idempotencyKey, cancellationToken);

        if (existing is null)
        {
            return new IdempotencyCheckResult(IdempotencyOutcome.New);
        }

        return existing.RequestHash == requestHash
            ? new IdempotencyCheckResult(IdempotencyOutcome.Replay, existing.ResponseStatusCode, existing.ResponsePayloadJson)
            : new IdempotencyCheckResult(IdempotencyOutcome.KeyReuse);
    }

    public void RecordResponse(string idempotencyKey, string endpoint, object requestBody, int statusCode, object? responsePayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        string requestHash = ComputeHash(endpoint, requestBody);
        string? payloadJson = responsePayload is null ? null : JsonSerializer.Serialize(responsePayload);

        var record = new IdempotencyRecord(
            _currentUserService.CompanyId, _currentUserService.UserId, idempotencyKey, endpoint, requestHash,
            statusCode, payloadJson, DateTimeOffset.UtcNow.Add(Retention));

        _context.IdempotencyRecords.Add(record);
    }

    public async Task<int> CleanupExpiredAsync(CancellationToken cancellationToken) =>
        await _context.IdempotencyRecords.IgnoreQueryFilters()
            .Where(r => r.ExpiresAt < DateTimeOffset.UtcNow)
            .ExecuteDeleteAsync(cancellationToken);

    private static string ComputeHash(string endpoint, object requestBody)
    {
        string canonical = endpoint + "|" + JsonSerializer.Serialize(requestBody);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexStringLower(hash);
    }
}
