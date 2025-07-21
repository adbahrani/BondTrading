using System.Collections.Concurrent;
using System.Text.Json;

namespace BondsServer
{
    public class CacheUpdateWorker(
        BlockingCollection<BondWithStatistics> inputQueue,
        BlockingCollection<BondUpdate> outputQueue)
    {
        private const int MAX_BONDS = 500_000;

        string[] latestStatuses = new string[MAX_BONDS];
        int numBonds = 0;
        private readonly object lockObject = new object(); // Better lock object

        public void Run()
        {
            Console.WriteLine(" CacheUpdateWorker started");
            Dictionary<string, int> bondIndicesById = new();
            int totalProcessed = 0;

            foreach (BondWithStatistics bondWithStats in inputQueue.GetConsumingEnumerable())
            {
                string bondId = bondWithStats.Bond.id;
                int index;
                bool alreadyExists;

                // Critical section - lock the entire operation
                lock (lockObject)
                {
                    alreadyExists = bondIndicesById.TryGetValue(bondId, out index);

                    if (!alreadyExists)
                    {
                        // We've not seen this bond before - we need to append it to the end of our array
                        if (numBonds == MAX_BONDS) throw new Exception("Maximum number of bonds exceeded");

                        index = numBonds;
                        bondIndicesById[bondId] = index;
                        numBonds++; // Increment here while we have the lock
                    }

                    // Convert the bond's current status to JSON and store it
                    string serializedStatus = JsonSerializer.Serialize(bondWithStats);
                    latestStatuses[index] = serializedStatus;

                    // Send to output queue
                    outputQueue.Add(new()
                    {
                        bondId = bondId,
                        serializedStatus = serializedStatus,
                    });
                    
                    totalProcessed++;
                    if (totalProcessed % 10000 == 0) // Reduced from 5000 to 10000
                    {
                        Console.WriteLine($"💾 CacheWorker processed {totalProcessed} updates, total bonds: {numBonds}");
                    }
                }
            }
            
            Console.WriteLine("⚠️ CacheUpdateWorker ended - inputQueue completed");
        }

        public Memory<string> GetLatestStatuses()
        {
            lock (lockObject)
            {
                return latestStatuses.AsMemory(0, numBonds);
            }
        }
    }
}