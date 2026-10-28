# Архитектура Send Files

## Обзор

```
┌──────────── ПК (Windows) ─────────────────────────────────────────────┐
│ SendFiles.App (WPF)                                                   │
│   окно-карточка, трей, тосты, single-instance, брандмауэр, меню        │
│   внешний Generic Host: настройки, Serilog, ViewModel                  │
│        │ использует только публичный фасад                             │
│        ▼                                                              │
│ SendFiles.Core (net10.0, без WPF)                                     │
│   SendFilesServer ── ServerSupervisor ── внутренний WebApplication    │
│   (Kestrel на конкретных частных IP; пересоздаётся при смене сети)    │
│   Pairing · Devices · Uploads · Outbox · Text · Events · History      │
│        │ статика из ресурсов                                          │
│ SendFiles.Web (HTML/CSS/JS как EmbeddedResource)                      │
└───────────────────────────────────────────────────────────────────────┘
            ▲ HTTP + WebSocket (LAN, частные адреса)
┌───────────┴───────── Телефон ──────────┐
│ Safari / Chrome: веб-страница из Web   │
└────────────────────────────────────────┘
```

`SendFiles.Core` не ссылается на WPF: в промпте 4 его встроят в приложение Dynamic Island.
Хост получает фасад `SendFilesServer` и реализует один интерфейс — `IPairingApprover`
(показать запрос «Разрешить устройство?» и вернуть ответ). Переход на UI-поток делает хост.

## Модули Core

| Namespace | Что внутри |
|---|---|
| `Hosting` | `SendFilesServer` (фасад, `IAsyncDisposable`, `Events`), `ServerSupervisor` (жизненный цикл Kestrel, перепривязка), `SendFilesOptions` |
| `Networking` | `AdapterInfo`, `NetworkAdapterSelector` (чистая функция, тесты на фейковых адаптерах), `SystemAdapters`, `NetworkChangeMonitor` (debounce), `PortSelector` |
| `Security` | `HostValidationMiddleware`, `OriginCheck`, `SecurityHeadersMiddleware`, аутентификация по ключу, `TicketService`, политики rate limit |
| `Pairing` | `PairingService` (QR-токен, заявки, 4-значный код), `IPairingApprover` |
| `Devices` | `DeviceStore` (JSON, хэши ключей), `DeviceKey` |
| `Uploads` | `UploadSessionStore` (на диске), `ChunkWriter` (CRC32), `UploadFinalizer` (SHA-256, MotW, атомарный move), `TempJanitor` |
| `Files` | `FileNameSanitizer`, `SafePathGuard`, `MarkOfTheWeb` |
| `Outbox` | файлы/папки, предложенные телефону; `ZipStreamWriter` (stored, ZIP64) |
| `Events` | `EventHub` (на каждое подключение ограниченный `Channel<T>`), WebSocket и SSE |
| `Text` | обмен текстом (в памяти, последние 50, до 64 КБ) |
| `History` | история передач (JSON, без текстов) |
| `Persistence` | `DataDirectory`, `AtomicJsonFile<T>` |

## Жизненный цикл сервера

1. App поднимает внешний Generic Host: настройки, хранилища, `SendFilesServer` — синглтоны.
2. `ServerSupervisor` выбирает адреса (`NetworkAdapterSelector`) и порт (запомненный, иначе свободный),
   создаёт **внутренний** `WebApplication` и слушает **конкретные IP**, не `0.0.0.0`.
3. Смена сети → debounce 1,5 с → новый набор адресов. Если он не изменился, ничего не происходит;
   иначе старый Kestrel останавливается (3 с), новый стартует на том же порту, обновляется
   список разрешённых `Host`, перевыпускается QR-токен, `ServerStateChanged` → новый QR.
   Незавершённые загрузки докачиваются по `uploadId`.

## HTTP-протокол (v1)

Аутентификация: `Authorization: Bearer sf1_<deviceId>_<secret>`. Ошибки — `application/problem+json`
со стабильным `code`. Для `<a href>`, `WebSocket`, `EventSource`, которые не умеют заголовки,
выдаются короткие **билеты** (`POST /api/v1/tickets`), привязанные к устройству и цели.

