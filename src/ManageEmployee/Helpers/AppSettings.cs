namespace ManageEmployee.Helpers;

public class AppSettings
{
    public string? Secret { get; set; }
    public string? TimeKeepBaseUrl { get; set; }
    public string? TimeKeepBaseApi { get; set; }
    public string? UrlHost { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? AccessToken { get; set; }
}
public class AppSettingInvoice
{
    public string? Endpoint { get; set; }
    public string? Url { get; set; }
}
public class SettingDatabase
{
    public string? ConnStr { get; set; }
    public string? DbName { get; set; }
}

public class AppSettingOtherCompany
{
    public string Endpoint { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }
}