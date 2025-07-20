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
            // Generate simple bonds
            List<Bond> bonds = GenerateBonds();

            // Output initial bond prices
            foreach (Bond bond in bonds)
            {
                outputQueue.Add(bond);
            }

            Console.WriteLine($"Generated {bonds.Count} bonds");
            Console.WriteLine($"BB Junk Bonds: {bonds.Count(b => b.id.StartsWith("BB-"))}");

            // IMPORTANT: Let the system stabilize before starting updates
            Console.WriteLine("Waiting 3 seconds for system to stabilize...");
            Thread.Sleep(3000);

            int totalUpdates = 0;
            long startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Update loop - steady 1k/sec rate
            while (true)
            {
                for (int i = 0; i < 10; ++i) // 10 updates per cycle
                {
                    int idx;

                    // 50% chance to pick a BB bond if available
                    if (Random.Shared.NextDouble() < 0.5)
                    {
                        // Try to find a BB bond
                        for (int attempt = 0; attempt < 10; attempt++)
                        {
                            int testIdx = Random.Shared.Next() % numBonds;
                            if (bonds[testIdx].id.StartsWith("BB-"))
                            {
                                idx = testIdx;
                                goto UpdateBond;
                            }
                        }
                    }

                    // Default: random selection
                    idx = Random.Shared.Next() % numBonds;

                    UpdateBond:
                    // Simple price change based on bond type
                    var priceChange = GetPriceChange(bonds[idx]);
                    bonds[idx].price += priceChange;
                    bonds[idx].price = Math.Max(500, Math.Min(1500, bonds[idx].price));

                    outputQueue.Add(bonds[idx]);

                    ++totalUpdates;
                    if (totalUpdates % 10000 == 0)
                    {
                        float rate = 1000f * totalUpdates / (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime);
                        Console.WriteLine($"Update rate: {rate:F1}/sec");
                    }
                }
                Thread.Sleep(5); // 10 updates every 5ms = 2,000/sec
            }
        }

        private List<Bond> GenerateBonds()
        {
            var bonds = new List<Bond>(numBonds);
            var random = new Random();
            
            // Keep track of counters for clean ID generation
            int bbCounter = 0;
            int ustCounter = 0;
            int aaplCounter = 0;
            int msftCounter = 0;
            int googlCounter = 0;

            for (int i = 0; i < numBonds; i++)
            {
                string bondId;
                int coupon;
                int duration;

                if (i < numBonds * 0.01) // First 1% are BB junk bonds (5k bonds)
                {
                    bondId = $"BB-{bbCounter++:D6}";
                    coupon = random.Next(8, 15); // High coupon for junk
                    duration = random.Next(365, 1825); // 1-5 years
                }
                else if (i < numBonds * 0.21) // Next 20% are Treasury (100k bonds)
                {
                    bondId = $"UST-{ustCounter++:D6}";
                    coupon = random.Next(2, 5); // Low coupon for Treasury
                    duration = random.Next(365, 10950); // 1-30 years
                }
                else // Remaining 79% are corporate - only the 3 types we filter for
                {
                    var corpType = random.Next(3);
                    switch (corpType)
                    {
                        case 0:
                            bondId = $"AAPL-{aaplCounter++:D6}";
                            break;
                        case 1:
                            bondId = $"MSFT-{msftCounter++:D6}";
                            break;
                        case 2:
                            bondId = $"GOOGL-{googlCounter++:D6}";
                            break;
                        default:
                            bondId = $"AAPL-{aaplCounter++:D6}";
                            break;
                    }
                    coupon = random.Next(4, 8); // Medium coupon for corporate
                    duration = random.Next(365, 3650); // 1-10 years
                }

                // Simple price calculation
                var basePrice = 1000 + (coupon - 6) * 20 + random.Next(-100, 101);
                var price = Math.Max(500, Math.Min(1500, basePrice));

                bonds.Add(new Bond
                {
                    id = bondId,
                    coupon = coupon,
                    duration = duration,
                    faceValue = 100,
                    price = price
                });
            }

            return bonds;
        }

        private int GetPriceChange(Bond bond)
        {
            // Simple volatility based on bond type
            if (bond.id.StartsWith("BB-")) return Random.Shared.Next(-50, 51); // High volatility junk
            if (bond.id.StartsWith("UST-")) return Random.Shared.Next(-5, 6);   // Low volatility Treasury
            return Random.Shared.Next(-15, 16); // Medium volatility corporate
        }
    }
}