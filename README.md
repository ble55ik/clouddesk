# CloudDesk

Учебный сервис заявок технической поддержки на ASP.NET Core Minimal API и PostgreSQL. Приложение и база данных запускаются в отдельных Docker-контейнерах через Docker Compose.

## Запуск приложения

```
cp .env.example .env
docker compose up -d --build
```

Доступные сервисы:

- `api` — HTTP API на порту 8080 (в Codespaces открывается через вкладку Ports)
- `db` — PostgreSQL 16, порт наружу не публикуется, данные лежат в томе `pgdata`

Проверить состояние контейнеров:

```
docker compose ps
docker compose logs -f api
curl -i http://localhost:8080/health
```

Остановить:

```
docker compose down
```

## HTTP-маршруты

| Метод | Адрес | Назначение |
|-------|-------|------------|
| GET | `/api/tickets` | Все заявки; фильтры `status` и `priority` в query-параметрах |
| GET | `/api/tickets/{id}` | Одна заявка; если нет — 404 |
| POST | `/api/tickets` | Создание заявки; ответ 201 и данные записи с ID |
| PUT | `/api/tickets/{id}` | Изменение title, description, priority и status |
| DELETE | `/api/tickets/{id}` | Удаление; 204 при успехе, 404 если заявки нет |
| GET | `/api/stats` | Сколько всего заявок и сколько в каждом статусе |
| GET | `/health` | 200, когда API и база доступны |

Пример тела для POST и PUT:

```json
{
  "title": "Не работает клавиатура",
  "description": "Не реагирует ни одна клавиша",
  "priority": 2,
  "status": "new"
}
```

Ограничения: `title` — 3–120 символов, `priority` — 1–3, `status` — `new`, `in_progress` или `done`. Нарушение возвращает 400.

## Как проверить сохранение данных

1. Добавьте заявку и запомните ID из ответа:

```
curl -i -X POST http://localhost:8080/api/tickets \
  -H "Content-Type: application/json" \
  -d '{"title":"Проверка тома","description":"Данные должны пережить перезапуск","priority":1}'
```

2. Перезапустите сервисы без удаления тома:

```
docker compose down
docker compose up -d
curl -i http://localhost:8080/api/tickets/{id}
```

Заявка остаётся на месте — она хранится в томе `pgdata`, а не внутри контейнера.

3. Проверка недоступности базы:

```
docker stop clouddesk-db-1
curl -i http://localhost:8080/health
```

Пока база остановлена, `/health` отвечает 503. После `docker start clouddesk-db-1` снова будет 200.

4. Удалить все данные вместе с томом:

```
docker compose down -v
```

При следующем запуске `init.sql` заново создаст таблицу и наполнит её тестовыми заявками.
