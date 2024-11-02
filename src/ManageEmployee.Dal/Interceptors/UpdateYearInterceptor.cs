using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Diagnostics;

namespace ManageEmployee.Dal.Interceptors;

public class UpdateYearInterceptor : SaveChangesInterceptor
{
    private const string YearProperty = "Year";

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            UpdateEntities(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void UpdateEntities(DbContext context)
    {
        try
        {
            var entities = context.ChangeTracker
            .Entries()
            .Where(x => x.State == EntityState.Added
                     || x.State == EntityState.Modified)
            .ToList();

            foreach (EntityEntry entry in entities)
            {
                var yearProperty = entry.Properties.FirstOrDefault(x => x.Metadata.Name == YearProperty);
                if (yearProperty is not null
                   && (yearProperty.Metadata.ClrType == typeof(int)
                    || yearProperty.Metadata.ClrType == typeof(int?))
                   && int.Parse(yearProperty.CurrentValue.ToString()) == 0)
                {
                    yearProperty.CurrentValue = DateTime.UtcNow.Year;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.Write(ex);
        }
    }
}
