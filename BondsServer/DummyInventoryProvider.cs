using System.Collections.Concurrent;

namespace BondsServer
{
    public class DummyInventoryProvider
    {
        int numBonds;
        BlockingCollection<Bond> outputQueue;

        public DummyInventoryProvider(int numBonds, BlockingCollection<Bond> outputQueue)
        {
            this.numBonds = numBonds;
            this.outputQueue = outputQueue;
        }

        public void Run()
        {
            // Generate dummy bonds
            List<Bond> dummyBonds = new List<Bond>(numBonds);
            for (int i = 0; i < numBonds; i++)
            {
                dummyBonds.Add(new()
                {
                    id = "Bond_" + i,
                    coupon = 100,
                    duration = 100,
                    faceValue = 100,
                    price = 1000,
                });
            }

            // Output initial bond prices
            foreach (Bond bond in dummyBonds)
            {
                outputQueue.Add(bond);
            }

            int totalUpdates = 0;
            long startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            while (true)
            {
                for (int i= 0; i<30; ++i)
                {
                    int idx = Random.Shared.Next() % numBonds;
                    dummyBonds[idx].price += (Random.Shared.Next() % 50) - 25;
                    outputQueue.Add(dummyBonds[idx]);

                    ++totalUpdates;
                    if (totalUpdates % 5000 == 0)
                    {
                        float rate = 1000f * totalUpdates / (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime);
                        Console.WriteLine($"Update rate: {rate}/sec");
                    }
                }
                Thread.Sleep(5);
            }
        }
    }
}
