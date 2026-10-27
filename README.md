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

## Конфиг и секреты

Секреты не лежат в git. Скопируй примеры и подставь свои значения:

```bash
cp .env.example .env
```

PowerShell: `Copy-Item .env.example .env`

- `.env` / `.env.example` — Postgres, connection string, JWT (`Jwt__Key` ≥ 32 символов)
- `src/Lumify.Api/appsettings.Example.json` — тот же набор в формате ASP.NET Core
- `appsettings.json` содержит только несекретные значения (issuer/audience, логирование)

Локальный `.env` подхватывается при старте API и `docker compose`.

Опционально путь к FFmpeg: `FFmpeg__BinaryFolder` в `.env` или:

```json
"FFmpeg": { "BinaryFolder": "D:\\Programs\\ffmpeg" }
```

## Запуск

```bash
cp .env.example .env
docker compose up -d
dotnet run --project src/Lumify.Api
```

- PostgreSQL: `localhost:${POSTGRES_PORT}` (по умолчанию 5433)
- Swagger: http://localhost:5247
- Миграции применяются при старте API

Если схема БД устарела:

```bash
docker compose down -v && docker compose up -d
```

## API

| Метод | Путь | Описание |
|-------|------|----------|
| POST | `/api/auth/register` | Регистрация (`multipart/form-data`), в ответе свой email |
| POST | `/api/auth/login` | Вход → токены + свой профиль (с email) |
| POST | `/api/auth/refresh` | Обновление токенов |
| POST | `/api/auth/logout` | Выход (нужен Bearer) |
| POST | `/api/videos` | Загрузка видео (Bearer) |
| GET | `/api/videos` | Список |
| GET | `/api/videos/{id}` | По id (статус: `Pending` / `Processing` / `Ready` / `Failed`) |
| DELETE | `/api/videos/{id}` | Удаление своего видео (Bearer) |
| GET | `/api/users` | Список (без email) |
| GET | `/api/users/me` | Свой профиль с email (Bearer) |
| GET | `/api/users/{id}` | Публичный профиль (без email) |

В Swagger → **Authorize** вставляй только значение `accessToken` (без `Bearer`).

Register fields: `userName`, `email`, `password`, `birthDate` (`YYYY-MM-DD`), опционально `avatar`.
