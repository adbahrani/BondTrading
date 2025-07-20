using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace BondsServer
{
    public class BatchNotificationWorker
    {
        BlockingCollection<BondUpdate> inputQueue;
        int batchSize;
        Action<string> broadcastToClients;

        public BatchNotificationWorker(BlockingCollection<BondUpdate> inputQueue, int batchSize, Action<string> broadcastToClients)
        {
            this.inputQueue = inputQueue;
            this.batchSize = batchSize;
            this.broadcastToClients = broadcastToClients;
        }

        public void Run()
        {
            StringBuilder sb = new StringBuilder();
            List<string> currentBatch = new List<string>(batchSize);
            Dictionary<string, int> bondIndicesById = new();
            int numUpdatesProcessed = 0;

            foreach (BondUpdate update in inputQueue.GetConsumingEnumerable())
            {
                // Either add this bond to our batch, or update it, if we've already seen it
                int index;
                bool alreadyExists = bondIndicesById.TryGetValue(update.bondId, out index);

                if (alreadyExists)
                {
                    currentBatch[index] = update.serializedStatus;
                }
                else
                {
                    bondIndicesById[update.bondId] = currentBatch.Count;
                    currentBatch.Add(update.serializedStatus);
                }

                ++numUpdatesProcessed;

                if (numUpdatesProcessed == batchSize)
                {
                    // Format the entire batch into a string, ready for sending
                    foreach (string bondStatus in currentBatch)
                    {
                        sb.Append(bondStatus);
                        sb.Append('\n');
                    }
                    string message = sb.ToString();

                    // Send the message
                    broadcastToClients(message);

                    // Reset for the next batch
                    sb.Clear();
                    currentBatch.Clear();
                    bondIndicesById.Clear();
                    numUpdatesProcessed = 0;
                }
            }
        }
    }
}
