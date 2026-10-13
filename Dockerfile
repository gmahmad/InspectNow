# Build with the SDK; ship only the published application and ASP.NET runtime.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json ./
COPY InspectNow.Api/InspectNow.Api.csproj InspectNow.Api/
COPY InspectNow.Application/InspectNow.Application.csproj InspectNow.Application/
COPY InspectNow.Domain/InspectNow.Domain.csproj InspectNow.Domain/
COPY InspectNow.Infrastructure/InspectNow.Infrastructure.csproj InspectNow.Infrastructure/
RUN dotnet restore InspectNow.Api/InspectNow.Api.csproj
COPY . .
RUN dotnet publish InspectNow.Api/InspectNow.Api.csproj     --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "InspectNow.Api.dll"]
