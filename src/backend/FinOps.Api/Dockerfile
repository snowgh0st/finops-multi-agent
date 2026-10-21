# ==========================================
# Build Stage (.NET 10.0)
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for dependency restoration
COPY ["FinOps.Api/FinOps.Api.csproj", "FinOps.Api/"]
COPY ["FinOps.Core/FinOps.Core.csproj", "FinOps.Core/"]
COPY ["FinOps.Domain/FinOps.Domain.csproj", "FinOps.Domain/"]
COPY ["FinOps.Infrastructure/FinOps.Infrastructure.csproj", "FinOps.Infrastructure/"]

RUN dotnet restore "FinOps.Api/FinOps.Api.csproj"

# Copy full source code and publish
COPY . .
WORKDIR "/src/FinOps.Api"
RUN dotnet publish "FinOps.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ==========================================
# Runtime Stage (.NET 10.0)
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install sqlite3 for container inspection if needed
RUN apt-get update && apt-get install -y sqlite3 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Ensure Models directory exists and copy the ONNX model
COPY --from=build /src/FinOps.Api/Models /app/Models

EXPOSE 8080

ENTRYPOINT ["dotnet", "FinOps.Api.dll"]