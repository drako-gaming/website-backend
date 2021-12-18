FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS build

WORKDIR /app

COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine

WORKDIR /app
COPY --from=build /app/out .
ENTRYPOINT ["dotnet", "Drako.Api.Database.dll"]
