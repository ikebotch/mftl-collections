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

public enum OutboxMessageStatus
{
    Pending,
    Processing,
    Sent,
    Failed,
    DeadLetter
}

public enum NotificationStatus
{
    Pending,
    Sent,
    Failed,
    Skipped
}

public enum NotificationChannel
{
    Email,
    Sms,
    WhatsApp,
    Push,
    InApp
}
