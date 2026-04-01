using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Bill_App_API.Dtos;
using Bill_App_API.Exceptions;
namespace Bill_App_API.Filters;

public class GlobalExceptionFilter : IExceptionFilter
{
  private readonly ILogger<GlobalExceptionFilter> _logger;

  public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger)
  {
    _logger = logger;
  }

  public void OnException(ExceptionContext context)
  {
    if (context.Exception is ApiException apiException)
    {
      context.Result = new ObjectResult(ResponseWrap<object>.Error(apiException.Message))
      {
        StatusCode = apiException.StatusCode
      };
    }
    else
    {
      _logger.LogError(context.Exception, "發生未處理的例外");
      context.Result = new ObjectResult(ResponseWrap<object>.Error("伺服器內部錯誤"))
      {
        StatusCode = 500
      };
    }
    context.ExceptionHandled = true;
  }
}
