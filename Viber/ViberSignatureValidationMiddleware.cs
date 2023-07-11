using System.Security.Cryptography;
using System.Text;
using tracker.Extensions;

namespace tracker.Viber
{
    public class ViberSignatureValidationMiddleware
    {
        private readonly RequestDelegate next;
        private readonly byte[] signatureKey;

        public ViberSignatureValidationMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            this.next = next;
            signatureKey = Encoding.UTF8.GetBytes(configuration.GetRequiredValue<string>("ViberSecret"));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.Request.Headers.TryGetValue("X-Viber-Content-Signature", out var value))
            {
                throw new ApplicationException("No X-Viber-Content-Signature header in request");
            }
            var signature = value.First();

            context.Request.EnableBuffering();
            using (HMACSHA256 hmac = new HMACSHA256(signatureKey))
            {
                var expectedSignature = await hmac.ComputeHashAsync(context.Request.Body);
                var expectedString = BitConverter.ToString(expectedSignature).Replace("-", "");

                if (string.Compare(expectedString, signature, true) != 0)
                {
                    throw new ApplicationException("Failed to validate signature");
                }
            }

            context.Request.Body.Seek(0, SeekOrigin.Begin);

            await next.Invoke(context);
        }
    }
}
