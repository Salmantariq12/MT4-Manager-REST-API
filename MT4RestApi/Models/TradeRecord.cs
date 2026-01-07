using System.Runtime.InteropServices;

namespace MT4RestApi.Models;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct TradeRecordNative
{
    public int order;
    public int login;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 12)]
    public string symbol;
    public int digits;
    public int cmd;
    public int volume;
    public int open_time;
    public int state;
    public double open_price;
    public double sl;
    public double tp;
    public int close_time;
    public int gw_volume;
    public int expiration;
    public byte reason;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public byte[] conv_reserv;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public double[] conv_rates;
    public double commission;
    public double commission_agent;
    public double storage;
    public double close_price;
    public double profit;
    public double taxes;
    public int magic;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string comment;
    public int gw_order;
    public int activation;
}

public class TradeRecord
{
    public int Order { get; set; }
    public int Login { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public int Digits { get; set; }
    public int Cmd { get; set; }
    public int Volume { get; set; }
    public DateTime OpenTime { get; set; }
    public double OpenPrice { get; set; }
    public double StopLoss { get; set; }
    public double TakeProfit { get; set; }
    public DateTime CloseTime { get; set; }
    public double ClosePrice { get; set; }
    public double Commission { get; set; }
    public double Storage { get; set; }
    public double Profit { get; set; }
    public int Magic { get; set; }
    public string Comment { get; set; } = string.Empty;

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

    public static TradeRecord FromNative(TradeRecordNative native)
    {
        var trade = new TradeRecord
        {
            Order = native.order,
            Login = native.login,
            Symbol = native.symbol ?? string.Empty,
            Digits = native.digits,
            Cmd = native.cmd,
            Volume = native.volume,
            OpenTime = DateTimeOffset.FromUnixTimeSeconds(native.open_time).DateTime,
            OpenPrice = native.open_price,
            StopLoss = native.sl,
            TakeProfit = native.tp,
            CloseTime = native.close_time > 0 ? DateTimeOffset.FromUnixTimeSeconds(native.close_time).DateTime : DateTime.MinValue,
            ClosePrice = native.close_price,
            Commission = native.commission,
            Storage = native.storage,
            Profit = native.profit,
            Magic = native.magic,
            Comment = native.comment ?? string.Empty
        };
        trade.CleanSymbol();
        return trade;
    }
}