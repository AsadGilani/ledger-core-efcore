using LedgerCoreApp;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace LedgerCoreApp.Tests;

[TestFixture]
public class LedgerServiceTests
{
    private LedgerDbContext _db = null!;
    private LedgerService _service = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new LedgerDbContext(options);
        _db.Accounts.Add(new Account { AccountId = "ACC-001", Currency = Currency.AED });
        _db.Accounts.Add(new Account { AccountId = "ACC-002", Currency = Currency.BHD });
        _db.SaveChanges();
        _service = new LedgerService(_db);
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
    }

    [Test]
    public void RoundCurrency_UsesCurrencyPrecisionAndRoundsMidpointsAwayFromZero()
    {
        Assert.That(_service.RoundCurrency(10.125m, Currency.AED), Is.EqualTo(10.13m));
        Assert.That(_service.RoundCurrency(10.1235m, Currency.BHD), Is.EqualTo(10.124m));
    }

    [Test]
    public void GetLedgerBalance_AppliesEntrySignsAndValueDateFilter()
    {
        _db.LedgerEntries.AddRange(
            Entry("credit", EntryType.Credit, 100m, 1),
            Entry("interest", EntryType.Interest, 2m, 2),
            Entry("reversal", EntryType.ReversalCredit, 4m, 3),
            Entry("debit", EntryType.Debit, 10m, 4),
            Entry("fee", EntryType.Fee, 3m, 5),
            Entry("settlement", EntryType.Settlement, 5m, 5),
            Entry("future", EntryType.Credit, 90m, 6));
        _db.SaveChanges();

        var balance = _service.GetLedgerBalance("ACC-001", upToDay: 5);

        Assert.That(balance, Is.EqualTo(88m));
    }

    [Test]
    public void GetAvailableBalance_SubtractsOnlyActiveHolds()
    {
        _db.LedgerEntries.Add(Entry("credit", EntryType.Credit, 150m, 1));
        _db.Holds.AddRange(
            new AuthorizationHold { AuthId = "active", AccountId = "ACC-001", Amount = 25m, IsActive = true },
            new AuthorizationHold { AuthId = "inactive", AccountId = "ACC-001", Amount = 50m, IsActive = false });
        _db.SaveChanges();

        var balance = _service.GetAvailableBalance("ACC-001", upToDay: 1);

        Assert.That(balance, Is.EqualTo(125m));
    }

    [Test]
    public void ProcessEvent_CreditAndDebit_AddRoundedLedgerEntries()
    {
        _service.ProcessEvent("credit", 1, "ACC-001", "CREDIT", 10.126m, 1);
        _service.ProcessEvent("debit", 2, "ACC-001", "DEBIT", 2.345m, 2);

        var entries = _db.LedgerEntries.OrderBy(entry => entry.EventId).ToList();

        Assert.That(entries, Has.Count.EqualTo(2));
        Assert.That(entries[0].Type, Is.EqualTo(EntryType.Credit));
        Assert.That(entries[0].Amount, Is.EqualTo(10.13m));
        Assert.That(entries[1].Type, Is.EqualTo(EntryType.Debit));
        Assert.That(entries[1].Amount, Is.EqualTo(2.35m));
    }

    [Test]
    public void ProcessEvent_AuthorizationCreatesHoldOrDeclineErrorBasedOnAvailableBalance()
    {
        _db.LedgerEntries.Add(Entry("credit", EntryType.Credit, 100m, 1));
        _db.SaveChanges();

        _service.ProcessEvent("auth-approved", 2, "ACC-001", "AUTHORIZATION", 40m, 2, authId: "Auth-A");
        _service.ProcessEvent("auth-declined", 3, "ACC-001", "AUTHORIZATION", 70m, 3, authId: "Auth-B");

        Assert.That(_db.Holds.Single().AuthId, Is.EqualTo("Auth-A"));
        Assert.That(_db.Holds.Single().Amount, Is.EqualTo(40m));
        Assert.That(_db.Errors.Single().EventId, Is.EqualTo("auth-declined"));
        Assert.That(_db.Errors.Single().Message, Does.Contain("insufficient available balance"));
    }

    [Test]
    public void ProcessEvent_SettlementDeactivatesMatchingHoldAndAddsLedgerEntry()
    {
        var hold = new AuthorizationHold
        {
            AuthId = "Auth-A",
            AccountId = "ACC-001",
            Amount = 20m,
            IsActive = true
        };
        _db.Holds.Add(hold);
        _db.SaveChanges();

        _service.ProcessEvent("settlement", 2, "ACC-001", "SETTLEMENT", 18m, 2, authId: "Auth-A");

        Assert.That(hold.IsActive, Is.False);
        Assert.That(_db.LedgerEntries.Single().Type, Is.EqualTo(EntryType.Settlement));
        Assert.That(_db.LedgerEntries.Single().Amount, Is.EqualTo(18m));
    }

    [Test]
    public void ProcessEvent_ReversalOfDebitCreatesReversalCredit()
    {
        _db.LedgerEntries.Add(Entry("original", EntryType.Debit, 30m, 1));
        _db.SaveChanges();

        _service.ProcessEvent("reversal", 2, "ACC-001", "REVERSAL", 0m, 2, targetId: "original");

        var reversal = _db.LedgerEntries.Single(entry => entry.EventId == "reversal");
        Assert.That(reversal.Type, Is.EqualTo(EntryType.ReversalCredit));
        Assert.That(reversal.Amount, Is.EqualTo(30m));
        Assert.That(_service.GetLedgerBalance("ACC-001", upToDay: 2), Is.EqualTo(0m));
    }

    [Test]
    public void ProcessEvent_CreditInstallmentsSplitsRemainderAcrossLastInstallment()
    {
        _service.ProcessEvent("installment", 1, "ACC-002", "CREDIT_INSTALLMENTS", 10m, 1);

        var entries = _db.LedgerEntries.OrderBy(entry => entry.EventId).ToList();

        Assert.That(entries.Select(entry => entry.EventId), Is.EqualTo(new[]
        {
            "installment-part1",
            "installment-part2",
            "installment-part3"
        }));
        Assert.That(entries.Select(entry => entry.Amount), Is.EqualTo(new[] { 3.333m, 3.333m, 3.334m }));
        Assert.That(entries.All(entry => entry.Type == EntryType.Credit), Is.True);
    }

    private static LedgerEntry Entry(string eventId, EntryType type, decimal amount, int valueDate)
    {
        return new LedgerEntry
        {
            EventId = eventId,
            AccountId = "ACC-001",
            Type = type,
            Amount = amount,
            ValueDate = valueDate
        };
    }
}
