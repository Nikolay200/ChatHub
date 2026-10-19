#docker run -d --name chat-container -p 8080:8080 chat-app

# Этап сборки
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Копируем csproj и восстанавливаем зависимости (для кэширования)
COPY *.csproj .
RUN dotnet restore

# Копируем всё остальное и публикуем
COPY . .
RUN dotnet publish -c Release -o /app

# Этап выполнения
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Chat.dll"]