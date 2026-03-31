using Microsoft.AspNetCore.Mvc.Filters;

namespace Bill_App_API.Filters;

/// <summary>
/// Step 1: 記錄 Controller、Action 名稱
/// Step 2: 記錄 ActionArguments（傳入參數）
/// Step 3: 記錄執行耗時（提示：用 Stopwatch）
/// </summary>
public class LogActionFilter : IActionFilter
{
  private readonly ILogger<LogActionFilter> _logger;

  public LogActionFilter(ILogger<LogActionFilter> logger)
  {
    _logger = logger;
  }

  public void OnActionExecuting(ActionExecutingContext context)
  {
    var action = context.RouteData.Values["action"];
    var controller = context.RouteData.Values["controller"];
    var args = context.ActionArguments;
    Console.WriteLine();
    _logger.LogInformation("{Controller} - {Action}", controller, action);
    foreach (var item in args)
    {
      _logger.LogInformation("arg: {Key} = {Value}", item.Key, item.Value);
    }
  }

  public void OnActionExecuted(ActionExecutedContext context)
  {
    if (context.Exception is not null)
    {
      _logger.LogError(context.Exception, "Action 發生例外");
    }
  }
}
