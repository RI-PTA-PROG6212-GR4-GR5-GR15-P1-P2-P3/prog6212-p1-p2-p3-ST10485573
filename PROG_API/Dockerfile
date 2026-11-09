FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["PROG_API/PROG_API.csproj", "PROG_API/"]
RUN dotnet restore "PROG_API/PROG_API.csproj"
COPY . .
WORKDIR "/src/PROG_API"
RUN dotnet build "PROG_API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PROG_API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PROG_API.dll"]