using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ManageEmployee.Services.Interfaces.Webs;
using ManageEmployee.DataTransferObject;
using ManageEmployee.DataTransferObject.Web;

namespace ManageEmployee.Controllers.Web;

[ApiController]
[Route("api/[controller]")]
public class WebAuthController : ControllerBase
{
    private readonly IWebAuthService _webAuthService;

    public WebAuthController(
        IWebAuthService webAuthService)
    {
        _webAuthService = webAuthService;
    }

    /// <summary>
    /// Đăng nhập web
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPost("login")]
    public async Task<IActionResult> Login(AuthenticateModel model)
    {
        var user = await _webAuthService.Authenticate(model.Username, model.Password);
        
        var authClaims = new List<Claim>
                {
                    new Claim("UserId", user.Id.ToString()),
                    new Claim("UserName", user.Phone),
                    new Claim("Name",  !String.IsNullOrEmpty(user.Name)? user.Name : ""),
                };

        var tokenString = _webAuthService.GenerateToken(authClaims);

        return Ok(new
        {
            Id = user.Id,
            Username = user.Code,
            Fullname = user.Name,
            Avatar = user.Avatar,
            Token = tokenString,
            Email = user.Email,
            Phone = user.Phone,
        });
    }

    /// <summary>
    /// Đăng ký tài khoản
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPost("register")]
    public async Task<IActionResult> Register(WebCustomerV2Model model)
    {
        var user = await _webAuthService.Register(model);
        var authClaims = new List<Claim>
                {
                    new Claim("UserId", user.Id.ToString()),
                    new Claim("UserName", user.Phone),
                    new Claim("Name",  !String.IsNullOrEmpty(user.Name)? user.Name : ""),
                };

        var tokenString = _webAuthService.GenerateToken(authClaims);

        return Ok(new ObjectReturn
        {
            status = 200,
            data = new
            {
                Id = user.Id,
                Code = user.Code,
                Name = user.Name,
                Avatar = user.Avatar,
                Phone = user.Phone,
                Token = tokenString,
            }
        });
    }

    /// <summary>
    /// Update email
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPost("update-email")]
    public async Task<IActionResult> UpdateEmail(WebCustomerV2Model model)
    {
        await _webAuthService.UpdateMail(model);
        return Ok();
    }
}