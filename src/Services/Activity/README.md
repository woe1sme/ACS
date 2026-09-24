# Activity

Идемпотентный приём событий прибыли/убытка и их просмотр. Публикует `ProfitPosted` для положительной прибыли.

- REST: `http://localhost:5102` (в контейнере — 8080)
- БД: `activity`, таблица `profit_events` + таблицы outbox MassTransit

## REST API

| Метод | Путь | Что делает |
|---|---|---|
| `POST` | `/events` | Принять событие: `{ "externalEventId": "e1", "userExternalId": "u4", "profit": 100.5 }`. 201 — принято; 200 — повтор с теми же данными; 409 — тот же id с другими данными; 400 — больше 4 знаков после запятой или неверные поля |
| `GET` | `/users/{id}/events` | События пользователя без комиссий, новые сверху. Неизвестный пользователь — пустой список |
| `GET` | `/events/{eventId}` | Событие со всеми комиссиями: кому, сколько, уровень, схема, выплачено ли. 404 — события нет; 503 — Commission недоступен |

Пример ответа детали:

```json
{
  "externalEventId": "e1", "userExternalId": "u4", "profit": 100.0, "receivedAt": "…",
  "commissions": [
    { "commissionId": "…", "beneficiaryExternalId": "u3", "level": 1, "amount": 1.0,
      "schemeType": "Linear", "calculatedAt": "…", "isPaid": true, "paidAt": "…" }
  ]
}
```

## Взаимодействие

- **Публикует** `ProfitPosted { EventExternalId, UserExternalId, Profit, OccurredAt }`, только если `Profit > 0`. Сообщение пишется в outbox в той же транзакции, что и событие, поэтому при недоступном брокере приём продолжает работать.
- **Вызывает** Commission по gRPC `GetCommissionsForEvent` для детали события. Для этого вызова настроены таймаут, повторы и circuit breaker.

## Как устроено

- Идемпотентность — по `external_event_id` (PK). Гонку двух одинаковых запросов разрешает перехват нарушения PK.
- Событие неизменяемо. Существование пользователя не проверяется: Activity не владеет пользователями.
- `/health/ready` проверяет только БД: приём событий не зависит от брокера.

## Конфигурация

| Ключ | Назначение |
|---|---|
| `ConnectionStrings__Activity` | строка подключения к Postgres |
| `RabbitMq__Host`, `__Username`, `__Password` | брокер |
| `Commission__GrpcAddress` | адрес gRPC Commission, например `http://commission-api:8081` |
| `Commission__DeadlineSeconds`, `Commission__Resilience__*` | дедлайн вызова, повторы и circuit breaker |
