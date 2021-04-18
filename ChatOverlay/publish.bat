dotnet publish -c Release -r win-x64 --self-contained=true -p:PublishTrimmed=false -o ../Publish
start ..\Publish
pause