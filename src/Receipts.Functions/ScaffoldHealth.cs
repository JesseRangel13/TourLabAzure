using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace TourLab.Azure.Receipts;

// Process liveness only; individual job readiness remains in Notifications SQL.
public sealed class ScaffoldHealth
{
    [Function("ScaffoldHealth")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "health/live")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { service = "Receipts", status = "Alive", stage = "AZ-008" }, cancellationToken);
        return response;
    }
}
