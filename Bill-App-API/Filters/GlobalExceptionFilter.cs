using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Bill_App_API.Dtos;
namespace Bill_App_API.Filters;

/// <summary>
/// Step 1: 捕捉例外，回傳統一錯誤格式
/// Step 2: 設定 ExceptionHandled = true
/// Step 3: 試試看不設定 ExceptionHandled 會發生什麼事
/// </summary>
public class GlobalExceptionFilter : IExceptionFilter
{
  private readonly ILogger<GlobalExceptionFilter> _logger;

  public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger)
  {
    _logger = logger;
  }

  public void OnException(ExceptionContext context)
  {
    // 1. 記錄例外
    _logger.LogError(context.Exception, "發生未處理的例外");
    // 2. 設定統一錯誤格式
    context.Result = new ObjectResult(ResponseWrap<object>.Error(context.Exception.Message))
    {
      StatusCode = 500
    };
    // 3. 標記已處理
    context.ExceptionHandled = true;
  }
}
