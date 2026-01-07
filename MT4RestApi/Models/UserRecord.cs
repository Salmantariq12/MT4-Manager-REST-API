using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace MT4RestApi.Models;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct UserRecordNative
{
    // Common settings
    public int login;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
    public string group;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
    public string password;
    
    // Access flags
    public int enable;
    public int enable_change_password;
    public int enable_read_only;
    public int enable_otp;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public int[] enable_reserved;
    
    // User information
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
    public string password_investor;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string password_phone;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string name;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string country;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string city;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string state;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
    public string zipcode;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 96)]
    public string address;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string lead_source;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string phone;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 48)]
    public string email;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string comment;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string id;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
    public string status;
    public int regdate;
    public int lastdate;
    
    // Trade settings
    public int leverage;
    public int agent_account;
    public int timestamp;
    public int last_ip;
    public double balance;
    public double prevmonthbalance;
    public double prevbalance;
    public double credit;
    public double interestrate;
    public double taxes;
    public double prevmonthequity;
    public double prevequity;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public double[] reserved2;
    
    // Security
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string otp_secret;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 240)]
    public byte[] secure_reserved;
    public int send_reports;
    public uint mqid;
    public uint user_color;
}

public class UserRecord
{
    public int Login { get; set; }
    public string Group { get; set; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Password { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public double Balance { get; set; }
    public double Credit { get; set; }
    public int Leverage { get; set; }
    public bool Enable { get; set; }
    public DateTime RegDate { get; set; }
    public DateTime LastDate { get; set; }
    
    public static UserRecord FromNative(UserRecordNative native)
    {
        return new UserRecord
        {
            Login = native.login,
            Group = native.group ?? string.Empty,
            Name = native.name ?? string.Empty,
            Email = native.email ?? string.Empty,
            Country = native.country ?? string.Empty,
            City = native.city ?? string.Empty,
            Phone = native.phone ?? string.Empty,
            Balance = native.balance,
            Credit = native.credit,
            Leverage = native.leverage,
            Enable = native.enable != 0,
            RegDate = DateTimeOffset.FromUnixTimeSeconds(native.regdate).DateTime,
            LastDate = DateTimeOffset.FromUnixTimeSeconds(native.lastdate).DateTime
        };
    }
}