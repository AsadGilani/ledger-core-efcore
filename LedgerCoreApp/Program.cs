namespace LedgerCoreApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            using var context = new LedgerDbContext();
            var service = new LedgerService(context);
            service.RunSimulation();
        }
    }
}
