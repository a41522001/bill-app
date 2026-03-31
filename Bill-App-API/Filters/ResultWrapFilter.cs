using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Bill_App_API.Dtos;
namespace Bill_App_API.Filters;

public class ResultWrapFilter : IResultFilter
{
  public void OnResultExecuted(ResultExecutedContext context) { }

  public void OnResultExecuting(ResultExecutingContext context)
  {
    if (context.Result is ObjectResult objectResult)
    {
      // 已經是 ResponseWrap 就跳過
      var valueType = objectResult.Value?.GetType();
      if (valueType != null
          && valueType.IsGenericType
          && valueType.GetGenericTypeDefinition() == typeof(ResponseWrap<>))
      {
        return;
      }
      var statusCode = objectResult.StatusCode ?? 200;
      var isSuccess = statusCode >= 200 && statusCode < 300;

      if (isSuccess)
      {
        objectResult.Value = ResponseWrap<object>.Success(objectResult.Value);
      }
      else
      {
        objectResult.Value = ResponseWrap<object>.Error(objectResult.Value?.ToString() ?? "Error");
      }
    }
    else if (context.Result is StatusCodeResult statusCodeResult)
    {
      var statusCode = statusCodeResult.StatusCode;
      var isSuccess = statusCode >= 200 && statusCode < 300;
      var wrapped = isSuccess
        ? ResponseWrap<object>.Success(null)
        : ResponseWrap<object>.Error("Error");
      context.Result = new ObjectResult(wrapped) { StatusCode = statusCode };
    }
  }
}
