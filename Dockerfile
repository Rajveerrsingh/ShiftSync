FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/ShiftSync.Domain/ShiftSync.Domain.csproj src/ShiftSync.Domain/
COPY src/ShiftSync.Application/ShiftSync.Application.csproj src/ShiftSync.Application/
COPY src/ShiftSync.Infrastructure/ShiftSync.Infrastructure.csproj src/ShiftSync.Infrastructure/
COPY src/ShiftSync.Api/ShiftSync.Api.csproj src/ShiftSync.Api/
RUN dotnet restore src/ShiftSync.Api/ShiftSync.Api.csproj
COPY src/ src/
RUN dotnet publish src/ShiftSync.Api/ShiftSync.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER root
RUN mkdir -p /data && chown -R app:app /data
USER app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Jwt__Key=dev-only-shiftsync-key-change-for-real-use \
    ConnectionStrings__Default="Data Source=/data/shiftsync.db"
EXPOSE 8080
VOLUME /data
ENTRYPOINT ["dotnet", "ShiftSync.Api.dll"]
