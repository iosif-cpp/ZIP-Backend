FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
WORKDIR /src/Kaspersky-Task1
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV HTTP_PORTS=5278
EXPOSE 5278
ENTRYPOINT ["dotnet", "Kaspersky-Task1.dll"]

