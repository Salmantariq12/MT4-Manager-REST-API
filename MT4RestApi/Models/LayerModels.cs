using System.Text.Json;

namespace MT4RestApi.Models;

public class LayerRequest
{
    public string Endpoint { get; set; } = string.Empty;
    public JsonElement? Data { get; set; }
}

public class OpenTradeRequest
{
    public int Login { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public int Cmd { get; set; } // 0=Buy, 1=Sell, 2=BuyLimit, 3=SellLimit, 4=BuyStop, 5=SellStop
    public double Volume { get; set; }
    public double Price { get; set; }
    public double StopLoss { get; set; }
    public double TakeProfit { get; set; }
    public string Comment { get; set; } = string.Empty;
}

public class OpenTradeResult
{
    public bool Success { get; set; }
    public int Order { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class CloseTradesResult
{
    public bool Success { get; set; }
    public int ClosedCount { get; set; }
    public List<int> ClosedOrders { get; set; } = new List<int>();
    public string Message { get; set; } = string.Empty;
}

public class CloseAllTradesRequest
{
    public int Login { get; set; } // Optional - 0 means all users
}

public class SymbolInfo
{
    public string Symbol { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Bid { get; set; }
    public double Ask { get; set; }
    public double Spread { get; set; }
    public int Digits { get; set; }
    public double ContractSize { get; set; }
    public double MinLot { get; set; }
    public double MaxLot { get; set; }
    public double LotStep { get; set; }

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