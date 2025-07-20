using System.Collections.Concurrent;

namespace BondsServer
{
    public class StatCalculationWorker
    {
        BlockingCollection<Bond> inputQueue;
        BlockingCollection<BondWithStatistics> outputQueue;

        public StatCalculationWorker(BlockingCollection<Bond> inputQueue, BlockingCollection<BondWithStatistics> outputQueue)
        {
            this.inputQueue = inputQueue;
            this.outputQueue = outputQueue;
        }

        public void Run()
        {
            foreach (Bond bond in inputQueue.GetConsumingEnumerable())
            {
                BondWithStatistics result = CalculateStats(bond);
                outputQueue.Add(result);
            }
        }

        private BondWithStatistics CalculateStats(Bond bond)
        {
            // Basic current yield
            var currentYield = (float)bond.coupon / (float)bond.price;
            
            // Calculate Yield to Maturity (approximation)
            var ytm = CalculateYieldToMaturity(bond);
            
            // Calculate Modified Duration (approximation)  
            var modifiedDuration = CalculateModifiedDuration(bond, ytm);
            
            // Calculate Dollar Duration
            var dollarDuration = (bond.price * modifiedDuration) / 100;
            
            // Calculate Convexity (approximation)
            var convexity = CalculateConvexity(bond, ytm);

            return new()
            {
                Bond = bond,
                Yield = currentYield,
                YieldToMaturity = ytm,
                ModifiedDuration = modifiedDuration,
                DollarDuration = dollarDuration,
                Convexity = convexity,
                LastUpdated = DateTime.UtcNow
            };
        }

        private float CalculateYieldToMaturity(Bond bond)
        {
            // Simplified YTM calculation using approximation formula
            // For a more accurate calculation, you'd use iterative methods like Newton-Raphson
            
            var yearsToMaturity = bond.duration / 365.0f; // Convert days to years
            var faceValue = bond.faceValue;
            var currentPrice = bond.price;
            var annualCoupon = bond.coupon;
            
            if (yearsToMaturity <= 0) return 0;
            
            // Approximation formula for YTM
            var numerator = annualCoupon + ((faceValue - currentPrice) / yearsToMaturity);
            var denominator = (faceValue + currentPrice) / 2;
            
            return numerator / denominator;
        }

        private float CalculateModifiedDuration(Bond bond, float ytm)
        {
            // Modified Duration = Macaulay Duration / (1 + YTM)
            var macaulayDuration = CalculateMacaulayDuration(bond, ytm);
            
            if (ytm == 0) return macaulayDuration; // Avoid division by zero
            
            return macaulayDuration / (1 + ytm);
        }

        private float CalculateMacaulayDuration(Bond bond, float ytm)
        {
            // Simplified Macaulay Duration calculation
            var yearsToMaturity = bond.duration / 365.0f;
            var couponRate = (float)bond.coupon / bond.faceValue;
            
            if (couponRate == 0) // Zero-coupon bond
            {
                return yearsToMaturity;
            }
            
            // For bonds with regular coupon payments (simplified calculation)
            // This is an approximation - real calculation requires summing present values of all cash flows
            var approximateDuration = yearsToMaturity * 0.75f + 0.25f; // Rough approximation
            
            return Math.Max(0.1f, Math.Min(approximateDuration, yearsToMaturity));
        }

        private float CalculateConvexity(Bond bond, float ytm)
        {
            // Simplified convexity calculation
            var yearsToMaturity = bond.duration / 365.0f;
            var modifiedDuration = CalculateModifiedDuration(bond, ytm);
            
            // Approximation: Convexity ≈ (Modified Duration)² + Modified Duration + (Years to Maturity / 12)
            var convexity = (modifiedDuration * modifiedDuration) + modifiedDuration + (yearsToMaturity / 12);
            
            return convexity;
        }
    }
}

 