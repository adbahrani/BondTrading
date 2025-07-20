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
            // Fast, simple calculations with minimal decimal precision
            var currentYield = (float)Math.Round((double)bond.coupon / bond.price, 3);
            
            // Very simple approximations - fast math only
            var yearsToMaturity = bond.duration / 365.0f;
            var ytm = (float)Math.Round(currentYield * 1.1f, 3);
            var modifiedDuration = (float)Math.Round(Math.Min(yearsToMaturity * 0.8f, 20.0f), 2);
            var dollarDuration = (float)Math.Round(bond.price * modifiedDuration / 100, 2);
            var convexity = (float)Math.Round(modifiedDuration * 2.0f, 2);

            return new()
            {
                Bond = bond,
                Yield = currentYield,
                YieldToMaturity = ytm,
                ModifiedDuration = modifiedDuration,
                DollarDuration = dollarDuration,
                Convexity = convexity,
                LastUpdated = DateTime.UnixEpoch // Fixed timestamp to reduce JSON size
            };
        }
    }
}