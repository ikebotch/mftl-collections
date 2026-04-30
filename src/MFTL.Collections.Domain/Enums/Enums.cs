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

public enum NotificationStatus
{
    Pending,
    Sent,
    Failed,
    Skipped
}

public enum OutboxMessageStatus
{
    Pending,
    Processing,
    Sent,
    Failed,
    DeadLetter
}

public enum NotificationChannel
{
    Sms,
    Email,
    Push,
    InApp,
    WhatsApp
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
