#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:5.0-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim AS build
WORKDIR /src
COPY ["CallTracking.FSProducerWorker/CallTracking.FSProducerWorker.csproj", "CallTracking.FSProducerWorker/"]
RUN dotnet restore "CallTracking.FSProducerWorker/CallTracking.FSProducerWorker.csproj"
COPY . .
WORKDIR "/src/CallTracking.FSProducerWorker"
RUN dotnet build "CallTracking.FSProducerWorker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CallTracking.FSProducerWorker.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CallTracking.FSProducerWorker.dll"]