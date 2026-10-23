using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace TourLab.Azure.Receipts;

// Host-only scaffold. Receipt processing is AZ-008; this never claims a receipt was generated.
public sealed class ScaffoldHealth
{
    [Function("ScaffoldHealth")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "health/live")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { service = "Receipts", status = "Alive", stage = "AZ-001" }, cancellationToken);
        return response;
    }
}
