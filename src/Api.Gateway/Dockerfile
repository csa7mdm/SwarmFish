FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Api.Gateway/Api.Gateway.csproj", "src/Api.Gateway/"]
COPY ["src/Core.Contracts/Core.Contracts.csproj", "src/Core.Contracts/"]
COPY ["src/Core.Domain/Core.Domain.csproj", "src/Core.Domain/"]
COPY ["src/Agents.Orleans/Agents.Orleans.csproj", "src/Agents.Orleans/"]
COPY ["src/Graph.KuzuDB/Graph.KuzuDB.csproj", "src/Graph.KuzuDB/"]
COPY ["src/Memory.Zep/Memory.Zep.csproj", "src/Memory.Zep/"]
COPY ["src/Simulation.Engine/Simulation.Engine.csproj", "src/Simulation.Engine/"]
COPY ["src/Report.Agent/Report.Agent.csproj", "src/Report.Agent/"]
RUN dotnet restore "src/Api.Gateway/Api.Gateway.csproj"
COPY . .
RUN dotnet publish "src/Api.Gateway/Api.Gateway.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
# Download KuzuDB native library for linux-x64
RUN apt-get update && apt-get install -y curl && \
    curl -L https://github.com/kuzudb/kuzu/releases/latest/download/libkuzu-linux-x86_64.tar.gz \
    -o /tmp/kuzu.tar.gz && \
    tar -xzf /tmp/kuzu.tar.gz -C /app && \
    rm /tmp/kuzu.tar.gz
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Api.Gateway.dll"]
