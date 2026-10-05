FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
# Run as the image's built-in non-root user (port 8080 needs no root privileges).
USER $APP_UID
ENTRYPOINT ["dotnet", "Ibernia.Assessment.Api.dll"]
