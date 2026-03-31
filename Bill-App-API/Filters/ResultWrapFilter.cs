using Microsoft.AspNetCore.Mvc.Filters;

namespace Bill_App_API.Filters;

public class ResultWrapFilter : IResultFilter
{
    public void OnResultExecuted(ResultExecutedContext context)
    {
        throw new NotImplementedException();
    }

    public void OnResultExecuting(ResultExecutingContext context)
    {
        throw new NotImplementedException();
    }
}
