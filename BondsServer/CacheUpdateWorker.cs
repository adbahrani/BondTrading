using System.Collections.Concurrent;
using System.Text.Json;

namespace BondsServer
{
    public class CacheUpdateWorker
    {
        const int MAX_BONDS = 1_000_000;

        BlockingCollection<BondWithStatistics> inputQueue;
        BlockingCollection<BondUpdate> outputQueue;

        string[] latestStatuses = new string[MAX_BONDS];
        int numBonds = 0;

        public CacheUpdateWorker(BlockingCollection<BondWithStatistics> inputQueue, BlockingCollection<BondUpdate> outputQueue)
        {
            this.inputQueue = inputQueue;
            this.outputQueue = outputQueue;
        }

        public void Run()
        {
            Dictionary<string, int> bondIndicesById = new();

            foreach (BondWithStatistics bondWithStats in inputQueue.GetConsumingEnumerable())
            {
                string bondId = bondWithStats.Bond.id;
                int index;
                bool alreadyExists = bondIndicesById.TryGetValue(bondId, out index);

                if (!alreadyExists)
                {
                    // We've not seen this bond before - we need to append it to the end of our array, and assign it a new index
                    if (numBonds == MAX_BONDS) throw new Exception("Maximum number of bonds exceeded");

                    index = numBonds;
                    bondIndicesById[bondId] = index;
                }

                // Convert the bond's current status to JSON
                string serializedStatus = JsonSerializer.Serialize(bondWithStats);
                latestStatuses[index] = serializedStatus;

                if (!alreadyExists)
                {
                    lock (latestStatuses)
                    {
                        numBonds++;
                    }
                }

                outputQueue.Add(new()
                {
                    bondId = bondId,
                    serializedStatus = serializedStatus,
                });
            }
        }

        public Memory<string> GetLatestStatuses()
        {
            lock (latestStatuses)
            {
                return latestStatuses.AsMemory(0, numBonds);
            }
        }
    }
}