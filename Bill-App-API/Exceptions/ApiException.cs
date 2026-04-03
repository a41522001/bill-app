using Bill_App_API.Enums;
namespace Bill_App_API.Exceptions;

public class ApiException : Exception
{
    public int StatusCode { get; }
    public int Code { get; }
    public ApiException(string message, int statusCode = 400, int code = ResponseCodeEnum.Error) : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }
}
