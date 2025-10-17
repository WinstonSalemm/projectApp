# ====== BUILD ======
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
# копим кэш на restore
COPY ProjectApp.Api/ProjectApp.Api.csproj ProjectApp.Api/
RUN dotnet restore ProjectApp.Api/ProjectApp.Api.csproj
# остальной код
COPY ProjectApp.Api/ ProjectApp.Api/
WORKDIR /src/ProjectApp.Api
RUN dotnet publish ProjectApp.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ====== RUNTIME ======
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:5028
COPY --from=build /app/publish .
EXPOSE 5028
ENTRYPOINT ["dotnet","ProjectApp.Api.dll"]
