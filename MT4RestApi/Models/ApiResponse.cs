namespace MT4RestApi.Models;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
    public string? Message { get; set; }
    
    public static ApiResponse<T> SuccessResult(T data)
    {
        return new ApiResponse<T> { Success = true, Data = data };
    }
    
    public static ApiResponse<T> ErrorResult(string error)
    {
        return new ApiResponse<T> { Success = false, Error = error };
    }
}

public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse SuccessResult()
    {
        return new ApiResponse { Success = true };
    }
    
    public static new ApiResponse ErrorResult(string error)
    {
        return new ApiResponse { Success = false, Error = error };
    }
}

public class ConnectionRequest
{
    public string Server { get; set; } = string.Empty;
}

public class LoginRequest
{
    public int Login { get; set; }
    public string Password { get; set; } = string.Empty;
}

public class BalanceInfo
{
    public int Login { get; set; }
    public double Balance { get; set; }
    public double Equity { get; set; }
    public double Margin { get; set; }
    public double FreeMargin { get; set; }
}