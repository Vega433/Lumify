# build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 1. Копируем только файлы проектов (csproj) для кэширования restore
COPY ["src/Lumify.Domain/Lumify.Domain.csproj", "src/Lumify.Domain/"]
COPY ["src/Lumify.Application/Lumify.Application.csproj", "src/Lumify.Application/"]
COPY ["src/Lumify.Infrastructure/Lumify.Infrastructure.csproj", "src/Lumify.Infrastructure/"]
COPY ["src/Lumify.Api/Lumify.Api.csproj", "src/Lumify.Api/"]

# 2. Восстанавливаем зависимости (этот слой закэшируется, если csproj не менялись)
RUN dotnet restore "src/Lumify.Api/Lumify.Api.csproj"

# 3. Копируем всё остальное
COPY . .

# 4. Переходим в папку API и публикуем
WORKDIR /src/src/Lumify.Api
RUN dotnet publish "Lumify.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# final
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Установка FFmpeg (выполняется под пользователем root)
RUN apt-get update && \
    apt-get install -y --no-install-recommends ffmpeg && \
    rm -rf /var/lib/apt/lists/*

# Создание папок под файлы
RUN mkdir -p /app/wwwroot/uploads/temp && \
    chown -R $APP_UID:$APP_UID /app/wwwroot

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

USER $APP_UID

ENTRYPOINT ["dotnet", "Lumify.Api.dll"]