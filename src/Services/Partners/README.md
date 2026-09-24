# Partners

Пользователи и дерево партнёров. К брокеру не подключён: только REST наружу и gRPC для Commission.

- REST: `http://localhost:5101` (в контейнере — 8080)
- gRPC: порт 8081, только внутри сети compose
- БД: `partners`, таблица `users(external_id, parent_external_id, path ltree)`

## REST API

| Метод | Путь | Что делает |
|---|---|---|
| `POST` | `/users` | Добавить пользователя: `{ "externalId": "u2", "parentExternalId": "u1" }`, родитель необязателен. 201 — создан; 200 — уже есть с тем же родителем; 400 — родитель не найден; 409 — уже есть с другим родителем |
| `PUT` | `/users/{id}/parent` | Установить или сменить партнёра: `{ "parentExternalId": "u5" }`. Поддерево переезжает целиком. 404 — пользователь не найден; 409 — получился бы цикл |
| `GET` | `/users/{id}/ancestors` | Ветка вверх: `{ items: [{ externalId, level }] }`, level 1 — прямой партнёр |
| `GET` | `/users/{id}/descendants` | Ветка вниз: `{ items: [{ externalId, parentExternalId, level }] }`, порядок — по level, затем по id |

`externalId` — `^[A-Za-z0-9_]{1,64}$`: значение используется как метка `ltree`.

## gRPC

`acs.partners.v1.Partners/GetAncestors(user_external_id)` → полная цепочка предков с уровнями, без пагинации. Контракт — `protos/partners/partners.proto`. Коды: `NOT_FOUND`, `INVALID_ARGUMENT`, `UNAVAILABLE`.

## Как устроено

- Предки берутся из `path` пользователя, без рекурсивных запросов.
- Потомки выбираются через `path <@ …` по GiST-индексу.
- Смена партнёра переписывает пути всего поддерева одним `UPDATE` в транзакции.
- Создание с родителем и смена партнёра сериализуются `pg_advisory_xact_lock`, поэтому два параллельных перемещения не могут образовать цикл.

## Конфигурация

| Ключ | Назначение |
|---|---|
| `ConnectionStrings__Partners` | строка подключения к Postgres |
| `Database__CommandTimeoutSeconds` | таймаут SQL-команд, по умолчанию 30 |
