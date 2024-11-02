namespace Common.Helpers;

public static class CommonHelper
{
    public static T GetMinValue<T>(params T[] items)
    {
        return items.Min() ?? throw new InvalidOperationException("Items is null or empty.");
    }
    
    public static T GetMaxValue<T>(params T[] items)
    {
        return items.Max() ?? throw new InvalidOperationException("Items is null or empty.");
    }
    
    public static bool IsNumberBetween<T>(this T numberToCheck, T numberFrom, T numberTo) where T : IComparable<T>
    {
        return numberToCheck.CompareTo(numberFrom) >= 0 && numberToCheck.CompareTo(numberTo) <= 0;
    }

    public static decimal RoundToThousand(this decimal amount)
    {
        return Math.Round(amount / 1000) * 1000;
    }
    
    public static double RoundToThousand(this double amount)
    {
        return Math.Round(amount / 1000) * 1000;
    }
}