using System.Text.Json;
using tracker.Viber.dto;

namespace tracker.Viber
{
    public class ViberHttpClient
    {
        private readonly HttpClient client;

        public ViberHttpClient(HttpClient client)
        {
            this.client = client;
        }

        public async Task SetupWebHook(string hookUrl)
        {
            var viberHookData = new ViberHookWebData(hookUrl, new[] { "delivered", "seen", "failed", "subscribed", "unsubscribed", "conversation_started" });
            using var hookRequest = new HttpRequestMessage(HttpMethod.Post, "set_webhook");
            hookRequest.Content = JsonContent.Create(viberHookData);
            var hookSetup = await client.SendAsync(hookRequest);
            hookSetup.EnsureSuccessStatusCode();
            var hookSetupResult = await hookSetup.Content.ReadFromJsonAsync<ViberResponse>();
            ValidateResponse(hookSetupResult);

        }

        public async Task SendText(string userId, string message)
        {
            var data = new ViberSendMessage(ViberMessageType.Text, message, userId);
            using var request = new HttpRequestMessage(HttpMethod.Post, "send_message");
            request.Content = JsonContent.Create(data);
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadFromJsonAsync<ViberResponse>();
            ValidateResponse(responseContent);
        }

        public async Task SendButton(string userId, string text, string buttonText, string reply)
        {
            var data = new ViberSendMessage(ViberMessageType.Text, text, userId, new ViberKeyboard(
                new ViberButton[] { new ViberButton("reply", reply, buttonText) }
                ));
            using var request = new HttpRequestMessage(HttpMethod.Post, "send_message");
            request.Content = JsonContent.Create(data, options: new JsonSerializerOptions(JsonSerializerDefaults.General));
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadFromJsonAsync<ViberResponse>();
            ValidateResponse(responseContent);
        }


        private void ValidateResponse(ViberResponse? result)
        {
            if (result == null)
            {
                throw new ApplicationException("Failed to parse Viber hook setup responce");
            }

            if (result.status != 0)
            {
                throw new ApplicationException($"Failed to setup Viber web hook error code: {result.status} message: {result.status_message}");
            }
        }
    }

}
