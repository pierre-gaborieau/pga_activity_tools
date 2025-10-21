# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copier le fichier .csproj et restaurer les dépendances
COPY pgaActivityTools/pgaActivityTools.csproj pgaActivityTools/
RUN dotnet restore "pgaActivityTools/pgaActivityTools.csproj"

# Copier tout le code source
COPY pgaActivityTools/ pgaActivityTools/

# Build l'application
WORKDIR /src/pgaActivityTools
RUN dotnet build "pgaActivityTools.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "pgaActivityTools.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Créer un utilisateur non-root pour la sécurité
RUN groupadd -r appuser && useradd -r -g appuser appuser
RUN chown -R appuser:appuser /app

# Copier les fichiers publiés
COPY --from=publish /app/publish .

# Changer vers l'utilisateur non-root
USER appuser

# Exposer le port
EXPOSE 8080

# Variables d'environnement par défaut
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Démarrer l'application
ENTRYPOINT ["dotnet", "pgaActivityTools.dll"]