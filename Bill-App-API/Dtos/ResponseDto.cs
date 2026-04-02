namespace Bill_App_API.Dtos;

public class ResponseWrap<T>
{
    public T? Data { get; set; }
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Time { get; set; } = DateTime.UtcNow;
    public static ResponseWrap<T> Success(T? data, string message = "成功", int code = 0)
      => new() { Data = data, Code = code, Message = message };

    public static ResponseWrap<T> Error(string message, int code = 1)
      => new() { Data = default, Message = message, Code = code };
}
