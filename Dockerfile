FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["UrbanService.sln", "./"]
COPY ["UrbanService/UrbanService.csproj", "UrbanService/"]
COPY ["UrbanService.BLL/UrbanService.BLL.csproj", "UrbanService.BLL/"]
COPY ["UrbanService.DAL/UrbanService.DAL.csproj", "UrbanService.DAL/"]

# Restore only the production API project. The solution also contains the
# test project, which is not needed in the runtime image and is intentionally
# not copied into the dependency-cache layer.
RUN dotnet restore "UrbanService/UrbanService.csproj"

COPY . .

RUN dotnet publish "UrbanService/UrbanService.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    Brevo__ApiKey="" \
    Brevo__SenderEmail="" \
    Brevo__SenderName=UrbanService \
    GoogleAuth__ClientId="" \
    Messenger__GraphApiVersion=v25.0 \
    SlaMonitoring__Enabled=true \
    SlaMonitoring__IntervalMinutes=5 \
    SlaMonitoring__InitialDelaySeconds=10 \
    SlaMonitoring__WarningThresholdPercent=30

EXPOSE 8080

USER $APP_UID

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl --fail --silent http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "UrbanService.dll"]
