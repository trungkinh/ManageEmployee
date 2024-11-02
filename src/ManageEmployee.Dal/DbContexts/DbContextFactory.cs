using ManageEmployee.Dal.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace ManageEmployee.Dal.DbContexts;

public class DbContextFactory : IDbContextFactory
{
    public ApplicationDbContext GetDbContext(string connectionStr)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        options.UseSqlServer(connectionStr);
        options.AddInterceptors(new UpdateYearInterceptor());
        return new ApplicationDbContext(options.Options);
    }
}