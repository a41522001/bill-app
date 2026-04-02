namespace Bill_App_API.Extensions;

public static class HttpContextExtension
{
    extension(HttpContext context)
    {
        public Guid GetUserId() =>
            (Guid)context.Items["userId"]!;
    }
}
