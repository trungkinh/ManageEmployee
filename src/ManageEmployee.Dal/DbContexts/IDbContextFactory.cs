namespace ManageEmployee.Dal.DbContexts;

public interface IDbContextFactory
{
    ApplicationDbContext GetDbContext(string connectionStr);
}