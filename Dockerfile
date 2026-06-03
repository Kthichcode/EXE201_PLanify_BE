# ─────────────────────────────────────────────────────────────
# Stage 1: Build
# ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files trước để tận dụng Docker layer cache khi restore
COPY Planify_Project/Planify.Domain/Planify.Domain.csproj             Planify.Domain/
COPY Planify_Project/Planify.Application/Planify.Application.csproj   Planify.Application/
COPY Planify_Project/Planify.Infrastructure/Planify.Infrastructure.csproj Planify.Infrastructure/
COPY Planify_Project/Planify.API/Planify.API.csproj                   Planify.API/

# Restore NuGet packages
RUN dotnet restore Planify.API/Planify.API.csproj

# Copy toàn bộ source code
COPY Planify_Project/ .

# Publish ở chế độ Release
RUN dotnet publish Planify.API/Planify.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ─────────────────────────────────────────────────────────────
# Stage 2: Runtime (image nhỏ hơn, không cần SDK)
# ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy artifact từ stage build
COPY --from=build /app/publish .

# Expose port HTTP (ASP.NET Core mặc định dùng 8080 trong Docker)
EXPOSE 8080

ENTRYPOINT ["dotnet", "Planify.API.dll"]
