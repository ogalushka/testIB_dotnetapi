namespace tracker.Extensions
{
    public static class Extensions
    {
        public static T GetRequiredValue<T>(this IConfiguration configuration, string key)
        {
            return configuration.GetValue<T>(key) ?? throw new ApplicationException($"Failed to get configuration with key: {key}");
        }
    }
}
