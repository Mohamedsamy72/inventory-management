namespace Inventory.Domain.Enums;

/// <summary>The document series document_sequences allocates numbers for (docs/28).
/// Formats: ITM-{000000} lifetime; REC-/REQ-/SUP-/CNT-/DSC-{YYYYMM}-{0000} monthly.</summary>
public enum DocumentType
{
    Item,
    ReceivingOrder,
    SupplyRequest,
    Supply,
    StockCount,
    Discrepancy,
}
