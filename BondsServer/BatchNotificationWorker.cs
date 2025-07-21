using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Net.WebSockets;

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
            Console.WriteLine(" BatchNotificationWorker started");
            StringBuilder sb = new StringBuilder();
            List<string> currentBatch = new List<string>(batchSize);
            Dictionary<string, int> bondIndicesById = new();
            int numUpdatesProcessed = 0;
            int totalBatchesSent = 0;

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

                // Debug: Log every 5000 updates received (instead of 1000)
                if (numUpdatesProcessed % 5000 == 0)
                {
                    Console.WriteLine($"📥 BatchWorker received {numUpdatesProcessed} updates, current batch size: {currentBatch.Count}");
                }

                if (numUpdatesProcessed == batchSize)
                {
                    // Format the entire batch into a string, ready for sending
                    foreach (string bondStatus in currentBatch)
                    {
                        sb.Append(bondStatus);
                        sb.Append('\n');
                    }
                    string message = sb.ToString();

                    totalBatchesSent++;
                    
                    // Only log every 100th batch (instead of every batch)
                    if (totalBatchesSent % 100 == 0)
                    {
                        Console.WriteLine($"📤 Sent 100 batches (last: #{totalBatchesSent} with {currentBatch.Count} bonds, {message.Length} bytes)");
                    }

                    // Send the message - with error handling
                    try
                    {
                        broadcastToClients(message);
                        // Remove the success log - too spammy
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error broadcasting batch #{totalBatchesSent}: {ex.Message}");
                        // Continue processing even if broadcast fails
                    }

                    // Reset for the next batch
                    sb.Clear();
                    currentBatch.Clear();
                    bondIndicesById.Clear();
                    numUpdatesProcessed = 0;
                }
            }
            
            Console.WriteLine("⚠️ BatchNotificationWorker ended - inputQueue completed");
        }
    }
}