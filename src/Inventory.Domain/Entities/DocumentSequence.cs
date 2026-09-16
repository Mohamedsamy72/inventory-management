using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities;

/// <summary>
/// Gap-free counter for one (company, document type, period). Composite primary key - no
/// surrogate id, matching docs/29 section 4.2. Allocation happens through a single transactional
/// <c>INSERT ... ON CONFLICT ... DO UPDATE ... RETURNING</c> statement (docs/28 section 5.3,
/// ADR-025) - never <c>SELECT MAX(...) + 1</c> and never a PostgreSQL <c>SEQUENCE</c> object,
/// both of which can produce gaps that read to an auditor as a deleted record.
/// </summary>
public sealed class DocumentSequence
{
    private DocumentSequence()
    {
    }

    public DocumentSequence(Guid companyId, DocumentType documentType, string periodKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(periodKey);

        CompanyId = companyId;
        DocumentType = documentType;
        PeriodKey = periodKey;
        LastValue = 0;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public Guid CompanyId { get; private set; }
    public DocumentType DocumentType { get; private set; }

    /// <summary>"YYYYMM" for monthly series, or "LIFETI" for the lifetime item series.</summary>
    public string PeriodKey { get; private set; } = string.Empty;

    public long LastValue { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
}
