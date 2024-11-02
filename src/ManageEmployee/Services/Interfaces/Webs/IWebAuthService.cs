using System.Security.Claims;
using ManageEmployee.DataTransferObject.Web;
using ManageEmployee.Entities.CustomerEntities;

namespace ManageEmployee.Services.Interfaces.Webs;

/// <summary>
/// Interface
/// </summary>
public interface IWebAuthService
{
    Task<Customer> Authenticate(string username, string password);
    string GenerateToken(List<Claim> authClaims);
    Task<Customer> Register(WebCustomerV2Model model);
    Task UpdateMail(WebCustomerV2Model model);
}
