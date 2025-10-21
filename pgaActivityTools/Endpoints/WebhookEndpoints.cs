using pgaActivityTools.Common.OpenApi;
using pgaActivityTools.Models.Webhook;
using pgaActivityTools.Services.Strava;

namespace pgaActivityTools.Endpoints;

public static class WebhookEndpoints
{
    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/webhook")
            .WithTags(OpenApiTags.WebHook.ToString())
            .WithOpenApi();

        group.MapPost("/subscribe", WebhookSubscription)
        .RequireAuthorization()
            .WithName("CreateStravaWebhookSubscription");

        group.MapGet("", WebhookValidation)
            .AllowAnonymous()
            .WithName("ValidateStravaWebhook");

        group.MapPost("", WebhookEvent)
            .AllowAnonymous()
            .WithName("ReceiveStravaWebhookEvent");

        group.MapGet("/subscriptions", GetSubscriptions)
        .RequireAuthorization()
            .WithName("GetStravaWebhookSubscriptions");

        group.MapDelete("/subscribe/{subscriptionId}", DeleteSubscription)
        .RequireAuthorization()
            .WithName("DeleteStravaWebhookSubscription");

        return routes;
    }

    private static async Task<IResult> DeleteSubscription(IStravaWebhook webhookService, ILogger<Program> logger, [Microsoft.AspNetCore.Mvc.FromRoute(Name = "subscriptionId")] int subscriptionId)
    {
        var success = await webhookService.DeleteSubscriptionAsync(subscriptionId);
        return success ? Results.Ok() : Results.BadRequest("Failed to delete subscription");
    }

    private static async Task<IResult> GetSubscriptions(IStravaWebhook webhookService, ILogger<Program> logger)
    {
        try
        {
            var subscriptions = await webhookService.GetSubscriptionsAsync();
            return Results.Ok(subscriptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting subscriptions");
            return Results.Problem(ex.Message);
        }
    }

    private static IResult WebhookValidation([Microsoft.AspNetCore.Mvc.FromQuery(Name = "hub.mode")] string hubMode,
    [Microsoft.AspNetCore.Mvc.FromQuery(Name = "hub.challenge")] string hubChallenge,
    [Microsoft.AspNetCore.Mvc.FromQuery(Name = "hub.verify_token")] string hubVerifyToken,
    IConfiguration configuration, ILogger<Program> logger)
    {
        var expectedToken = configuration["Strava:WebhookVerifyToken"];

        if (hubMode == "subscribe" && hubVerifyToken == expectedToken)
        {
            logger.LogInformation("Webhook validation successful");
            return Results.Json(new Dictionary<string, string>
        {
            { "hub.challenge", hubChallenge }
        });
        }

        return Results.Unauthorized();
    }

    private static async Task<IResult> WebhookSubscription(IStravaWebhook webhookService, ILogger<Program> logger)
    {
        logger.LogInformation("Attempting to create Strava webhook subscription");

        try
        {
            var subscription = await webhookService.CreateSubscriptionAsync();

            if (subscription != null)
            {
                logger.LogInformation("Subscription created successfully: {SubscriptionId}", subscription.Id);
                return Results.Ok(subscription);
            }

            logger.LogWarning("Failed to create subscription");
            return Results.BadRequest("Failed to create subscription");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating subscription");
            return Results.Problem(ex.Message);
        }
    }

    private async static Task<IResult> WebhookEvent(HttpContext context,
    IStravaWebhook webhookService,
    ILogger<Program> logger)
    {
        try
        {
            // Vérifier si le body est vide
            if (context.Request.ContentLength == 0 || context.Request.ContentLength == null)
            {
                logger.LogWarning("Empty request body received");
                return Results.Ok(); // Retourner 200 quand même pour éviter les retry
            }

            string body = string.Empty;

            try
            {
                context.Request.EnableBuffering();
                using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                body = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;

                logger.LogInformation("Body: {Body}", body);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error reading request body");
                return Results.Ok(); // Retourner 200 pour éviter les retry
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                logger.LogWarning("Empty or whitespace body received");
                return Results.Ok();
            }

            StravaWebhookEvent? webhookEvent = null;

            try
            {
                webhookEvent = System.Text.Json.JsonSerializer.Deserialize<StravaWebhookEvent>(
                    body,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deserializing webhook event");
                return Results.Ok(); // Retourner 200 pour éviter les retry
            }

            if (webhookEvent != null)
            {
                logger.LogInformation("✅ Received webhook: Type={Type}, ObjectId={ObjectId}, AthleteId={AthleteId}",
                    webhookEvent.Aspect_type, webhookEvent.Object_id, webhookEvent.Owner_id);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await webhookService.ProcessActivityEventAsync(webhookEvent);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error processing webhook in background");
                    }
                });
            }
            else
            {
                logger.LogWarning("Failed to deserialize webhook event");
            }

            return Results.Ok();
        }
        catch (BadHttpRequestException ex)
        {
            logger.LogError(ex, "BadHttpRequestException: {Message}", ex.Message);
            return Results.Ok(); // Retourner 200 pour éviter les retry de Strava
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error processing webhook");
            return Results.Ok(); // Toujours retourner 200 pour Strava
        }

    }
}