# ====== BUILD ======
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
# копим кэш на restore
COPY ProjectApp.Core/ProjectApp.Core.csproj ProjectApp.Core/
COPY ProjectApp.Api/ProjectApp.Api.csproj ProjectApp.Api/
RUN dotnet restore "ProjectApp.Api/ProjectApp.Api.csproj"
# остальной код
COPY . .
WORKDIR /src/ProjectApp.Api
RUN dotnet publish "ProjectApp.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ====== RUNTIME ======
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
# Railway автоматически установит переменную PORT
# ASP.NET будет слушать на порту из ASPNETCORE_URLS
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:$PORT
EXPOSE 8080
ENTRYPOINT ["dotnet", "ProjectApp.Api.dll"]
