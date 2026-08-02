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

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    Brevo__ApiKey="" \
    Brevo__SenderEmail="" \
    Brevo__SenderName=UrbanService \
    GoogleAuth__ClientId=""

EXPOSE 8080

ENTRYPOINT ["dotnet", "UrbanService.dll"]
