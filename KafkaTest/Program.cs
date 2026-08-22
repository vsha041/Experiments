using Confluent.Kafka;

namespace KafkaTest;

internal class Program
{
    static void Main(string[] args)
    {
        var config = new AdminClientConfig
        {
            BootstrapServers = "localhost:9092"
        };

        using var adminClient = new AdminClientBuilder(config).Build();

        try
        {
            var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));

            Console.WriteLine($"Brokers: {metadata.Brokers.Count}");
            Console.WriteLine();

            foreach (var broker in metadata.Brokers)
            {
                Console.WriteLine(
                    $"Broker: {broker.BrokerId} - {broker.Host}:{broker.Port}");
            }

            Console.WriteLine();
            Console.WriteLine("Topics:");

            foreach (var topic in metadata.Topics.OrderBy(t => t.Topic))
            {
                Console.WriteLine($"Topic: {topic.Topic}");

                foreach (var partition in topic.Partitions)
                {
                    Console.WriteLine(
                        $"   Partition {partition.PartitionId}, Leader {partition.Leader}");
                }
            }
        }
        catch (KafkaException ex)
        {
            Console.WriteLine($"Kafka error: {ex.Error.Reason}");
        }
    }
}