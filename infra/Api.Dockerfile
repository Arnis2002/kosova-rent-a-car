FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY apps/api/ ./
RUN dotnet publish -c Release -o /out
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
COPY apps/api/Sql/ ./Sql/
ENV ASPNETCORE_URLS=http://+:8080
RUN mkdir -p /data/keys /data/documents && chown -R $APP_UID /data
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet","Kosova.Api.dll"]

