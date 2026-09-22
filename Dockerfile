FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/TabularDb.WebApi -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080 \
    Storage__DataDir=/data
VOLUME /data
EXPOSE 8080
ENTRYPOINT ["dotnet", "TabularDb.WebApi.dll"]
