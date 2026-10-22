# Imagen para desplegar AppFronton en Railway
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY AppFronton.csproj .
RUN dotnet restore AppFronton.csproj
COPY . .
RUN dotnet publish AppFronton.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production
# Railway define PORT; Program.cs lo lee
EXPOSE 8080
ENTRYPOINT ["dotnet", "AppFronton.dll"]
