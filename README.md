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

Схема БД при старте не создаётся: миграций в репозитории нет.

## API

| Метод | Путь | Описание |
|-------|------|----------|
| POST | `/api/auth/register` | Регистрация (`multipart/form-data`), в ответе свой email |
| POST | `/api/auth/login` | Вход → токены + свой профиль (с email) |
| POST | `/api/auth/refresh` | Обновление токенов |
| POST | `/api/auth/logout` | Выход (нужен Bearer) |
| POST | `/api/videos` | Загрузка видео (Bearer) |
| GET | `/api/videos` | Лента: `{ recommended, recent }` только `Ready` (`?limit=24`, max 48) |
| GET | `/api/videos/{id}` | По id (статус: `Pending` / `Processing` / `Ready` / `Failed`), в ответе есть `likes` |
| GET | `/api/videos/author/{id}` | Видео автора |
| GET | `/api/videos/mine` | Свои видео, все статусы (Bearer) |
| PATCH | `/api/videos/{id}` | Своё название и описание (Bearer). JSON: `title`, `description`. Пустой `description` очищает текст |
| DELETE | `/api/videos/{id}` | Удаление своего видео (Bearer) |
| POST | `/api/videos/{id}/view` | +1 просмотр. Ответ: `{ videoId, views, likes, likedByMe }` |
| POST | `/api/videos/{id}/like` | Лайк (Bearer). Повторный вызов не увеличивает счётчик |
| DELETE | `/api/videos/{id}/like` | Снять свой лайк (Bearer) |
| GET | `/api/videos/{id}/comments` | Комментарии ролика, по времени |
| POST | `/api/videos/{id}/comments` | Комментарий (Bearer). JSON: `text`, необязательный `parentCommentId` (только к верхнему комментарию) |
| PUT | `/api/comments/{id}` | Свой комментарий (Bearer). JSON: `text` |
| DELETE | `/api/comments/{id}` | Свой комментарий и ответы на него (Bearer) |
| GET | `/api/users` | Список (без email) |
| GET | `/api/users/me` | Свой профиль с email (Bearer) |
| PATCH | `/api/users/me` | Имя, почта, дата рождения, пароль (Bearer). JSON: `userName`, `email`, `birthDate` (`YYYY-MM-DD`), `currentPassword`, `newPassword` |
| PUT | `/api/users/me/avatar` | Новый аватар, `multipart`, поле `avatar` (Bearer). jpg, jpeg, png, webp |
| DELETE | `/api/users/me/avatar` | Убрать аватар (Bearer) |
| GET | `/api/users/{id}` | Публичный профиль (без email) |
| DELETE | `/api/users/{id}` | Удалить свой аккаунт (Bearer) |

В Swagger → **Authorize** вставляй только значение `accessToken` (без `Bearer`).

Register fields: `userName`, `email`, `password`, `birthDate` (`YYYY-MM-DD`), опционально `avatar`.
