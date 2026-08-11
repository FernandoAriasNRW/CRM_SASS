# Use the .NET 9 SDK image to build and publish the app
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy the entire solution and source code
COPY . .

# Restore dependencies for the main ApiHost project
RUN dotnet restore "src/Host/ApiHost/ApiHost.csproj"

# Build and publish the application
WORKDIR "/src/src/Host/ApiHost"
RUN dotnet publish "ApiHost.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Use the ASP.NET Core runtime image for the final stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Expose port 8080
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "ApiHost.dll"]
