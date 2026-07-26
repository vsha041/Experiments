namespace ReflectionTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var metadata = new Model
            {
                Name = "cow",
                Age = "123"
            };
            Solve(metadata);
        }

        private static void Solve(Model metadata)
        {
            var properties = ReflectionHelper.HeaderProperties.GetOrAdd(
                metadata.GetType(),
                ReflectionHelper.FindHeaderProperties);

            foreach (var property in properties)
            {
                var value = property.Property.GetValue(metadata);
                if (value is not null)
                {
                    var result = ReflectionHelper.FormatHeaderValue(value);
                    Console.WriteLine($"{property.HeaderName} - {result}");
                }
            }
        }
    }

    public class Model
    {
        [AddApiResponseHeader("crocodile")]
        public string? Name { get; set; }

        [AddApiResponseHeader("kangaroo")]
        public string? Age { get; set; }

        public string? Voice { get; set; }
    }
}
