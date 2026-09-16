FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/TabularDb.GrpcServer -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV Kestrel__Endpoints__Grpc__Url=http://+:8080 \
    Kestrel__Endpoints__GrpcWeb__Url=http://+:8081 \
    Storage__DataDir=/data
VOLUME /data
EXPOSE 8080 8081
ENTRYPOINT ["dotnet", "TabularDb.GrpcServer.dll"]
