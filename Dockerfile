FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["OhMyGPA Telegram Bot/OhMyGPA Telegram Bot.csproj", "OhMyGPA Telegram Bot/"]
RUN dotnet restore "OhMyGPA Telegram Bot/OhMyGPA Telegram Bot.csproj"
COPY . .
WORKDIR "/src/OhMyGPA Telegram Bot"
RUN dotnet build "OhMyGPA Telegram Bot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OhMyGPA Telegram Bot.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OhMyGPA Telegram Bot.dll"]
