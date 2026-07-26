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

            //var metadata2 = new List<Model>();
            //metadata2.Add(new Model()
            //{
            //    Name = "one"
            //});
            //metadata2.Add(new Model()
            //{
            //    Name = "two"
            //});
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
