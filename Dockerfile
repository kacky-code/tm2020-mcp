FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY tm2020-mcp.slnx ./
COPY src/Tm2020Mcp/Tm2020Mcp.csproj src/Tm2020Mcp/
RUN dotnet restore

COPY src/Tm2020Mcp src/Tm2020Mcp
RUN dotnet publish src/Tm2020Mcp/Tm2020Mcp.csproj \
  --configuration Release \
  --no-restore \
  --output /app

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app

ENV TM2020_BRIDGE_URL=http://host.docker.internal:29100

COPY --from=build /app ./
ENTRYPOINT ["dotnet", "Tm2020Mcp.dll"]

