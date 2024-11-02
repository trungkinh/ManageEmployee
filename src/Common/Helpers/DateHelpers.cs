﻿namespace Common.Helpers;

public static class DateHelpers
{
    public static int UnixTimeNow()
    {
        var timeSpan = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0));
        return (int)timeSpan.TotalSeconds;
    }

    /// <summary>
    /// date : dd/MM/yyyy
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public static DateTime ConvertToDate(string date)
    {
        if (string.IsNullOrEmpty(date))
        {
            return new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);
        }
        var dateSplit = date.Split('/');
        if (dateSplit.Length >= 3)
        {
            return new DateTime(Int32.Parse(dateSplit[2]), Int32.Parse(dateSplit[1]), Int32.Parse(dateSplit[0]));
        }
        else
        {
            return new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);
        }
    }

    public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
    {
        // Unix timestamp is seconds past epoch
        System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
        dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
        return dtDateTime;
    }

    public static double DateTimeToUnixTimeStamp(DateTime time)
    {
        System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc).ToLocalTime();
        var epoch = (time - dtDateTime).TotalSeconds;
        return epoch;
    }

    /// <summary>
    /// Determines whether a specific date falls within a given date range.
    /// </summary>
    /// <param name="dateToCheck">The date to be checked.</param>
    /// <param name="startDate">The start date of the range.</param>
    /// <param name="endDate">The end date of the range.</param>
    /// <returns>
    /// <c>true</c> if <paramref name="dateToCheck"/> is within the range inclusive of the start and end dates; 
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method checks if the <paramref name="dateToCheck"/> is greater than or equal to 
    /// <paramref name="startDate"/> and less than or equal to <paramref name="endDate"/>.
    /// </remarks>
    public static bool IsDateBetween(this DateTime dateToCheck, DateTime startDate, DateTime endDate)
    {
        return dateToCheck.Date >= startDate.Date && dateToCheck.Date <= endDate.Date;
    }
}
