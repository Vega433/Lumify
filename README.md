# Lumify

REST API для загрузки и обработки видео: JWT, PostgreSQL, фоновый worker с FFmpeg.

## Стек

| | |
|---|---|
| Runtime | .NET 10 / ASP.NET Core |
| ORM | EF Core |
| БД | PostgreSQL |
| Auth | JWT (access + refresh) |
| Медиа | FFmpeg (FFMpegCore) |

```
src/
  Lumify.Api
  Lumify.Application
  Lumify.Domain
  Lumify.Infrastructure
```

## Требования

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- FFmpeg и FFprobe в `PATH` (`winget install --id Gyan.FFmpeg`)

Опционально путь к бинарникам:

```json
"FFmpeg": { "BinaryFolder": "D:\\Programs\\ffmpeg" }
```

## Запуск

```bash
docker compose up -d
dotnet run --project src/Lumify.Api
```

- PostgreSQL: `localhost:5433` (`lumify` / `lumify` / `lumify`)
- Swagger: http://localhost:5247
- Миграции применяются при старте API

Если схема БД устарела:

```bash
docker compose down -v && docker compose up -d
```

## API

| Метод | Путь | Описание |
|-------|------|----------|
| POST | `/api/auth/register` | Регистрация (`multipart/form-data`) |
| POST | `/api/auth/login` | Вход → `accessToken`, `refreshToken` |
| POST | `/api/auth/refresh` | Обновление токенов |
| POST | `/api/auth/logout` | Выход (нужен Bearer) |
| POST | `/api/videos` | Загрузка видео (Bearer) |
| GET | `/api/videos` | Список |
| GET | `/api/videos/{id}` | По id (статус: `Pending` / `Processing` / `Ready` / `Failed`) |
| DELETE | `/api/videos/{id}` | Удаление своего видео (Bearer) |
| GET | `/api/users` | Список пользователей |
| GET | `/api/users/{id}` | Пользователь по id |

В Swagger → **Authorize** вставляй только значение `accessToken` (без `Bearer`).

Register fields: `userName`, `email`, `password`, `birthDate` (`YYYY-MM-DD`), опционально `avatar`.
