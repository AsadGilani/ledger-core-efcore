using System.Collections.Generic;

namespace LedgerCoreApp
{
    public class Account
    {
        public string AccountId { get; set; } = string.Empty;
        public Currency Currency { get; set; }
        public decimal OpeningBalance { get; set; }
        public ICollection<LedgerEntry> LedgerEntries { get; set; } = new List<LedgerEntry>();
        public ICollection<AuthorizationHold> Holds { get; set; } = new List<AuthorizationHold>();
        public ICollection<AuditError> Errors { get; set; } = new List<AuditError>();
        public ICollection<FeeAssessment> FeesAssessed { get; set; } = new List<FeeAssessment>();
    }

    public class LedgerEntry
    {
        public int Id { get; set; }
        public string EventId { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public int Day { get; set; }
        public int ValueDate { get; set; }
        public EntryType Type { get; set; }
        public decimal Amount { get; set; }
        public Account Account { get; set; } = null!;
    }

    public class AuthorizationHold
    {
        public int Id { get; set; }
        public string AuthId { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public bool IsActive { get; set; } = true;
        public Account Account { get; set; } = null!;
    }

    public class AuditError
    {
        public int Id { get; set; }
        public string EventId { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public int Day { get; set; }
        public string Message { get; set; } = string.Empty;
        public Account Account { get; set; } = null!;
    }

    public class FeeAssessment
    {
        public int Id { get; set; }
        public int Day { get; set; }
        public string AccountId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public Account Account { get; set; } = null!;
    }
}
