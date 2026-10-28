# Send Files

**RU** · [EN](#english)

Быстрая передача файлов между iPhone/Android и Windows в одной сети — как AirDrop, но без установки
чего-либо на телефон. На ПК работает небольшое приложение со встроенным веб-сервером; телефон
сканирует QR-код и открывает страницу в Safari или Chrome.

> Статус: в разработке, первая версия ещё не выпущена.

## Как это работает

```
 Телефон (Safari / Chrome)                 ПК (Send Files)
 ┌──────────────────────┐   Wi-Fi / LAN    ┌──────────────────────────┐
 │ веб-страница         │ ───────────────▶ │ встроенный веб-сервер    │
 │ (ничего не ставится) │ ◀─────────────── │ папка «Загрузки\Send Files│
 └──────────────────────┘   HTTP + WS      └──────────────────────────┘
```

1. Запустите Send Files на ПК — в окне появится QR-код.
2. Наведите камеру телефона на код и откройте ссылку.
3. Подтвердите на ПК запрос «Разрешить устройство?» — код из 4 цифр должен совпасть на обоих экранах.
4. Отправляйте файлы в обе стороны.

Если Wi-Fi нет — включите на телефоне точку доступа и подключите к ней ПК.

## Безопасность (кратко)

- Сервер слушает только частные адреса локальной сети (не VPN и не виртуальные адаптеры).
- Каждое устройство сопрягается вручную с подтверждением на ПК; список доверенных устройств можно очистить.
- Соединение — HTTP внутри локальной сети. В открытых (общественных) Wi-Fi сетях пользоваться не стоит.
  Подробности — [docs/DECISIONS.md](docs/DECISIONS.md).

## Сборка из исходников

Нужен .NET 10 SDK (Windows 10 1809+).

```bash
dotnet build SendFiles.slnx
dotnet test tests/SendFiles.Tests
```

Dev-сборка хранит данные отдельно от установленной версии (`%LOCALAPPDATA%\SendFiles-dev`
или папка из переменной `SENDFILES_DATA_DIR`).

## Лицензия

MIT — см. [LICENSE](LICENSE). Сторонние компоненты — [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

---

## English

Fast file transfer between iPhone/Android and Windows on the same network — like AirDrop, with
nothing to install on the phone. The PC runs a small app with a built-in web server; the phone
scans a QR code and opens a page in Safari or Chrome.

> Status: work in progress, no release yet.

1. Start Send Files on your PC — a QR code appears.
2. Point the phone camera at it and open the link.
3. Approve "Allow device?" on the PC — the 4-digit code must match on both screens.
4. Send files both ways.

No Wi-Fi? Turn on the phone's hotspot and connect the PC to it.

Security: the server listens only on private LAN addresses; every device is approved manually;
traffic is plain HTTP inside your LAN, so avoid public Wi-Fi. See [docs/DECISIONS.md](docs/DECISIONS.md).

Build: .NET 10 SDK, `dotnet build SendFiles.slnx`, `dotnet test tests/SendFiles.Tests`.

License: MIT.
