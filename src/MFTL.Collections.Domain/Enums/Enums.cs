namespace MFTL.Collections.Domain.Enums;

public enum ContributionStatus
{
    Pending,
    AwaitingPayment,
    RecordedCash,
    Completed,
    Failed,
    Cancelled,
    Reversed
}

public enum PaymentStatus
{
    Pending,
    Initiated,
    Processing,
    Succeeded,
    Failed,
    Cancelled,
    Refunded,
    Reversed
}

public enum ReceiptStatus
{
    Issued,
    Voided
}

public enum ReconciliationStatus
{
    Pending,
    Matched,
    Exception,
    Adjusted
}
