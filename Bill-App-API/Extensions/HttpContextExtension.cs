namespace Bill_App_API.Extensions;

public static class HttpContextExtension
{
    extension(HttpContext context)
    {
        public Guid GetUserId() =>
            (Guid)context.Items["userId"]!;
        public bool HasUserId() =>
            context.Items.ContainsKey("userId");
        public void SetUserId(Guid userId) =>
            context.Items["userId"] = userId;
    }
}
