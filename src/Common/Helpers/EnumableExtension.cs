using System.Linq.Expressions;

namespace Common.Helpers;

public static class EnumableExtension
{
    public static IEnumerable<T> WhereIf<T>(this IEnumerable<T> source, bool condition, Func<T, bool> predicate)
    {
        if (condition)
        {
            source = source.Where(predicate);
        }
        return source;
    }
}
public static class QueryableExtension
{
    public static IQueryable<T> WhereIf<T>(this IQueryable<T> source, bool condition, Expression<Func<T, bool>> predicate)
    {
        if (condition)
        {
            source = source.Where(predicate);
        }
        return source;
    }
}

