using System.Security.Claims;
using PatriotMechanical.API.Application.Services;

namespace PatriotMechanical.API.Controllers
{
    public static class DemoFilter
    {
        public static bool IsDemo(ClaimsPrincipal user)
        {
            return user.FindFirst("isDemo")?.Value == "true";
        }

        public static readonly Guid DemoCompanyId = DemoSeeder.DemoCompanyId;
    }
}