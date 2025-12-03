@echo off
echo Starting Chess960 Development Environment...

:: Start Tailwind CSS Watcher in a new window
start "Tailwind CSS Watcher" cmd /c "cd Chess960.Web && npm run css:watch"

:: Start .NET App with Hot Reload
cd Chess960.Web/Chess960.Web
dotnet watch run
