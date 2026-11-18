namespace EnterpriseHR.Api.Models;

/// <summary>
/// The envelope every API response uses, success or failure:
/// <c>{ "success": true, "message": "OK", "data": { ... } }</c>.
/// Clients branch on <see cref="Success"/> first and read <see cref="Message"/> when it is
/// false; <see cref="Data"/> carries the payload on success and the validation details on a
/// 400 model-binding failure.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T data, string message = "OK")
    {
        return new ApiResponse<T> { Success = true, Message = message, Data = data };
    }

    public static ApiResponse<T> Fail(string message, T? data = default)
    {
        return new ApiResponse<T> { Success = false, Message = message, Data = data };
    }
}
