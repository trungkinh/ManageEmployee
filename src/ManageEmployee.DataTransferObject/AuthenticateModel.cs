using System.ComponentModel.DataAnnotations;

namespace ManageEmployee.DataTransferObject;

public class AuthenticateModel
{
    [Required]
    public string? Username { get; set; }

    [Required]
    public string? Password { get; set; }
}