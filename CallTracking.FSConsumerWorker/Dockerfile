#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:5.0-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim AS build
WORKDIR /src
COPY ["CallTracking.FSConsumerWorker/CallTracking.FSConsumerWorker.csproj", "CallTracking.FSConsumerWorker/"]
RUN dotnet restore "CallTracking.FSConsumerWorker/CallTracking.FSConsumerWorker.csproj"
COPY . .
WORKDIR "/src/CallTracking.FSConsumerWorker"
RUN dotnet build "CallTracking.FSConsumerWorker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CallTracking.FSConsumerWorker.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CallTracking.FSConsumerWorker.dll"]