namespace MT4RestApi.Models;

public class PriceQuote
{
    public string Symbol { get; set; } = string.Empty;
    public double Bid { get; set; }
    public double Ask { get; set; }
    public double Spread { get; set; }
    public DateTime Timestamp { get; set; }
    public DateTime Time { get; set; } = DateTime.UtcNow;
    public int Digits { get; set; }
    public double High { get; set; }
    public double Low { get; set; }

    /// <summary>
    /// Removes the .r suffix from the symbol if present
    /// </summary>
    public void CleanSymbol()
    {
        if (Symbol.EndsWith(".r", StringComparison.OrdinalIgnoreCase))
        {
            Symbol = Symbol.Substring(0, Symbol.Length - 2);
        }
    }
}

public class PriceRequest
{
    public string Symbol { get; set; } = string.Empty;
}

public class PriceResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public PriceQuote? Data { get; set; }
}