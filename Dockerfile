# ====== BUILD ======
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Debug: check build context
COPY . /debug
RUN ls -la /debug && ls -la /debug/src || echo "No src directory"

# Копируем решение и проекты
COPY src/ ./

# Restore
WORKDIR /app/ProjectApp.Api
RUN dotnet restore "ProjectApp.Api.csproj"

# Build и publish
RUN dotnet publish "ProjectApp.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ====== RUNTIME ======
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
# Railway автоматически установит переменную PORT
# Используйте переменную окружения ASPNETCORE_URLS в Railway UI
EXPOSE 8080
ENTRYPOINT ["dotnet", "ProjectApp.Api.dll"]
