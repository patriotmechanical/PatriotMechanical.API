using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PatriotMechanical.API.Application.Services;

namespace PatriotMechanical.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("pricing")]
    public class PricingController : ControllerBase
    {
        private readonly PricingEngine _pricing;

        public PricingController(PricingEngine pricing)
        {
            _pricing = pricing;
        }

        [HttpGet("calculate")]
        public IActionResult Calculate(decimal cost)
        {
            var multiplier = _pricing.CalculateMarkupMultiplier(cost);
            var basePrice = _pricing.CalculateBasePrice(cost);
            var finalPrice = _pricing.CalculateFinalPrice(cost);

            return Ok(new
            {
                cost,
                multiplier,
                basePrice,
                finalPrice
            });
        }
    }
}