| Метод и путь | Auth | Назначение |
|---|---|---|
| GET `/`, `/pair`, `/assets/*`, `/manifest.webmanifest` | — | статика |
| POST `/api/v1/pair/start` | QR-токен | заявка на сопряжение → `requestId`, `requestSecret`, `code` |
| POST `/api/v1/pair/wait` | requestSecret | long-poll 25 с → `pending` / `approved` + ключ / `denied` / `expired` |
| POST `/api/v1/pair/cancel` | requestSecret | отмена заявки |
| GET / DELETE `/api/v1/me` | ключ | кто я, лимиты / отвязать себя |
| POST `/api/v1/tickets` | ключ | билет `events` (30 с, одноразовый) или `download` (10 мин) |
| POST `/api/v1/uploads` | ключ | начать загрузку → `uploadId`, `chunkSize`, `receivedChunks` |
| GET `/api/v1/uploads[/{id}]` | ключ | активные загрузки / состояние для докачки |
| PUT `/api/v1/uploads/{id}/chunks/{index}` | ключ | чанк, заголовок `X-Chunk-Crc32` |
| POST `/api/v1/uploads/{id}/complete` | ключ | проверка, SHA-256, MotW, переименование |
| DELETE `/api/v1/uploads/{id}` | ключ | отмена |
| GET `/api/v1/outbox` | ключ | файлы с ПК |
| GET/HEAD `/dl/{ticket}` | билет | скачивание, Range 206/416 |
| POST / GET `/api/v1/text` | ключ | текст в обе стороны |
| GET `/api/v1/ws?ticket=` | билет + Origin | события |
| GET `/api/v1/events?ticket=` | билет | SSE (запасной канал) |

Состояния загрузки: `Created → Receiving → Ready → Finalizing → Completed`, а также `Cancelled`,
`Failed`, `Expired` (24 ч без активности). Чанк — 4 МиБ (выбирает сервер), не более 3 файлов
параллельно на устройство и 8 на сервер.

## Конвейер запроса

ProblemDetails → **HostValidation** (защита от DNS rebinding, до статики) → SecurityHeaders →
RateLimiter → StaticFiles → Routing → Auth → **OriginCheck** (не-GET и WS) → Endpoints.
CORS не подключается. Заголовки: строгий CSP без inline, `Referrer-Policy: no-referrer`,
`X-Content-Type-Options: nosniff`, `Cache-Control: no-store` на `/api`.

## Сопряжение

1. ПК держит один QR-токен (32 байта, 5 мин, одноразовый; хранится только хэш).
2. Телефон открывает `/pair#t=…`, страница сразу убирает фрагмент (`history.replaceState`) и
   отправляет токен через `POST /pair/start`.
3. Сервер гасит токен, создаёт заявку и код из 4 цифр (`RandomNumberGenerator.GetInt32(10000)`);
   одновременно одна заявка.
4. ПК показывает «Разрешить «iPhone (Safari)»? Код 1234», таймаут 120 с.
5. Телефон ждёт через long-poll с `requestSecret`; при одобрении один раз получает ключ
   `sf1_<deviceId>_<secret>`. ПК хранит `SHA-256(secret)`, сравнение в постоянное время.

## Данные

Папка данных: `%LOCALAPPDATA%\SendFiles.Data` (Velopack занимает и удаляет `%LOCALAPPDATA%\SendFiles`).
`SENDFILES_DATA_DIR` переопределяет путь; Debug-сборка по умолчанию пишет в `SendFiles.Data-dev`.

| Файл | Содержимое |
|---|---|
| `settings.json` | папка приёма, порт, язык, автозапуск, разрешение виртуальных адаптеров |
| `devices.json` | доверенные устройства: id, имя, хэш ключа, даты |
| `uploads/{id}.json` | состояние загрузки (bitmap чанков) |
| `<папка приёма>\.sendfiles-tmp\{id}.part` | данные загрузки на том же томе |
| `history.json` | последние 500 передач |
| `logs\` | Serilog, 7 дней, без имён файлов, текстов и ключей |

Запись — атомарная: временный файл → `Flush(true)` → `File.Move(overwrite)`.

## Фронтенд телефона

Чистые HTML/CSS/JS без сборщика. Данные в DOM — только через `textContent`. Загрузка — XHR
(у `fetch` нет прогресса отправки), CRC32 на JS. Докачка по `visibilitychange`/`pageshow`;
`uploadId` хранится в `localStorage` по отпечатку `name|size|lastModified`.
