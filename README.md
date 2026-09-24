# ACS — система партнёрских отчислений

Набор микросервисов на .NET 8, который начисляет и выплачивает партнёрские комиссии по иерархии пользователей.

| Сервис | Отвечает за | REST (снаружи) | gRPC (внутри) |
|---|---|---|---|
| **Partners** | пользователи и дерево партнёров | `:5101` | `:8081` `GetAncestors` |
| **Activity** | приём событий прибыли/убытка, просмотр событий | `:5102` | — |
| **Commission** | схемы начисления, расчёт и хранение комиссий | `:5103` | `:8081` `GetCommissionsForEvent` |
| **Wallet** | накопление, периодическая выплата, баланс, история | `:5104` | — |

У каждого сервиса своя БД Postgres. Асинхронное взаимодействие идёт через RabbitMQ (MassTransit с transactional outbox), синхронные внутренние чтения — через gRPC. Архитектура и принятые решения описаны в [ARCHITECTURE.md](ARCHITECTURE.md).

## Быстрый старт

Нужен Docker (Compose v2).

```bash
docker compose up --build -d
docker compose ps          # все контейнеры должны стать healthy
./scripts/smoke-test.sh    # сквозная проверка (bash, curl, python3)
```

Сквозная проверка строит цепочку из пяти пользователей и проверяет:

- расчёт по схемам Linear и Fibonacci;
- переключение схемы: старые комиссии не пересчитываются;
- идемпотентность приёма событий;
- что убыток не даёт комиссий;
- выплату и баланс кошелька.

В compose выплата запускается раз в 15 секунд (`Payout__Interval`), по умолчанию — раз в минуту.

Наружу публикуются порты:

- 5101–5104 — REST сервисов;
- 5672 и 15672 — RabbitMQ (management UI: `guest` / `guest`).

Базы данных и gRPC-порты наружу не публикуются.

## API

Ошибки возвращаются в формате RFC 9457 Problem Details с `traceId`. Коды: 400 — валидация, 404 — не найдено, 409 — конфликт, 503 — зависимость недоступна. Списки пагинируются параметрами `page` (≥ 1) и `pageSize` (1–100, по умолчанию 50); ответ имеет вид `{ items, page, pageSize, total }`.

### Partners — `http://localhost:5101`

| Метод | Путь | Что делает |
|---|---|---|
| `POST` | `/users` | Добавить пользователя: `{ "externalId": "u2", "parentExternalId": "u1" }` (родитель необязателен). 201 — создан; 200 — уже есть с тем же родителем; 409 — уже есть с другим родителем |
| `PUT` | `/users/{id}/parent` | Установить или сменить партнёра: `{ "parentExternalId": "u5" }`. Поддерево переезжает целиком; цикл даёт 409 |
| `GET` | `/users/{id}/ancestors` | Ветка вверх: `[{ externalId, level }]`, где level 1 — прямой партнёр |
| `GET` | `/users/{id}/descendants` | Ветка вниз: `[{ externalId, parentExternalId, level }]` |

`externalId` пользователя — `^[A-Za-z0-9_]{1,64}$`.

### Activity — `http://localhost:5102`

| Метод | Путь | Что делает |
|---|---|---|
| `POST` | `/events` | Принять событие: `{ "externalEventId": "e1", "userExternalId": "u4", "profit": 100.5 }`. 201 — принято; 200 — повтор с теми же данными; 409 — тот же id с другими данными |
| `GET` | `/users/{id}/events` | События пользователя без комиссий |
| `GET` | `/events/{eventId}` | Деталь события со всеми комиссиями: кому, сколько, по какой схеме, выплачено ли |

### Commission — `http://localhost:5103`

| Метод | Путь | Что делает |
|---|---|---|
| `GET` | `/admin/scheme` | Текущая схема |
| `POST` | `/admin/scheme` | Переключить схему: `{ "schemeType": "Fibonacci" }` или `"Linear"`. Действует только на новые расчёты |

### Wallet — `http://localhost:5104`

| Метод | Путь | Что делает |
|---|---|---|
| `GET` | `/users/{id}/balance` | Баланс — сумма **выплаченных** комиссий |
| `GET` | `/users/{id}/payouts` | История выплат: какие комиссии и когда выплачены |

