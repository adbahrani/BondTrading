using System.Collections.Concurrent;

namespace BondsServer
{
    public class DummyInventoryProvider
    {
        int numBonds;
        BlockingCollection<Bond> outputQueue;
        private static readonly string[] Issuers = {
            "AAPL", "MSFT", "GOOGL", "AMZN", "TSLA", "META", "NVDA", "JPM", "JNJ", "V",
            "WMT", "UNH", "PG", "HD", "DIS", "MA", "BAC", "ADBE", "CRM", "NFLX",
            "PYPL", "INTC", "CMCSA", "PEP", "ABT", "TMO", "COST", "AVGO", "ACN", "MRK",
            "US_TREASURY", "CORP_AAA", "CORP_AA", "CORP_A", "CORP_BBB", "CORP_BB",
            "MUNI_NY", "MUNI_CA", "MUNI_TX", "MUNI_FL"
        };
        
        private static readonly Dictionary<string, (int minCoupon, int maxCoupon, int minDuration, int maxDuration)> IssuerProfiles = new()
        {
            ["US_TREASURY"] = (1, 4, 30, 10950), // 30 days to 30 years
            ["CORP_AAA"] = (2, 6, 365, 3650), // 1 to 10 years
            ["CORP_AA"] = (3, 7, 365, 3650),
            ["CORP_A"] = (4, 8, 365, 2920), // 1 to 8 years
            ["CORP_BBB"] = (5, 10, 365, 2555), // 1 to 7 years
            ["CORP_BB"] = (7, 12, 365, 1825), // 1 to 5 years
        };

        public DummyInventoryProvider(int numBonds, BlockingCollection<Bond> outputQueue)
        {
            this.numBonds = numBonds;
            this.outputQueue = outputQueue;
        }

        public void Run()
        {
            // Generate realistic bonds
            List<Bond> bonds = GenerateRealisticBonds();

            // Output initial bond prices
            foreach (Bond bond in bonds)
            {
                outputQueue.Add(bond);
            }

            int totalUpdates = 0;
            long startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            while (true)
            {
                for (int i = 0; i < 30; ++i)
                {
                    int idx = Random.Shared.Next() % numBonds;
                    
                    // More realistic price movements based on bond type
                    var priceChange = CalculateRealisticPriceChange(bonds[idx]);
                    bonds[idx].price += priceChange;
                    
                    // Keep prices within reasonable bounds
                    bonds[idx].price = Math.Max(500, Math.Min(1500, bonds[idx].price));
                    
                    outputQueue.Add(bonds[idx]);

                    ++totalUpdates;
                    if (totalUpdates % 5000 == 0)
                    {
                        float rate = 1000f * totalUpdates / (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime);
                        Console.WriteLine($"Update rate: {rate:F1}/sec");
                    }
                }
                Thread.Sleep(5);
            }
        }

        private List<Bond> GenerateRealisticBonds()
        {
            var bonds = new List<Bond>(numBonds);
            var random = new Random();

            for (int i = 0; i < numBonds; i++)
            {
                var issuer = Issuers[random.Next(Issuers.Length)];
                var bondType = DetermineBondType(issuer);
                var profile = GetIssuerProfile(bondType);
                
                var coupon = random.Next(profile.minCoupon, profile.maxCoupon + 1);
                var duration = random.Next(profile.minDuration, profile.maxDuration + 1);
                
                // Price starts near par (1000) but varies based on coupon and market conditions
                var basePrice = 1000;
                var couponAdjustment = (coupon - 5) * 10; // Higher coupon = higher price
                var priceNoise = random.Next(-50, 51); // Random market movement
                var initialPrice = basePrice + couponAdjustment + priceNoise;
                
                bonds.Add(new Bond
                {
                    id = GenerateBondId(issuer, i),
                    coupon = coupon,
                    duration = duration,
                    faceValue = 100, // Standard face value
                    price = Math.Max(500, Math.Min(1500, initialPrice)), // Keep within bounds
                });
            }

            return bonds;
        }

        private string DetermineBondType(string issuer)
        {
            if (issuer == "US_TREASURY") return "US_TREASURY";
            if (issuer.StartsWith("CORP_")) return issuer;
            if (issuer.StartsWith("MUNI_")) return "MUNI";
            
            // For corporate names, assign random credit rating
            var ratings = new[] { "CORP_AAA", "CORP_AA", "CORP_A", "CORP_BBB", "CORP_BB" };
            var weights = new[] { 0.05, 0.15, 0.25, 0.35, 0.20 }; // Realistic distribution
            
            var random = Random.Shared.NextDouble();
            var cumulative = 0.0;
            
            for (int i = 0; i < ratings.Length; i++)
            {
                cumulative += weights[i];
                if (random <= cumulative)
                    return ratings[i];
            }
            
            return "CORP_BBB"; // Default
        }

        private (int minCoupon, int maxCoupon, int minDuration, int maxDuration) GetIssuerProfile(string bondType)
        {
            if (IssuerProfiles.TryGetValue(bondType, out var profile))
                return profile;
                
            // Default for municipal or unknown
            return (3, 8, 365, 7300); // 1 to 20 years
        }

        private string GenerateBondId(string issuer, int index)
        {
            if (issuer == "US_TREASURY")
                return $"UST-{index:D6}";
            if (issuer.StartsWith("CORP_"))
                return $"{issuer.Replace("CORP_", "")}-{index:D6}";
            if (issuer.StartsWith("MUNI_"))
                return $"{issuer}-{index:D6}";
                
            return $"{issuer}-{index:D6}";
        }

        private int CalculateRealisticPriceChange(Bond bond)
        {
            var issuerType = DetermineBondType(bond.id.Split('-')[0]);
            
            // Different volatility for different bond types
            var volatility = issuerType switch
            {
                "US_TREASURY" => 5,   // Very low volatility
                "CORP_AAA" => 8,      // Low volatility  
                "CORP_AA" => 12,      // Low-medium volatility
                "CORP_A" => 18,       // Medium volatility
                "CORP_BBB" => 25,     // Medium-high volatility
                "CORP_BB" => 35,      // High volatility
                _ => 20               // Default medium volatility
            };
            
            return Random.Shared.Next(-volatility, volatility + 1);
        }
    }
}