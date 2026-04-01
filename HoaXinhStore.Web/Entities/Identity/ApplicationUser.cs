using Microsoft.AspNetCore.Identity;

namespace HoaXinhStore.Web.Entities.Identity;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
}
