using ManageEmployee.Entities.Enumerations;

namespace ManageEmployee.Helpers;

public static class StringHelpers
{
    public static string GetStringWithMaxLength(string input, int maxLength)
    {
        try
        {
            if(!string.IsNullOrEmpty(input) && input.Length > maxLength)
            {
                    return input.Substring(0, maxLength) + " ... ";
            }
            return input;
        }
        catch
        {
            return input;
        }
    }
    public static string GenderVI(GenderEnum gender)
    {
        switch (gender)
        {
            case GenderEnum.All:
                return "Tất cả";
            case GenderEnum.Male:
                return "Nam";
            case GenderEnum.Female:
                return "Nữ";
            case GenderEnum.Other:
                return "Khác";
            default:
                return "";
        }
    }
}
