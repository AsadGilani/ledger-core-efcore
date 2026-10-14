using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace LedgerCoreApp
{
    public class LedgerService
    {
        private readonly LedgerDbContext _db;
        private readonly Dictionary<string, List<decimal>> _dailyInterestTracker = new()
        {
            { "ACC-001", new List<decimal>() },
            { "ACC-002", new List<decimal>() }
        };

        public LedgerService(LedgerDbContext db)
        {
            _db = db;
        }

        public decimal RoundCurrency(decimal amount, Currency currency)
        {
            int decimals = currency == Currency.BHD ? 3 : 2;
            return Math.Round(amount, decimals, MidpointRounding.AwayFromZero);
        }

        public decimal GetLedgerBalance(string accountId, int upToDay)
        {
            var entries = _db.LedgerEntries
                .Where(e => e.AccountId == accountId && e.ValueDate <= upToDay)
                .ToList();

            decimal total = 0;
            foreach (var e in entries)
            {
                if (e.Type == EntryType.Credit || e.Type == EntryType.Interest || e.Type == EntryType.ReversalCredit)
                    total += e.Amount;
                else if (e.Type == EntryType.Debit || e.Type == EntryType.Fee || e.Type == EntryType.Settlement)
                    total -= e.Amount;
            }
            var acc = _db.Accounts.First(a => a.AccountId == accountId);
            return RoundCurrency(total, acc.Currency);
        }

        public decimal GetAvailableBalance(string accountId, int upToDay)
        {
            var acc = _db.Accounts.First(a => a.AccountId == accountId);
            decimal ledgerBal = GetLedgerBalance(accountId, upToDay);
            decimal activeHolds = _db.Holds
                .Where(h => h.AccountId == accountId && h.IsActive)
                .Sum(h => h.Amount);

            return RoundCurrency(ledgerBal - activeHolds, acc.Currency);
        }

        public void ProcessEvent(string eventId, int day, string accountId, string eventType, decimal amount, int valueDate, string? authId = null, string? targetId = null)
        {
            var acc = _db.Accounts.Include(a => a.Holds).FirstOrDefault(a => a.AccountId == accountId);
            if (acc == null) return;

            switch (eventType)
            {
                case "CREDIT":
                    _db.LedgerEntries.Add(new LedgerEntry
                    {
                        EventId = eventId,
                        AccountId = accountId,
                        Day = day,
                        ValueDate = valueDate,
                        Type = EntryType.Credit,
                        Amount = RoundCurrency(amount, acc.Currency)
                    });
                    break;

                case "DEBIT":
                    _db.LedgerEntries.Add(new LedgerEntry
                    {
                        EventId = eventId,
                        AccountId = accountId,
                        Day = day,
                        ValueDate = valueDate,
                        Type = EntryType.Debit,
                        Amount = RoundCurrency(amount, acc.Currency)
                    });
                    break;

                case "AUTHORIZATION":
                    decimal holdAmt = RoundCurrency(amount, acc.Currency);
                    decimal avail = GetAvailableBalance(accountId, day);
                    if (avail >= holdAmt)
                    {
                        _db.Holds.Add(new AuthorizationHold
                        {
                            AuthId = authId!,
                            AccountId = accountId,
                            Amount = holdAmt,
                            IsActive = true
                        });
                    }
                    else
                    {
                        _db.Errors.Add(new AuditError
                        {
                            EventId = eventId,
                            AccountId = accountId,
                            Day = day,
                            Message = $"Auth {authId} declined: insufficient available balance"
                        });
                    }
                    break;

                case "SETTLEMENT":
                    decimal settleAmt = RoundCurrency(amount, acc.Currency);
                    var hold = _db.Holds.FirstOrDefault(h => h.AccountId == accountId && h.AuthId == authId && h.IsActive);
                    if (hold == null)
                    {
                        _db.Errors.Add(new AuditError
                        {
                            EventId = eventId,
                            AccountId = accountId,
                            Day = day,
                            Message = $"Settlement rejected: Auth ID {authId} not present in ledger"
                        });
                    }
                    else
                    {
                        hold.IsActive = false;
                        _db.LedgerEntries.Add(new LedgerEntry
                        {
                            EventId = eventId,
                            AccountId = accountId,
                            Day = day,
                            ValueDate = valueDate,
                            Type = EntryType.Settlement,
                            Amount = settleAmt
                        });
                    }
                    break;

                case "REVERSAL":
                    var target = _db.LedgerEntries.FirstOrDefault(e => e.AccountId == accountId && e.EventId == targetId);
                    if (target == null)
                    {
                        _db.Errors.Add(new AuditError
                        {
                            EventId = eventId,
                            AccountId = accountId,
                            Day = day,
                            Message = $"Reversal rejected: target event {targetId} not found"
                        });
                    }
                    else
                    {
                        var revType = target.Type == EntryType.Debit ? EntryType.ReversalCredit : EntryType.Debit;
                        _db.LedgerEntries.Add(new LedgerEntry
                        {
                            EventId = eventId,
                            AccountId = accountId,
                            Day = day,
                            ValueDate = valueDate,
                            Type = revType,
                            Amount = target.Amount
                        });
                    }
                    break;

                case "CREDIT_INSTALLMENTS":
                    decimal instAmt = RoundCurrency(amount / 3m, acc.Currency);
                    decimal lastInst = RoundCurrency(amount - (instAmt * 2), acc.Currency);
                    decimal[] amounts = { instAmt, instAmt, lastInst };
                    for (int i = 0; i < amounts.Length; i++)
                    {
                        _db.LedgerEntries.Add(new LedgerEntry
                        {
                            EventId = $"{eventId}-part{i + 1}",
                            AccountId = accountId,
                            Day = day,
                            ValueDate = valueDate,
                            Type = EntryType.Credit,
                            Amount = amounts[i]
                        });
                    }
                    break;
            }
            _db.SaveChanges();
        }

        public void RunSimulation()
        {
            // Seed Accounts
            _db.Accounts.Add(new Account { AccountId = "ACC-001", Currency = Currency.AED, OpeningBalance = 0.00m });
            _db.Accounts.Add(new Account { AccountId = "ACC-002", Currency = Currency.BHD, OpeningBalance = 0.000m });
            _db.SaveChanges();

            // Replay Event Stream
            ProcessEvent("E1", 1, "ACC-001", "CREDIT", 1200.00m, 1);
            ProcessEvent("E2", 1, "ACC-001", "DEBIT", 950.00m, 1);
            ProcessEvent("E3", 2, "ACC-001", "AUTHORIZATION", 200.00m, 2, authId: "Auth-A");
            ProcessEvent("E4", 3, "ACC-001", "CREDIT", 400.00m, 3);
            ProcessEvent("E5", 4, "ACC-001", "SETTLEMENT", 185.00m, 4, authId: "Auth-A");
            ProcessEvent("E6", 4, "ACC-001", "SETTLEMENT", 180.00m, 4, authId: "Auth-Z"); // Unmatched
            ProcessEvent("E7", 5, "ACC-001", "DEBIT", 620.00m, 2);
            ProcessEvent("E8", 5, "ACC-001", "AUTHORIZATION", 90.00m, 5, authId: "Auth-B");
            ProcessEvent("E10", 5, "ACC-002", "CREDIT_INSTALLMENTS", 10.000m, 5);
            ProcessEvent("E9", 6, "ACC-001", "REVERSAL", 0m, 2, targetId: "E7");

            // Daily Processing Days 1 to 6
            for (int day = 1; day <= 6; day++)
            {
                Console.WriteLine($"\n--- DAY {day} ---");
                foreach (var acc in _db.Accounts.ToList())
                {
                    decimal closingBal = GetLedgerBalance(acc.AccountId, day);

                    if (closingBal < 0 && acc.Currency == Currency.AED)
                    {
                        decimal feeAmt = 25.00m;
                        _db.FeesAssessed.Add(new FeeAssessment { Day = day, AccountId = acc.AccountId, Amount = feeAmt });
                        _db.LedgerEntries.Add(new LedgerEntry
                        {
                            EventId = $"FEE-D{day}",
                            AccountId = acc.AccountId,
                            Day = day,
                            ValueDate = day,
                            Type = EntryType.Fee,
                            Amount = feeAmt
                        });
                        _db.SaveChanges();
                        closingBal = GetLedgerBalance(acc.AccountId, day);
                    }

                    if (closingBal > 0)
                    {
                        decimal dailyInt = closingBal * 0.0004m;
                        _dailyInterestTracker[acc.AccountId].Add(dailyInt);
                    }
                    else
                    {
                        _dailyInterestTracker[acc.AccountId].Add(0m);
                    }

                    decimal availBal = GetAvailableBalance(acc.AccountId, day);
                    var activeHolds = _db.Holds.Where(h => h.AccountId == acc.AccountId && h.IsActive).Select(h => h.AuthId).ToList();
                    var errors = _db.Errors.Where(e => e.AccountId == acc.AccountId && e.Day == day).Select(e => e.Message).ToList();

                    Console.WriteLine($"Account [{acc.AccountId}] ({acc.Currency})");
                    Console.WriteLine($"  Ledger Balance   : {closingBal:N2}");
                    Console.WriteLine($"  Available Balance: {availBal:N2}");
                    Console.WriteLine($"  Active Holds     : {(activeHolds.Any() ? string.Join(", ", activeHolds) : "None")}");
                    Console.WriteLine($"  Errors           : {(errors.Any() ? string.Join("; ", errors) : "None")}");
                }
            }

            // Capitalize Interest at End of Day 6
            Console.WriteLine("\n--- INTEREST CAPITALIZATION (DAY 6) ---");
            foreach (var kvp in _dailyInterestTracker)
            {
                var acc = _db.Accounts.First(a => a.AccountId == kvp.Key);
                decimal totalRaw = kvp.Value.Sum();
                decimal capitalized = RoundCurrency(totalRaw, acc.Currency);
                Console.WriteLine($"Account [{acc.AccountId}] Capitalized Interest: {capitalized} (from daily sum {totalRaw})");

                if (capitalized > 0)
                {
                    _db.LedgerEntries.Add(new LedgerEntry
                    {
                        EventId = "INT-CAP-D6",
                        AccountId = acc.AccountId,
                        Day = 6,
                        ValueDate = 6,
                        Type = EntryType.Interest,
                        Amount = capitalized
                    });
                }
            }
            _db.SaveChanges();
        }
    }
}
