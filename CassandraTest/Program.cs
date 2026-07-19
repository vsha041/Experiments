using System.Net;
using Cassandra;

namespace CassandraTest;

public class Program
{
    public static async Task Main(string[] args)
    {
        const string contactPoint = "127.0.0.1";
        const int port = 9042;

        ICluster? cluster = null;

        try
        {
            cluster = Cluster.Builder()
                .AddContactPoint(contactPoint)
                .WithPort(port)
                .WithLoadBalancingPolicy(new DCAwareRoundRobinPolicy("datacenter1"))
                .Build();

            // Connect without selecting a keyspace first.
            ISession session = await cluster.ConnectAsync();

            Console.WriteLine("Connected to Cassandra.");

            await session.ExecuteAsync(new SimpleStatement(
                """
                CREATE KEYSPACE IF NOT EXISTS learning
                WITH replication = {
                    'class': 'SimpleStrategy',
                    'replication_factor': 1
                }
                """));

            await session.ExecuteAsync(new SimpleStatement(
                """
                CREATE TABLE IF NOT EXISTS learning.users (
                    user_id UUID PRIMARY KEY,
                    name TEXT,
                    email TEXT,
                    created_at TIMESTAMP
                )
                """));

            Guid userId = Guid.NewGuid();

            PreparedStatement insertStatement = await session.PrepareAsync(
                """
                INSERT INTO learning.users (
                    user_id,
                    name,
                    email,
                    created_at
                )
                VALUES (?, ?, ?, ?)
                """);

            BoundStatement insert = insertStatement.Bind(
                userId,
                "Varun",
                "varun@example.com",
                DateTimeOffset.UtcNow);

            await session.ExecuteAsync(insert);

            Console.WriteLine($"Inserted user {userId}");

            PreparedStatement selectStatement = await session.PrepareAsync(
                """
                SELECT user_id, name, email, created_at
                FROM learning.users
                WHERE user_id = ?
                """);

            RowSet result = await session.ExecuteAsync(
                selectStatement.Bind(userId));

            Row? user = result.FirstOrDefault();

            if (user is null)
            {
                Console.WriteLine("User was not found.");
                return;
            }

            Console.WriteLine($"ID:      {user.GetValue<Guid>("user_id")}");
            Console.WriteLine($"Name:    {user.GetValue<string>("name")}");
            Console.WriteLine($"Email:   {user.GetValue<string>("email")}");
            Console.WriteLine(
                $"Created: {user.GetValue<DateTimeOffset>("created_at"):O}");
        }
        catch (NoHostAvailableException exception)
        {
            Console.Error.WriteLine("Could not connect to Cassandra.");

            foreach ((IPEndPoint host, Exception error) in exception.Errors)
            {
                Console.Error.WriteLine($"{host}: {error.Message}");
            }
        }
        finally
        {
            if (cluster is not null)
            {
                await cluster.ShutdownAsync();
            }
        }
    }
}