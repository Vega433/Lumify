# Lumify

REST API сервиса загрузки и обработки видео.

Проект демонстрирует backend на ASP.NET Core: аутентификация JWT, работа с PostgreSQL через EF Core, асинхронная обработка медиафайлов в фоновом воркере (`BackgroundService` + `Channel`) с использованием FFmpeg.

## Возможности

- Регистрация и вход (JWT access + refresh token)
- Загрузка видео (multipart/form-data)
- Фоновая транскодировка и генерация превью
- Получение списка видео и статуса обработки (`Pending` / `Ready` / `Failed`)
- Удаление собственного видео

Документация и ручное тестирование — через **Swagger UI** (отдельного фронтенда нет).

## Технологии

| Компонент | Технология |
|-----------|------------|
| Runtime | .NET 10, ASP.NET Core |
| ORM | Entity Framework Core |
| БД | PostgreSQL |
| Auth | JWT Bearer |
| Медиа | FFmpeg (FFMpegCore) |
| Инфра | Docker Compose (только PostgreSQL) |

## Архитектура

```
src/
  Lumify.Api              HTTP-слой, Swagger, DI
  Lumify.Application      бизнес-сценарии, DTO, интерфейсы
  Lumify.Domain           сущности и value objects
  Lumify.Infrastructure   EF Core, хранилище файлов, FFmpeg, worker
```

## Требования

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- FFmpeg и FFprobe в системном `PATH`

Установка FFmpeg (Windows):

```bash
winget install --id Gyan.FFmpeg
```

После установки перезапустите терминал и убедитесь, что команды доступны:

```bash
ffmpeg -version
ffprobe -version
```

При необходимости путь к бинарникам задаётся в конфигурации:

```json
"FFmpeg": {
  "BinaryFolder": "D:\\Programs\\ffmpeg"
}
```

## Быстрый старт

Все команды выполняются из корня репозитория.

### 1. PostgreSQL

```bash
docker compose up -d
docker compose ps
```

Контейнер `lumify-db` должен быть в статусе `healthy`.  
Порт хоста: **5433** (чтобы не конфликтовать с локальным PostgreSQL на 5432).

Параметры подключения по умолчанию:

```
Host=localhost;Port=5433;Database=lumify;Username=lumify;Password=lumify
```

### 2. API

```bash
dotnet run --project src/Lumify.Api
```

При старте автоматически применяются EF-миграции.

Swagger UI: [http://localhost:5247](http://localhost:5247)

## Пример сценария

1. `POST /api/auth/register` — регистрация (multipart: `userName`, `email`, `password`, `birthDate` в формате `YYYY-MM-DD`)
2. `POST /api/auth/login` — получение `accessToken`
3. В Swagger: **Authorize** → вставить access token (без слова Bearer, если поле само добавляет схему)
4. `POST /api/videos/Upload` — загрузка файла
5. `GET /api/videos/{id}` — ожидание статуса `Ready` или `Failed`

## Ограничения текущего scope

- UI отсутствует; взаимодействие через Swagger / HTTP-клиент
- Очередь обработки — in-memory (`System.Threading.Channels`), без внешнего брокера
- Сущность Comments присутствует в модели БД, публичного API для комментариев пока нет
- JWT-секреты в `appsettings` предназначены только для локальной разработки

## Лицензия

Учебный / pet-проект. Используйте по своему усмотрению.
