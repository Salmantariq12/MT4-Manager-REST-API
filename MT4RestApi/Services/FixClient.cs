using System.Net.Sockets;
using System.Text;
using System.Collections.Concurrent;

namespace MT4RestApi.Services;

/// <summary>
/// Lightweight FIX 4.3 Protocol Client for MT4 Price Feed
/// </summary>
public class FixClient : IDisposable
{
    private readonly ILogger<FixClient> _logger;
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private bool _isConnected = false;
    private bool _disposed = false;
    private int _msgSeqNum = 1;
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;

    // FIX Configuration
    private readonly string _senderCompID;
    private readonly string _targetCompID;
    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;

    // Event for receiving market data - passes raw FIX message for proper repeating group parsing
    public event Action<string>? OnMarketDataReceived;

    public bool IsConnected => _isConnected;

    public FixClient(
        string host,
        int port,
        string senderCompID,
        string targetCompID,
        string username,
        string password,
        ILogger<FixClient> logger)
    {
        _host = host;
        _port = port;
        _senderCompID = senderCompID;
        _targetCompID = targetCompID;
        _username = username;
        _password = password;
        _logger = logger;
    }

    public async Task<bool> ConnectAsync()
    {
        try
        {
            _logger.LogInformation("Connecting to FIX server {Host}:{Port}", _host, _port);

            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(_host, _port);
            _stream = _tcpClient.GetStream();

            _logger.LogInformation("TCP connection established to {Host}:{Port}", _host, _port);

            // Set connected BEFORE sending Logon so SendMessageAsync doesn't early return
            _isConnected = true;

            // Start receiving messages BEFORE sending logon to catch the response
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => ReceiveMessagesAsync(_cts.Token));

            // Send Logon message
            var logonMsg = CreateLogonMessage();
            await SendMessageAsync(logonMsg);

            _logger.LogInformation("FIX Logon message sent successfully");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to FIX server");
            _isConnected = false;
            return false;
        }
    }

    private string CreateLogonMessage()
    {
        var msgType = "A"; // Logon
        var body = new StringBuilder();

        // Standard header fields will be added by BuildMessage
        body.Append($"98=0\x01"); // EncryptMethod (0 = None)
        body.Append($"108=30\x01"); // HeartBtInt (30 seconds)
        body.Append($"553={_username}\x01"); // Username
        body.Append($"554={_password}\x01"); // Password
        body.Append($"141=Y\x01"); // ResetSeqNumFlag

        return BuildMessage(msgType, body.ToString());
    }

    private string BuildMessage(string msgType, string body)
    {
        var msg = new StringBuilder();
        var sendingTime = DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss.fff");

        // Build body with header fields
        var fullBody = new StringBuilder();
        fullBody.Append($"35={msgType}\x01"); // MsgType
        fullBody.Append($"49={_senderCompID}\x01"); // SenderCompID
        fullBody.Append($"56={_targetCompID}\x01"); // TargetCompID
        fullBody.Append($"34={_msgSeqNum}\x01"); // MsgSeqNum
        fullBody.Append($"52={sendingTime}\x01"); // SendingTime
        fullBody.Append(body);

        var bodyStr = fullBody.ToString();
        var bodyLength = Encoding.ASCII.GetByteCount(bodyStr);

        // Begin string
        msg.Append($"8=FIX.4.3\x01");
        msg.Append($"9={bodyLength}\x01");
        msg.Append(bodyStr);

        // Calculate checksum
        var msgWithoutChecksum = msg.ToString();
        var checksum = CalculateChecksum(msgWithoutChecksum);
        msg.Append($"10={checksum:000}\x01");

        _msgSeqNum++;
        return msg.ToString();
    }

    private string CalculateChecksum(string message)
    {
        int sum = 0;
        foreach (char c in message)
        {
            sum += c;
        }
        return (sum % 256).ToString("000");
    }

    private async Task SendMessageAsync(string message)
    {
        if (_stream == null || !_isConnected) return;

        lock (_lock)
        {
            try
            {
                var bytes = Encoding.ASCII.GetBytes(message);
                _stream.Write(bytes, 0, bytes.Length);
                _stream.Flush();

                _logger.LogInformation("FIX RAW MESSAGE SENT: {Message}", message.Replace("\x01", "|"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send FIX message");
                _isConnected = false;
            }
        }
    }

    public async Task SendMarketDataRequestAsync(string[] symbols, string? account = null)
    {
        if (!_isConnected) return;

        foreach (var symbol in symbols)
        {
            var msgType = "V"; // MarketDataRequest
            var mdReqID = Guid.NewGuid().ToString("N").Substring(0, 8);

            var body = new StringBuilder();

            // Required fields in correct order per FXCubic specification
            body.Append($"262={mdReqID}\x01"); // MDReqID (required)

            // Optional Account field - might be required by this server
            if (!string.IsNullOrWhiteSpace(account))
            {
                body.Append($"1={account}\x01"); // Account (optional but might be needed)
            }

            body.Append($"263=1\x01"); // SubscriptionRequestType (1 = Snapshot + Updates, required)
            body.Append($"264=1\x01"); // MarketDepth (1 = Top of Book, per FXCubic example)
            body.Append($"265=0\x01"); // MDUpdateType (0 = Full Refresh, per FXCubic example)
            body.Append($"266=N\x01"); // AggregatedBook (N = No, per FXCubic example)

            // NoMDEntryTypes group (required) - must come before NoRelatedSym
            body.Append($"267=2\x01"); // NoMDEntryTypes (number of entry types, required)
            body.Append($"269=0\x01"); // MDEntryType (0 = Bid, required in group)
            body.Append($"269=1\x01"); // MDEntryType (1 = Offer/Ask, required in group)

            // NoRelatedSym group (required) - must come after NoMDEntryTypes
            body.Append($"146=1\x01"); // NoRelatedSym (number of symbols, required)
            body.Append($"55={symbol}\x01"); // Symbol (required in Instrument component)
            body.Append($"460=4\x01"); // Product (4 = Currency, per FXCubic example)

            var message = BuildMessage(msgType, body.ToString());
            await SendMessageAsync(message);

            _logger.LogInformation("Sent MarketDataRequest for symbol: {Symbol}, Account: {Account}", symbol, account ?? "none");
        }
    }

    private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var messageBuffer = new StringBuilder();

        _logger.LogInformation("FIX message receiver started");

        try
        {
            while (_isConnected && !cancellationToken.IsCancellationRequested && _stream != null)
            {
                try
                {
                    // Use timeout to prevent indefinite blocking
                    var readTask = _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

                    var completedTask = await Task.WhenAny(readTask, timeoutTask);

                    if (completedTask == timeoutTask)
                    {
                        // Timeout - no data received in 30 seconds
                        _logger.LogWarning("No data received from FIX server in 30 seconds");
                        continue; // Keep waiting
                    }

                    int bytesRead = await readTask;

                    if (bytesRead > 0)
                    {
                        var data = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                        messageBuffer.Append(data);

                        // Log raw incoming data for debugging
                        _logger.LogInformation("FIX RAW MESSAGE RECEIVED: {Message}", data.Replace("\x01", "|"));

                        // Process complete messages
                        ProcessMessages(messageBuffer);
                    }
                    else
                    {
                        // bytesRead == 0 means connection closed
                        _logger.LogWarning("FIX connection closed by server");
                        _isConnected = false;
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in receive loop");
                    await Task.Delay(1000, cancellationToken); // Backoff before retry
                }
            }
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Fatal error in FIX message receiver");
                _isConnected = false;
            }
        }

        _logger.LogInformation("FIX message receiver stopped");
    }

    private void ProcessMessages(StringBuilder buffer)
    {
        var bufferStr = buffer.ToString();

        _logger.LogInformation("ProcessMessages called with buffer length: {Length}", bufferStr.Length);

        // Debug: show first 100 chars of buffer
        var preview = bufferStr.Length > 100 ? bufferStr.Substring(0, 100) : bufferStr;
        _logger.LogInformation("Buffer preview: {Preview}", preview.Replace("\x01", "|"));

        // Check if buffer contains "8=FIX"
        var contains8FIX = bufferStr.Contains("8=FIX");
        _logger.LogInformation("Buffer contains '8=FIX': {Contains}", contains8FIX);

        // FIX messages start with "8=FIX" and end with checksum "10=xxx\x01"
        while (bufferStr.Contains("8=FIX"))
        {
            var startIdx = bufferStr.IndexOf("8=FIX");
            if (startIdx < 0)
            {
                _logger.LogInformation("No 8=FIX found");
                break;
            }

            _logger.LogInformation("Found message start at index: {Index}", startIdx);

            // Debug: Check if buffer contains "10=" at all
            var contains10Equals = bufferStr.Contains("10=");
            var idx10Equals = bufferStr.IndexOf("10=");
            _logger.LogInformation("Buffer contains '10=': {Contains}, Index: {Index}", contains10Equals, idx10Equals);

            // Debug: Show characters around where checksum should be (near end of buffer)
            if (bufferStr.Length > 20)
            {
                var endPreview = bufferStr.Substring(Math.Max(0, bufferStr.Length - 20));
                _logger.LogInformation("Buffer end (last 20 chars): {EndPreview}", endPreview.Replace("\x01", "|"));
            }

            // Find the checksum field which marks the end
            // IMPORTANT: Use "\x01" + "10=" instead of "\x0110=" to avoid hex parsing issues
            //   "\x0110" would be parsed as hex 0x0110 instead of \x01 followed by "10"
            var checksumIdx = bufferStr.IndexOf("\x01" + "10=", startIdx);
            if (checksumIdx < 0)
            {
                _logger.LogInformation("No checksum field found - searching for SOH+10= pattern failed");

                // Debug: Try searching for just "10=" and see what character is before it
                if (idx10Equals >= 0 && idx10Equals > 0)
                {
                    var charBefore = (int)bufferStr[idx10Equals - 1];
                    _logger.LogInformation("Found '10=' at index {Idx}, character before it: {Char} (byte value: {Byte})",
                        idx10Equals, bufferStr[idx10Equals - 1], charBefore);
                }

                break; // Incomplete message
            }

            _logger.LogInformation("Found checksum at index: {Index}", checksumIdx);

            // Find end of checksum (next SOH after 10=)
            var endIdx = bufferStr.IndexOf("\x01", checksumIdx + 4);
            if (endIdx < 0)
            {
                _logger.LogInformation("No end SOH found - incomplete message");
                break; // Incomplete message
            }

            _logger.LogInformation("Found message end at index: {Index}", endIdx);

            // Extract the complete message
            var message = bufferStr.Substring(startIdx, endIdx - startIdx + 1);

            _logger.LogInformation("Extracted complete FIX message, calling HandleFixMessage");

            // Remove from buffer
            buffer.Remove(0, endIdx + 1);
            bufferStr = buffer.ToString();

            // Parse and handle the message
            HandleFixMessage(message);
        }
    }

    private void HandleFixMessage(string message)
    {
        try
        {
            var fields = ParseFixMessage(message);

            if (!fields.ContainsKey("35")) return; // No MsgType

            var msgType = fields["35"];

            _logger.LogDebug("Received FIX message type: {MsgType}", msgType);

            switch (msgType)
            {
                case "A": // Logon
                    _logger.LogInformation("FIX Logon successful");
                    break;

                case "0": // Heartbeat
                    _logger.LogDebug("Received Heartbeat");
                    break;

                case "1": // TestRequest
                    SendHeartbeat(fields.ContainsKey("112") ? fields["112"] : "");
                    break;

                case "W": // MarketDataSnapshotFullRefresh
                    HandleMarketData(message);
                    break;

                case "3": // Reject
                    _logger.LogWarning("FIX Reject received: {Message}", message.Replace("\x01", "|"));
                    break;

                case "5": // Logout
                    _logger.LogWarning("FIX Logout received");
                    _isConnected = false;
                    break;

                default:
                    _logger.LogDebug("Unhandled FIX message type: {MsgType}", msgType);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling FIX message");
        }
    }

    private Dictionary<string, string> ParseFixMessage(string message)
    {
        var fields = new Dictionary<string, string>();
        var pairs = message.Split('\x01', StringSplitOptions.RemoveEmptyEntries);

        foreach (var pair in pairs)
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2)
            {
                // Store last value if duplicate (for repeating groups)
                fields[parts[0]] = parts[1];
            }
        }

        return fields;
    }

    private void HandleMarketData(string rawMessage)
    {
        try
        {
            _logger.LogInformation("FixClient.HandleMarketData called");

            // Check if anyone is subscribed
            if (OnMarketDataReceived == null)
            {
                _logger.LogWarning("No subscribers to OnMarketDataReceived event!");
            }
            else
            {
                _logger.LogInformation("Invoking OnMarketDataReceived event with {Count} subscribers",
                    OnMarketDataReceived.GetInvocationList().Length);
            }

            // Notify listeners with RAW message so they can properly parse repeating groups
            OnMarketDataReceived?.Invoke(rawMessage);

            _logger.LogInformation("Event invoked successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling market data");
        }
    }

    private void SendHeartbeat(string testReqID = "")
    {
        var msgType = "0"; // Heartbeat
        var body = string.IsNullOrEmpty(testReqID) ? "" : $"112={testReqID}\x01";
        var message = BuildMessage(msgType, body);
        _ = SendMessageAsync(message);
    }

    public async Task DisconnectAsync()
    {
        if (!_isConnected) return;

        try
        {
            // Send Logout
            var logoutMsg = BuildMessage("5", ""); // Logout msgType
            await SendMessageAsync(logoutMsg);

            await Task.Delay(1000); // Wait for graceful disconnect

            _cts?.Cancel();
            _stream?.Close();
            _tcpClient?.Close();
            _isConnected = false;

            _logger.LogInformation("Disconnected from FIX server");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during FIX disconnect");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _cts?.Cancel();
        _cts?.Dispose();
        _stream?.Dispose();
        _tcpClient?.Dispose();
        _disposed = true;
    }
}
