using Microsoft.EntityFrameworkCore;

namespace LedgerCoreApp
{
    public class LedgerDbContext : DbContext
    {
        public DbSet<Account> Accounts => Set<Account>();
        public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
        public DbSet<AuthorizationHold> Holds => Set<AuthorizationHold>();
        public DbSet<AuditError> Errors => Set<AuditError>();
        public DbSet<FeeAssessment> FeesAssessed => Set<FeeAssessment>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase("LedgerCoreDb");
        }
    }
}
