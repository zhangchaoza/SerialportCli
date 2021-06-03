# dotnet publish -c release -r win-x64 -v n
dotnet pack .\src -c release /p:PackAsTool=true /p:ToolCommandName=SerialportCli

Pause