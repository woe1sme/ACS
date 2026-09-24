# Wallet

Накопление начисленных комиссий, периодическая выплата, баланс и история выплат. На баланс попадают только выплаченные комиссии.

- REST: `http://localhost:5104` (в контейнере — 8080)
- БД: `wallet`, таблица `wallet_entries(status Pending|Paid)` + inbox/outbox MassTransit

## REST API

| Метод | Путь | Что делает |
|---|---|---|
| `GET` | `/users/{id}/balance` | `{ userExternalId, balance }` — сумма выплаченных комиссий. Неизвестный пользователь — 0 |
| `GET` | `/users/{id}/payouts` | История выплат: `{ items: [{ commissionId, eventExternalId, amount, paidAt }] }`, новые сверху |

## Взаимодействие

| Сообщение | Роль | Что происходит |
|---|---|---|
| `CommissionAccrued` | потребляет | создаёт запись `Pending`; повтор игнорируется (UNIQUE `commission_id`) |
| `CommissionPaid` | публикует | по одному на каждую выплаченную запись, в той же транзакции, что и выплата |

## Выплата

`PayoutWorker` раз в `Payout__Interval` переводит накопленные записи `Pending` → `Paid` пакетами по `Payout__BatchSize`:

```sql
UPDATE … SET status = 'Paid' WHERE status = 'Pending' AND id IN (… FOR UPDATE SKIP LOCKED)
```

- Каждая запись выплачивается ровно один раз, даже при нескольких репликах.
- Прерванный пакет откатывается целиком.
- Ошибка одного тика не останавливает сервис.

## Конфигурация

| Ключ | Назначение |
|---|---|
| `ConnectionStrings__Wallet` | строка подключения к Postgres |
| `RabbitMq__*`, `MessageRetry__*` | брокер и политика повторов консьюмера |
| `Payout__Interval` | период выплаты: по умолчанию `00:01:00`, в compose — `00:00:15` |
| `Payout__BatchSize` | размер пакета выплаты, по умолчанию 500 |
