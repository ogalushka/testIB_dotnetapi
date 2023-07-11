using System.Text.Json.Serialization;

namespace tracker.Viber.dto
{
    public static class ViberEventType
    {
        public const string Subscribed = "subscribed";
        public const string ConversationStarted = "conversation_started";
        public const string Message = "message";
    }

    public class ViberEvent
    {
        [JsonPropertyName("event")]
        public string Event { get; set; } = "";
        public ViberSender Sender { get; set; } = new ViberSender(string.Empty, string.Empty);
        public ViberReceivedMessage? Message { get; set; }
    }

    public record ViberResponse(int status, string status_message);
    public record ViberSender(string name, string id = "");
    public record ViberSendMessage(string type, string text, string? receiver = null, ViberKeyboard? keyboard = null);
    public record ViberReceivedMessage(string text);

    public record ViberKeyboard(ViberButton[] Buttons, bool DefaultHeight = false, string Type = "keyboard");
    public record ViberButton(string ActionType, string ActionBody, string Text, string TextSize = "regular");
    
    public record ViberHookWebData(string url, string[] event_types, bool send_name = false, bool send_photo = false);
}
