FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Копируем файлы проектов
COPY ["Bot/Bot.csproj", "Bot/"]
COPY ["Data/Data.csproj", "Data/"]
COPY ["Services/Services.csproj", "Services/"]
COPY ["Configurations/Configurations.csproj", "Configurations/"]
RUN dotnet restore "Bot/Bot.csproj"

# Копируем весь код и публикуем
COPY . .
WORKDIR "/src/Bot"
RUN dotnet publish "Bot.csproj" -c Release -o /app/publish

# Финальный образ
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Bot.dll"]