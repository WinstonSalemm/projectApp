# ====== BUILD ======
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Копируем csproj файлы для restore (кэширование слоев)
COPY src/ProjectApp.Core/ProjectApp.Core.csproj ProjectApp.Core/
COPY src/ProjectApp.Api/ProjectApp.Api.csproj ProjectApp.Api/

# Restore зависимостей
RUN dotnet restore "ProjectApp.Api/ProjectApp.Api.csproj"

# Копируем весь исходный код
COPY src/ .

# Build и publish
WORKDIR /src/ProjectApp.Api
RUN dotnet publish "ProjectApp.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ====== RUNTIME ======
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
# Railway автоматически установит переменную PORT
# Используйте переменную окружения ASPNETCORE_URLS в Railway UI
EXPOSE 8080
ENTRYPOINT ["dotnet", "ProjectApp.Api.dll"]
