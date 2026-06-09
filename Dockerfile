# Diagnova — multi-stage Docker build
# Build context: repository root (where this Dockerfile lives)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Diagnova/Diagnova.csproj Diagnova/
RUN dotnet restore Diagnova/Diagnova.csproj

COPY Diagnova/ Diagnova/
WORKDIR /src/Diagnova
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Diagnova.dll"]
