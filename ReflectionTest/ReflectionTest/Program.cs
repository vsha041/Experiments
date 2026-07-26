namespace ReflectionTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var models = new List<Model>
            {
                new() { Name = "cow", Age = "123" },
                new() { Name = "dog", Age = "456", Voice = "bark" }
            };
            Solve(models);
        }

        private static void Solve<T>(List<T> models)
        {
            var properties = ReflectionHelper.HeaderProperties.GetOrAdd(
                typeof(T),
                ReflectionHelper.FindHeaderProperties);

            foreach (var model in models)
            {
                if (model is null)
                {
                    continue;
                }

                foreach (var property in properties)
                {
                    var value = property.Property.GetValue(model);
                    if (value is not null)
                    {
                        var result = ReflectionHelper.FormatHeaderValue(value);
                        Console.WriteLine($"{property.HeaderName} - {result}");
                    }
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
