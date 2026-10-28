# План

Статусы: ✅ готово · 🔄 в работе · ⬜ не начато. Ручные проверки — в `QA_CHECKLIST.md` (⏳).

## 0. Разведка и репозиторий ✅
- Окружение: .NET SDK 10.0.403, gh 2.103 (zab1yak), Node 22, Playwright 1.64; агенты ECC подключены.
- План — planner + architect. `.gitignore`, `.editorconfig`, `global.json`, `LICENSE`, README, docs.
- `gh repo create zab1yak/send-files --public`, первый push.

## 1. Каркас ✅
- Решение `SendFiles.slnx`, central package management, `TreatWarningsAsErrors`, анализаторы.
- Core: `DataDirectory`, `SettingsStore` (атомарно), `NetworkAdapterSelector` + тесты, `PortSelector`,
  `NetworkChangeMonitor`, `ServerSupervisor`, QR.
- App: внешний Generic Host, Serilog в папку данных, single-instance (Mutex + pipe, склейка аргументов
  за 300 мс), трей, окно с QR.
- Перенесено: экран-объяснение брандмауэра до первого `Listen` — в задачу 5 вместе с «Исправить»;
  папка приёма по умолчанию (`Загрузки\Send Files`) — в задачу 3, где начинается приём.
- Тесты: адаптеры, порт, настройки (битый JSON → значения по умолчанию), single-instance.

## 2. Протокол и сопряжение ⬜
- Host/Origin, заголовки, лимиты Kestrel, rate limit, сопряжение, устройства, билеты.
- Загрузка чанками с докачкой (состояние на диске), санитизация имён, `SafePathGuard`, MotW, `TempJanitor`.
- Outbox, Range, WS/SSE, текст.
- Тесты: санитизация (таблица), без ключа 401, чужой Host 400, чужой Origin 403, токен повторно/после
  5 мин, 429, 413, Range 206/416, обрыв и продолжение, перезапуск и продолжение, неверный CRC.
- security-reviewer.

## 3. Телефон → ПК ⬜
- Веб: выбор файлов/фото/видео, прогресс, докачка, отмена, «Готово». Тост и история на ПК.
- Playwright (WebKit iPhone, Chromium Android).

## 4. ПК → телефон ⬜
- Drag-and-drop файлов/папок (ZIP64 потоково), меню Проводника (HKCU), скачивание на телефоне, текст.
- security-reviewer.

## 5. Полировка ⬜
- Анимации, пустые состояния, ошибки (разные сети, брандмауэр, общедоступная сеть), манифест, трей, автозапуск.
- `PERFORMANCE.md`: 1 ГБ по loopback, CPU в простое. Скриншоты до/после.

## 6. Качество и безопасность ⬜
- security-reviewer + code-reviewer по всему; `QA_CHECKLIST.md`.

## 7. Установщик и релиз ⬜
- Velopack win-x64/arm64, GitHub Actions, README RU/EN; тег `v0.1.0` — только после «да».

## Главные риски

1. Диалог брандмауэра Windows при первом `Listen` → «Отмена» создаёт Block-правила (D13).
2. Ключ в `localStorage` — защищаемся отсутствием XSS (D2).
3. Смена IP меняет origin → повторное сопряжение (D15).
4. Репозиторий в OneDrive: синхронизация может блокировать `bin/obj`; облачные заглушки — reparse points
   (в `SafePathGuard` запрещать только symlink/junction, облачные — разрешать).
5. Кириллица и длинные пути — `longPathAware` в манифесте, проверка длины.
6. WebKit в Playwright на Windows — не Safari: поведение iOS (блокировка, «Файлы», safe area) — только на устройстве.
