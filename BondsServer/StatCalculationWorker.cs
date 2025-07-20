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
            return new()
            {
                id = bond.id,
                coupon = bond.coupon,
                duration = bond.duration,
                faceValue = bond.faceValue,
                price = bond.price,
                yield = (float)bond.coupon / (float)bond.price
            };
        }
    }
}
