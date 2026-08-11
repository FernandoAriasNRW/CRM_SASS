using System.Security.Cryptography;
using System.Text;
using BuildingBlocks.Infrastructure.Webhooks.Webhook.Application;
using BuildingBlocks.Infrastructure.Webhooks.Webhook.Domain;
using Polly;
using Polly.Retry;

namespace BuildingBlocks.Infrastructure.Webhooks.Webhook.Infrastructure;

public class WebhookDeliveryService : IWebhookDeliveryService
{
  private readonly HttpClient _httpClient;
  private readonly IWebhookDeliveryRepository _deliveryRepo;
  private readonly AsyncRetryPolicy _retryPolicy;

  public WebhookDeliveryService(
      HttpClient httpClient,
      IWebhookDeliveryRepository deliveryRepo)
  {
    _httpClient = httpClient;
    _deliveryRepo = deliveryRepo;

    _retryPolicy = Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(3, retry => TimeSpan.FromSeconds(Math.Pow(2, retry)));
  }

  public async Task SendAsync(WebhookEntity webhook, string payload, CancellationToken ct)
  {
    var delivery = new WebhookDelivery(webhook.Id, payload);
    await _deliveryRepo.AddAsync(delivery, ct);

    await _retryPolicy.ExecuteAsync(async () =>
    {
      var request = new HttpRequestMessage(HttpMethod.Post, webhook.Url);

      var signature = GenerateSignature(payload, webhook.Secret);

      request.Headers.Add("X-Signature", signature);
      request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

      var response = await _httpClient.SendAsync(request, ct);

      if (response.IsSuccessStatusCode)
      {
        delivery.MarkSuccess();
        await _deliveryRepo.UpdateAsync(delivery, ct);
      }
      else
      {
        delivery.MarkFailed();
        await _deliveryRepo.UpdateAsync(delivery, ct);

        throw new Exception("Webhook failed");
      }
    });
  }

  private static string GenerateSignature(string payload, string secret)
  {
    var key = Encoding.UTF8.GetBytes(secret);
    using var hmac = new HMACSHA256(key);
    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
    return Convert.ToBase64String(hash);
  }
}