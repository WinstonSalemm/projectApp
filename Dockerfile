# ====== BUILD ======
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /build

# Копируем весь проект
COPY . .

# Debug: check what was copied
RUN echo "=== BUILD ROOT ===" && ls -la /build && \
    echo "=== SRC DIR ===" && ls -la /build/src && \
    echo "=== API DIR ===" && ls -la /build/src/ProjectApp.Api

# Restore
WORKDIR /build/src/ProjectApp.Api
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
