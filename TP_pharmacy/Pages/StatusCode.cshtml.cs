using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TP_pharmacy.Pages;

[AllowAnonymous]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class StatusCodeModel : PageModel
{
    public int Code { get; private set; }
    public void OnGet(int code)
    {
        Code = code is >= 400 and <= 599 ? code : 404;
        Response.StatusCode = Code;
    }
}
