FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["UrbanService.sln", "./"]
COPY ["UrbanService/UrbanService.csproj", "UrbanService/"]
COPY ["UrbanService.BLL/UrbanService.BLL.csproj", "UrbanService.BLL/"]
COPY ["UrbanService.DAL/UrbanService.DAL.csproj", "UrbanService.DAL/"]

RUN dotnet restore "UrbanService.sln"

COPY . .

RUN dotnet publish "UrbanService/UrbanService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "UrbanService.dll"]