### Пример

```bash
curl -X POST localhost:5101/users -H 'Content-Type: application/json' -d '{"externalId":"alice"}'
curl -X POST localhost:5101/users -H 'Content-Type: application/json' -d '{"externalId":"bob","parentExternalId":"alice"}'
curl -X POST localhost:5101/users -H 'Content-Type: application/json' -d '{"externalId":"carol","parentExternalId":"bob"}'

curl -X POST localhost:5102/events -H 'Content-Type: application/json' \
  -d '{"externalEventId":"ev-1","userExternalId":"carol","profit":200}'

curl localhost:5102/events/ev-1          # Linear: bob — 2 (L1), alice — 4 (L2)
sleep 20
curl localhost:5104/users/alice/balance  # 4 после выплаты
```

### Эксплуатация

У каждого сервиса есть эндпоинты:

- `/health/live` — процесс жив;
- `/health/ready` — зависимости доступны (у Activity — только БД, см. ARCHITECTURE.md);
- `/metrics` — метрики в формате Prometheus.

Логи структурированные (Serilog), в каждой строке есть сервис и trace id.

## Разработка

Нужен .NET SDK 8. Сервисы подключают `BuildingBlocks.Contracts` пакетом из локального feed `local-nuget-feed/`, поэтому перед первой сборкой пакет нужно собрать:

```bash
./scripts/pack-contracts.sh      # Windows: ./scripts/pack-contracts.ps1
dotnet build ACS.sln
dotnet test ACS.sln
```

Unit-тесты покрывают:

- расчёт комиссий по обеим схемам, округление, переполнение, расширяемость схем (`Commission.Tests`);
- дерево и use case'ы Partners (`Partners.Tests`);
- доменные правила Activity и Wallet.

Чтобы запустить сервис вне Docker, поднимите в compose только инфраструктуру и запустите сервис с `ASPNETCORE_ENVIRONMENT=Development`. `appsettings.Development.json` ожидает Postgres на `localhost:5433`, RabbitMQ — на `localhost:5672`. REST-порты — 5101–5104, gRPC — 6101 и 6103; адреса gRPC задаются через `Partners__GrpcAddress` и `Commission__GrpcAddress`.

Миграции EF Core применяются при старте сервиса. Новая миграция создаётся так:

```bash
dotnet ef migrations add <Name> -p src/Services/<Service>/<Service>.Api -o Infrastructure/Migrations
```

## Структура

```text
src/BuildingBlocks/Contracts        события ProfitPosted, CommissionAccrued, CommissionPaid (NuGet-пакет)
src/BuildingBlocks/ServiceDefaults  логи, трейсинг, метрики, health, ошибки, MassTransit + outbox, миграции, resilience
src/Services/<Service>/<Service>.Api  Domain / Application / Infrastructure / Api
protos/                             gRPC-контракты
tests/<Service>.Tests               unit-тесты
scripts/                            упаковка контрактов, сквозная проверка
```

## Допущения

- Событие содержит `ExternalId` владельца, `ExternalId` события и `Profit`. Время приёма ставит сервис.
- Партнёрскую связь можно менять. Уже начисленные комиссии при смене связи или схемы не пересчитываются.
- Глубина дерева искусственно не ограничена (Postgres `ltree`), хотя ТЗ разрешает ограничить её 10 уровнями.
- Суммы — `numeric(18,4)`. Комиссия округляется до 4 знаков по правилу «от нуля»; комиссия, округлившаяся до 0, не создаётся.
- Фибоначчи: F(1)=1, F(2)=1, F(3)=2, F(4)=3, F(5)=5…
- Схема применяется в момент расчёта комиссии, а не в момент приёма события. Событие, принятое прямо перед переключением, может быть рассчитано уже по новой схеме; в самой комиссии записана применённая схема.
- Аутентификация и авторизация (включая админский эндпоинт) — задача API Gateway, который в объём решения не входит.
- Activity не проверяет существование пользователя. Событие по неизвестному пользователю не даст комиссий: после повторов его сообщение уйдёт в `_error`-очередь Commission.
