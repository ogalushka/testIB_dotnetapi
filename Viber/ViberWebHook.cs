using tracker.Extensions;
using tracker.Viber.dto;

namespace tracker.Viber
{
    public class ViberWebHook
    {
        private readonly HttpClient client;
        private readonly ViberHttpClient httpClient;
        private readonly string hookProviderEndpoint;
        private readonly string viberHookEndpoint;
        private readonly string viberSecret;

        public ViberWebHook(HttpClient client, IConfiguration configuration, ViberHttpClient httpClient)
        {
            this.client = client;
            this.httpClient = httpClient;
            hookProviderEndpoint = configuration.GetRequiredValue<string>("HookProviderEndpoint");
            viberHookEndpoint = configuration.GetRequiredValue<string>("ViberHookEndpoint");
            viberSecret = configuration.GetRequiredValue<string>("ViberSecret");
        }

        public async Task Setup()
        {
            var hookInfo = await client.GetAsync(hookProviderEndpoint);
            hookInfo.EnsureSuccessStatusCode();
            var content = await hookInfo.Content.ReadFromJsonAsync<NgrokStatus>();
            if (content == null)
            {
                throw new ApplicationException($"Failed to get public url from ngrok, make sure it's running and config is available at url setup in HookProviderEndpoint. Current value: ${hookProviderEndpoint}");
            }
            var url = content.tunnels.First().public_url;

            await httpClient.SetupWebHook(url);
        }

        public async Task Clear()
        {
            var clearResult = await client.PostAsJsonAsync(viberHookEndpoint, new ViberHookWebData("", Array.Empty<string>()));
            clearResult.EnsureSuccessStatusCode();
        }
    }

    record NgrokStatus(NgrokTunnel[] tunnels);
    record NgrokTunnel(string public_url);
}
