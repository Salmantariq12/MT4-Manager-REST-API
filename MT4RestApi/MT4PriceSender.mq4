//+------------------------------------------------------------------+
//|                                            MT4PriceSender.mq4   |
//|                     Sends real-time prices via HTTP POST        |
//+------------------------------------------------------------------+
#property copyright "MT4 Price Sender"
#property link      "http://localhost"
#property version   "1.00"
#property strict

// Input parameters
input string WebhookURL = "http://localhost:8081/prices";  // Webhook endpoint
input int SendIntervalMs = 1000;  // Send interval in milliseconds
input string SymbolsList = "AUDUSD,EURUSD,GBPUSD,USDJPY,XAUUSD";  // Symbols to monitor

string symbols[];
datetime lastSendTime = 0;

//+------------------------------------------------------------------+
//| Expert initialization function                                   |
//+------------------------------------------------------------------+
int OnInit()
{
    // Parse symbols list
    StringSplit(SymbolsList, ',', symbols);

    Print("MT4 Price Sender initialized");
    Print("Sending prices for: ", SymbolsList);
    Print("Webhook URL: ", WebhookURL);

    return(INIT_SUCCEEDED);
}

//+------------------------------------------------------------------+
//| Expert tick function                                             |
//+------------------------------------------------------------------+
void OnTick()
{
    // Check if enough time has passed
    if(GetTickCount() - lastSendTime < SendIntervalMs)
        return;

    lastSendTime = GetTickCount();

    // Send prices for all symbols
    for(int i = 0; i < ArraySize(symbols); i++)
    {
        string symbol = symbols[i];
        if(symbol == "") continue;

        double bid = MarketInfo(symbol, MODE_BID);
        double ask = MarketInfo(symbol, MODE_ASK);
        double high = MarketInfo(symbol, MODE_HIGH);
        double low = MarketInfo(symbol, MODE_LOW);

        if(bid > 0 && ask > 0)
        {
            SendPrice(symbol, bid, ask, high, low);
        }
    }
}

//+------------------------------------------------------------------+
//| Send price via HTTP POST                                        |
//+------------------------------------------------------------------+
void SendPrice(string symbol, double bid, double ask, double high, double low)
{
    string json = StringFormat(
        "{\"symbol\":\"%s\",\"bid\":%f,\"ask\":%f,\"high\":%f,\"low\":%f,\"time\":\"%s\"}",
        symbol, bid, ask, high, low, TimeToString(TimeGMT(), TIME_DATE|TIME_SECONDS)
    );

    // Using WebRequest (requires adding URL to allowed list in MT4)
    char post[];
    char result[];
    string headers = "Content-Type: application/json\r\n";

    StringToCharArray(json, post, 0, StringLen(json));

    int res = WebRequest("POST", WebhookURL, headers, 5000, post, result, headers);

    if(res == -1)
    {
        Print("Error sending price for ", symbol, ": ", GetLastError());
    }
    else
    {
        Print("Sent price for ", symbol, ": Bid=", bid, " Ask=", ask);
    }
}

//+------------------------------------------------------------------+
//| Expert deinitialization function                                |
//+------------------------------------------------------------------+
void OnDeinit(const int reason)
{
    Print("MT4 Price Sender stopped");
}