

using Microsoft.AspNetCore.Mvc;
using Server.Dtos;
using Server.Entities;

namespace Server.Contracts;
public interface ICustomer
{
    Task<ActionResult> RegisterCustomer(Customers customer);
    Task<IActionResult> EmailVerification(string? email, string? code);
    Task<ActionResult<ResponseDto>> Login(LoginDto login);
    Task SendEmailAsync(MailRequest mailRequest);

}
