# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy solution and project files (adjust paths to match your structure)
COPY ["Scribble/Scribble API/Scribble API.csproj", "Scribble API/"]
COPY ["Scribble/Scribble.Business/Scribble.Business.csproj", "Scribble.Business/"]
COPY ["Scribble/Scribble.Repository/Scribble.Repository.csproj", "Scribble.Repository/"]

# Restore dependencies
RUN dotnet restore "Scribble API/Scribble API.csproj"

# Copy everything else
COPY Scribble/ .

# Build the application
WORKDIR "/src/Scribble API"
RUN dotnet build "Scribble API.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Scribble API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final
WORKDIR /app

# Create directory for SQLite database
RUN mkdir -p /app/data

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Scribble API.dll"]