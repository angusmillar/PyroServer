FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Abm.Pyro.Api/Abm.Pyro.Api.csproj", "Abm.Pyro.Api/"]
COPY ["Abm.Pyro.Application/Abm.Pyro.Application.csproj", "Abm.Pyro.Application/"]
COPY ["Abm.Pyro.Domain/Abm.Pyro.Domain.csproj", "Abm.Pyro.Domain/"]
COPY ["Abm.Pyro.Repository/Abm.Pyro.Repository.csproj", "Abm.Pyro.Repository/"]
RUN dotnet restore "Abm.Pyro.Api/Abm.Pyro.Api.csproj"
COPY . .
WORKDIR "/src/Abm.Pyro.Api"
RUN dotnet build "Abm.Pyro.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Abm.Pyro.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Abm.Pyro.Api.dll"]
