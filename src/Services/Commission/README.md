# Commission

Схемы начисления, расчёт и хранение комиссий. Каждая комиссия хранит `scheme_type` — схему, по которой она рассчитана.

- REST: `http://localhost:5103` (в контейнере — 8080)
- gRPC: порт 8081, только внутри сети compose
- БД: `commission`, таблицы `commissions`, `commission_scheme_settings` + inbox/outbox MassTransit

## Схемы

Для уровня L (1 — прямой партнёр владельца события):

| Схема | Сумма |
|---|---|
| `Linear` | `L × Profit / 100` |
| `Fibonacci` | `F(L) × Profit / 100`, где F(1)=1, F(2)=1, F(3)=2, F(4)=3, F(5)=5… |

- Сумма округляется до 4 знаков по правилу «от нуля». Комиссия, округлившаяся до 0, не создаётся.
- Комиссии начисляются только при `Profit > 0`.
- Переполнение суммы — ошибка без повторов: сообщение сразу уходит в `_error`.
- Новая схема добавляется одним классом `ICommissionScheme` и одной регистрацией в DI.

## REST API

| Метод | Путь | Что делает |
|---|---|---|
| `GET` | `/admin/scheme` | Текущая схема: `{ "schemeType": "Linear" }` |
| `POST` | `/admin/scheme` | Переключить схему: `{ "schemeType": "Fibonacci" }`. Действует только на новые расчёты. 400 — неизвестная схема |

## gRPC

`acs.commission.v1.Commission/GetCommissionsForEvent(event_external_id)` → все комиссии события по уровням. Сумма передаётся строкой без потери точности. Контракт — `protos/commission/commission.proto`.

## Взаимодействие

| Сообщение | Роль | Что происходит |
|---|---|---|
| `ProfitPosted` | потребляет | получает цепочку у Partners (gRPC `GetAncestors`), читает активную схему, одной транзакцией сохраняет комиссии и `CommissionAccrued` |
| `CommissionAccrued` | публикует | по одному на каждую комиссию |
| `CommissionPaid` | потребляет | помечает комиссию оплаченной; первый `paid_at` не перезаписывается |

Защита от двойного начисления:

1. inbox MassTransit;
2. проверка, есть ли уже комиссии события;
3. UNIQUE `(event_external_id, beneficiary_external_id)`.

Если Partners недоступен, сообщение повторяется с экспоненциальной задержкой. Повторов на самом gRPC-вызове нет, есть только таймаут и circuit breaker.

## Конфигурация

| Ключ | Назначение |
|---|---|
| `ConnectionStrings__Commission` | строка подключения к Postgres |
| `RabbitMq__*`, `MessageRetry__*` | брокер и политика повторов консьюмеров |
| `Partners__GrpcAddress` | адрес gRPC Partners, например `http://partners-api:8081` |
| `Partners__DeadlineSeconds`, `Partners__Resilience__*` | дедлайн, таймауты и circuit breaker вызова Partners |
