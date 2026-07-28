using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RedfishEmulator.Core.Models;

namespace RedfishEmulator.Api.Filters;

/// <summary>
/// Rewrites bare controller error results (e.g. <c>NotFound()</c>) into Redfish
/// <see cref="RedfishError"/> bodies, so clients always receive the standard
/// <c>error</c> / <c>@Message.ExtendedInfo</c> shape instead of an empty response.
/// </summary>
public sealed class RedfishErrorResultFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        context.Result = context.Result switch
        {
            NotFoundResult => NotFound(context),
            NotFoundObjectResult { Value: null } => NotFound(context),
            _ => context.Result,
        };
    }

    public void OnResultExecuted(ResultExecutedContext context) { }

    private static ObjectResult NotFound(ResultExecutingContext context) =>
        new(RedfishError.ResourceNotFound(context.HttpContext.Request.Path))
        {
            StatusCode = StatusCodes.Status404NotFound,
        };
}
