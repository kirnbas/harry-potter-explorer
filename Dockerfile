# Harry Potter Explorer — production image.
#
# Two stages: the SDK compiles and publishes, the (much smaller) ASP.NET runtime image
# ships. Works unchanged on Render, Railway, Fly.io and any other container host.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, on its own layer, so a source-only change does not re-download packages.
COPY HarryPotterExplorer/HarryPotterExplorer.csproj HarryPotterExplorer/
RUN dotnet restore HarryPotterExplorer/HarryPotterExplorer.csproj

COPY . .
RUN dotnet publish HarryPotterExplorer/HarryPotterExplorer.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# The SQLite file is written here. On a host with ephemeral disks the database is simply
# recreated and re-seeded from the public API on the next start; mount a volume here if
# you want the collection tally to survive a redeploy.
RUN mkdir -p /app/App_Data
VOLUME ["/app/App_Data"]

ENV ASPNETCORE_ENVIRONMENT=Production
# PORT is honoured at startup (see Program.cs); 8080 is the fallback for plain `docker run`.
ENV PORT=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "HarryPotterExplorer.dll"]
