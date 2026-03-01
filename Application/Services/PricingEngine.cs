namespace PatriotMechanical.API.Application.Services
{
    public class PricingEngine
    {
        private const decimal CreditCardPercent = 0.025m;

        public decimal CalculateMarkupMultiplier(decimal cost)
        {
            if (cost < 5) return 8m;
            if (cost < 10) return 6m;
            if (cost < 50) return 4m;
            if (cost < 100) return 2.5m;
            return 1.75m;
        }

        public decimal CalculateBasePrice(decimal cost)
        {
            var multiplier = CalculateMarkupMultiplier(cost);
            return cost * multiplier;
        }

        public decimal ApplyCreditCardAdjustment(decimal basePrice)
        {
            return basePrice * (1 + CreditCardPercent);
        }

        public decimal CalculateFinalPrice(decimal cost)
        {
            var basePrice = CalculateBasePrice(cost);
            return ApplyCreditCardAdjustment(basePrice);
        }
    }
}