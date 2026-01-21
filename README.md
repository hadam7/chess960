# Chess960 Web Application
This project is a comprehensive Chess960 (Fischer Random) web application featuring real-time multiplayer, Stockfish bot integration, and social features. It uses ASP.NET Core for the backend and Blazor WebAssembly for the interactive frontend.

## Technologies
- **Backend**: .NET 9, ASP.NET Core, Entity Framework Core + SQLite, ASP.NET Identity
- **Frontend**: Blazor WebAssembly, SignalR, Tailwind CSS
- **Chess Engine**: Rudzoft.ChessLib (C# Chess Library), Stockfish (via Web Worker)
- **Other**: Google Authentication, SignalR for real-time communication

## Directory Structure
| Map | Content |
|---|---|
| `Chess960.Web` | ASP.NET Core Host & Server API project |
| `Chess960.Web.Client` | Blazor WebAssembly Client project |
| `start-dev.bat` | Helper script to start backend and CSS watcher |

## Prerequisites
- .NET SDK 9.0+
- Node.js 20+ and npm (for Tailwind CSS)
- SQLite (embedded `app.db` created automatically)

## Developer Setup

### 1. Database & Backend
Navigate to the server directory:
```bash
cd Chess960.Web/Chess960.Web
dotnet restore
dotnet ef database update   # Creates/Migrates the SQLite database
dotnet run                  # Starts the server (https://localhost:7147)
```
The application uses an SQLite database (`Data/app.db`).

### 2. Frontend (Tailwind CSS)
The Blazor app uses Tailwind CSS. You need to run the Tailwind compiler/watcher:
```bash
cd Chess960.Web
npm install
npm run css:watch
```
This commands watches for changes in `.razor` files and rebuilds `wwwroot/app.css`.

### 3. Quick Start (Windows)
Simply run the provided batch script in the root directory:
```cmd
start-dev.bat
```
This will open terminals for both the .NET server and the Tailwind watcher.

## Configuration
The application is configured via `appsettings.json` in the Server project.

**Database**:
`ConnectionStrings:DefaultConnection` -> Defaults to `DataSource=Data\app.db;Cache=Shared`.

**Authentication**:
Google Authentication is configured for login:
```json
"Authentication": {
  "Google": {
    "ClientId": "YOUR_CLIENT_ID",
    "ClientSecret": "YOUR_CLIENT_SECRET"
  }
}
```

## Test Accounts
The application automatically seeds two test users on startup:

| Role | Email | Password |
|---|---|---|
| Player 1 | `player1@test.com` | `Password123!` |
| Player 2 | `player2@test.com` | `Password123!` |

## Features
- **Multiplayer**: Real-time Chess960 games using SignalR.
- **Chess960**: Full support for Fischer Random starting positions.
- **Stockfish**: Play against the engine with adjustable difficulty.
- **Analysis**: Post-game analysis powered by Stockfish.
- **Social**: Friends system, status updates, and live chat.
- **Authentication**: Secure login via Google or Local accounts.

## Build & Deploy
To publish the application for production:
```bash
cd Chess960.Web/Chess960.Web
dotnet publish -c Release -o out
```
The `out` directory will contain the self-contained backend with the hosted Blazor WASM client.