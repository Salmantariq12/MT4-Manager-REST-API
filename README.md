# MT4 Manager REST API

A RESTful API wrapper for MetaTrader 4 Manager API, built with ASP.NET Core and C++/CLI wrapper. Includes real-time price feeds via FIX Protocol.

## Overview

This project provides a modern REST API interface to interact with MetaTrader 4 servers using the official MT4 Manager API. It enables programmatic access to trading accounts, user management, trade operations, real-time pricing, and server diagnostics.

## Features

- **Account Management** - Get balance, equity, and margin information
- **User Management** - Manage MT4 user accounts
- **Trade Operations** - Execute and manage trades
- **Symbol Data** - Access market symbols and pricing
- **Real-time Prices** - Live price feeds via FIX 4.3 Protocol
- **Layer Management** - Configure trading layers
- **Connection Management** - Connect/disconnect from MT4 servers
- **Diagnostics** - Server health and status checks
- **Swagger UI** - Interactive API documentation

## Tech Stack

- **ASP.NET Core 6+** - REST API framework
- **C++/CLI** - Native wrapper for MT4 Manager API
- **FIX Protocol 4.3** - Real-time price streaming
- **Serilog** - Structured logging
- **Swagger/OpenAPI** - API documentation

## Project Structure

```
MT4RestApi/
├── Controllers/
│   ├── AccountController.cs      # Account balance, equity, margin
│   ├── ConnectionController.cs   # MT4 server connection
│   ├── TradesController.cs       # Trade operations
│   ├── SymbolController.cs       # Symbol information
│   ├── UsersController.cs        # User management
│   ├── LayerController.cs        # Layer configuration
│   ├── PriceController.cs        # Price data endpoints
│   ├── RealtimePriceController.cs # Real-time price streaming
│   └── DiagnosticController.cs   # Health checks
├── Models/                       # Data models
├── Services/                     # Business logic & FIX integration
├── Native/                       # Native interop
├── MT4PriceSender.mq4           # MT4 Expert Advisor for prices
└── Program.cs                    # Application entry point

MT4Wrapper/                       # C++ wrapper for MT4 Manager API
├── MT4Wrapper.cpp
├── MT4Wrapper.h
└── MT4Wrapper.def

MT4RestApi_SelfContained/         # Self-contained deployment
```

## Prerequisites

- .NET 6.0 or later
- Visual Studio 2022 (for C++ wrapper compilation)
- MT4 Manager API files (`mtmanapi.dll`, `MT4ManagerAPI.h`) - obtain from MetaQuotes
- Valid MT4 Manager credentials

## Configuration

Create or update `appsettings.json` with your MT4 server details:

```json
{
  "MT4": {
    "Server": "your-mt4-server:443",
    "Login": 1,
    "Password": "your-password"
  },
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://*:5000"
      }
    }
  }
}
```

## Running the API

### Using batch files:
```bash
START_API.bat    # Start the API server
STOP_API.bat     # Stop the API server
```

### Using .NET CLI:
```bash
cd MT4RestApi
dotnet run
```

The API will be available at `http://localhost:5000` with Swagger UI at `/swagger`.

## API Endpoints

### Connection
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/connection/connect` | POST | Connect to MT4 server |
| `/api/connection/disconnect` | POST | Disconnect from server |
| `/api/connection/status` | GET | Connection status |

### Account
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/account/{login}/balance` | GET | Get account balance |
| `/api/account/{login}/equity` | GET | Get account equity |
| `/api/account/{login}/margin` | GET | Get account margin |

### Trading
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/trades` | GET | Get open trades |
| `/api/trades/{ticket}` | GET | Get specific trade |

### Prices
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/price/{symbol}` | GET | Get current price |
| `/api/realtimeprice/subscribe` | WS | Real-time price stream |

### Users & Symbols
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/users` | GET | Get user accounts |
| `/api/symbols` | GET | Get available symbols |

### Diagnostics
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/diagnostic/health` | GET | Health check |

## MT4 Expert Advisor

The `MT4PriceSender.mq4` file is an Expert Advisor that can be installed on MT4 to send price data to this API.

## License

MIT License - See LICENSE file for details.

## Disclaimer

Trading forex and CFDs involves significant risk of loss. This software is provided as-is without warranty. Use at your own risk.
