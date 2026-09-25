# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY CreditosApp.csproj ./
RUN dotnet restore CreditosApp.csproj
COPY . .
RUN dotnet publish CreditosApp.csproj -c Release -o /app/publish --no-restore

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
# Carpeta del disco persistente de Render (SQLite)
RUN mkdir -p /var/data
# Render inyecta PORT en tiempo de ejecución. Se expande AQUÍ con sh -c
# (no se asume que ${PORT} se expanda dentro de una variable de entorno).
CMD ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} exec dotnet CreditosApp.dll"]
