using Inventory.Domain.Enums;

namespace Inventory.Application.Common;

/// <summary>
/// Task 5.1 (docs/28 §5.3, ADR-025) - allocates the next gap-free business identifier for the
/// current tenant, formatted per docs/28 §4's catalog (<c>ITM-000001</c>,
/// <c>REC-202609-0001</c>, ...). Allocation happens via a single
/// <c>INSERT ... ON CONFLICT ... DO UPDATE ... RETURNING</c> raw-SQL statement, executed
/// IMMEDIATELY against the database over the caller's own <c>DbContext</c> connection - it is
/// NOT merely added to the change tracker for a later <c>SaveChangesAsync</c> to flush. docs/28
/// §5.3 requires this to run "inside the business transaction" so a rejected document leaves
/// <c>last_value</c> unchanged (AC-28-3): the CALLER is therefore responsible for opening an
/// explicit <c>IDbContextTransaction</c> BEFORE calling <see cref="AllocateAsync"/> and
/// committing it only after the entity itself is also saved - calling this without an ambient
/// transaction already open lets the allocation commit on its own, independently of whatever
/// happens to the entity afterward.
/// </summary>
public interface IDocumentSequenceService
{
    /// <summary>Allocates and returns the next formatted identifier, e.g. <c>ITM-000042</c>.
    /// Throws <see cref="SequenceExhaustedException"/> at the documented ceiling (999999
    /// lifetime, 9999 monthly) rather than silently wrapping. Must be called inside an explicit
    /// transaction the caller also uses for the entity it is numbering - see the type-level
    /// remarks.</summary>
    Task<string> AllocateAsync(DocumentType documentType, CancellationToken cancellationToken);
}

/// <summary>docs/28 §Overflow - reaching the ceiling is an operational escalation, mapped to
/// <c>409 SEQUENCE_EXHAUSTED</c> by the endpoint that catches it.</summary>
public sealed class SequenceExhaustedException : Exception
{
    public SequenceExhaustedException(DocumentType documentType)
        : base($"Sequence exhausted for document type {documentType}.")
    {
        DocumentType = documentType;
    }

    public DocumentType DocumentType { get; }
}
