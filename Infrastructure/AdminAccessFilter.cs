using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace RevUpIQ.Admin.Infrastructure
{
    // Runs before every action: an account may only open the sections it was given
    // (AdminAccess). Signing in itself is handled by the fallback policy in Program.cs.
    public class AdminAccessFilter : IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any()) return;

            var user = context.HttpContext.User;
            if (user.Identity?.IsAuthenticated != true) return;

            var controller = (context.ActionDescriptor as ControllerActionDescriptor)?.ControllerName;
            if (user.CanOpen(controller)) return;

            // Pages get an explanation; fetch() calls from a page just get the status
            var fetchMode = context.HttpContext.Request.Headers["Sec-Fetch-Mode"].ToString();
            context.Result = fetchMode is "" or "navigate"
                ? new ViewResult
                {
                    ViewName = "~/Views/Shared/AccessDenied.cshtml",
                    StatusCode = StatusCodes.Status403Forbidden,
                    ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), context.ModelState)
                }
                : new StatusCodeResult(StatusCodes.Status403Forbidden);
        }
    }
}
