# ====== BUILD ======
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
# копим кэш на restore
COPY ProjectApp.Api.csproj .
RUN dotnet restore ProjectApp.Api.csproj
# остальной код
COPY . .
RUN dotnet publish ProjectApp.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ====== RUNTIME ======
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
# Railway автоматически установит переменную PORT
# ASP.NET будет слушать на порту из ASPNETCORE_URLS
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet","ProjectApp.Api.dll"]
