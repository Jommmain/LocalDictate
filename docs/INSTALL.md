# Установка LocalDictate

Windows 10/11, 64-bit. Диктовка офлайн. Сеть нужна для установки обновлений и, один раз, для загрузки модели распознавания.

## 1. Скачать

Откройте [релизы](https://github.com/Jommmain/LocalDictate/releases) и возьмите **стабильный** тег `vX.Y.Z` (не prerelease).

- Проще всего: `LocalDictate-Setup-X.Y.Z.exe` — установщик.
- Либо `LocalDictate-vX.Y.Z.zip` — вся папка программы (exe, Whisper, CUDA). Распакуйте её целиком. Один exe без соседних dll не запустится.

## 2. Установить

Установщик (Inno Setup) ставит программу **для текущего пользователя**, без прав администратора:

`%LocalAppData%\Programs\LocalDictate`

Ярлык в меню «Пуск» создаётся всегда. Ярлык на рабочем столе — по желанию, в мастере установки.

Удаление: «Параметры Windows → Приложения» или «Пуск → LocalDictate → Uninstall». Удаляется только папка программы и ярлыки.

Модель, настройки и история лежат отдельно и **не удаляются**:

`%LocalAppData%\LocalDictate`

Чтобы стереть и их, удалите эту папку вручную после деинсталляции.

## 3. Первый запуск

1. Разрешите доступ к микрофону.
2. Дождитесь однократной загрузки модели в `%LocalAppData%\LocalDictate\models`.
3. Правый Ctrl — начать запись, ещё раз — остановить, распознать и вставить текст в активное окно.

Звуковых сигналов при запуске нет. Короткие тихие сигналы начала и конца записи выключены (`playSoundCues: false`). Их можно включить переключателем «Сигналы записи». Если в старом `settings.json` стоит `true`, значение сохраняется, но звук больше не системный и тише.

После появления модели диктовка работает без интернета.

## 4. GPU / CUDA

В папке программы уже лежат библиотеки CUDA 13 (`cudart64_13.dll`, `cublas64_13.dll`, `cublasLt64_13.dll`) и сборки Whisper (`whisper.dll`, `ggml-*.dll`, в том числе `runtimes\cuda\win-x64`). Отдельно ставить CUDA Toolkit не требуется, если эти файлы на месте.

Нет видеокарты NVIDIA — приложение остаётся на CPU. Не копируйте exe отдельно от папки: natives должны обновляться вместе с ним.

## 5. Обновления

Канал — только стабильные теги `vX.Y.Z` в GitHub Releases. Черновики и prerelease игнорируются.

При запуске LocalDictate спрашивает список релизов. Если есть версия новее, в окне строка «Обновления» меняет кнопку на **Обновить** (без всплывающего звука в трее):

1. Скачивает zip всего пакета, не один exe.
2. Сверяет SHA-256 с полем `digest` релиза. Без суммы или при несовпадении папка установки не меняется.
3. Распаковывает пакет во временный каталог `%LocalAppData%\LocalDictate\updates`.
4. Закрывает приложение. Небольшой скрипт дожидается выхода процесса и копирует **всю** папку установки поверх текущей (`robocopy /E /MIR`), включая Whisper и CUDA.
5. Запускает новую копию.

Настройки и модель в `%LocalAppData%\LocalDictate` этот обмен не затрагивает. Не ставьте программу внутрь этой папки данных.

Обновление из каталога `bin` / `obj` отключено специально.

Пока идёт запись, обновление не стартует.

## Сборка установщика

На Windows, после публикации папки:

```powershell
dotnet publish src\LocalDictate\LocalDictate.csproj -c Release -r win-x64 --self-contained false -o artifacts\LocalDictate
iscc installer\LocalDictate.iss
```

Нужен [Inno Setup 6](https://jrsoftware.org/isinfo.php). Готовый `LocalDictate-Setup-0.2.6.exe` появится в `artifacts\`. В репозиторий он не коммитится: пакет большой из‑за CUDA.

Для релиза загрузите на GitHub и zip папки `artifacts\LocalDictate` (имя вида `LocalDictate-v0.2.6.zip`), и setup exe. У автообновления в `digest` ассета должен быть sha256 — GitHub пишет его сам.

## Позже

Подпись кода (SmartScreen), сборка setup в CI и дельта-обновления в этот срез не входят.

---

# Install LocalDictate

Windows 10/11 x64. Dictation stays offline. The network is used for updates and for the one-time speech-model download.

## Download

Open the [releases](https://github.com/Jommmain/LocalDictate/releases) and take a **stable** tag `vX.Y.Z`.

- `LocalDictate-Setup-X.Y.Z.exe` is the installer.
- `LocalDictate-vX.Y.Z.zip` is the full app folder (exe, Whisper, CUDA). Unpack the whole archive. The exe will not run alone.

## Install

The Inno Setup installer is per-user and does not ask for administrator rights. It copies the app to:

`%LocalAppData%\Programs\LocalDictate`

A Start menu shortcut is created. A desktop shortcut is optional in the wizard.

Uninstall removes that folder and the shortcuts. Models, settings, and history stay in `%LocalAppData%\LocalDictate`. Delete that folder yourself if you want them gone.

## First run

Allow the microphone, wait for the one-time model download, then use Right Ctrl to record and again to transcribe and paste.

There is no startup beep. Record start/stop cues are off (`playSoundCues: false`) until the “Сигналы записи” switch is turned on. An older `settings.json` that already has `true` keeps that choice; the tone is a short quiet wave, not a system beep.

## GPU / CUDA

CUDA 13 redistributables and Whisper natives ship inside the install folder. A separate CUDA Toolkit install is unnecessary when those files are present. Machines without an NVIDIA GPU stay on CPU. Updates replace the whole folder, not a lone exe.

## Updates

The in-app checker uses the stable `vX.Y.Z` channel only. A newer release changes the window’s update row to **Update**; it does not play a tray balloon. **Update** downloads the release zip, verifies the GitHub `sha256` digest, then restarts. A helper script waits for the process to exit and mirrors the staged folder onto the install directory, including Whisper and CUDA natives. User data is left in place.

Do not install the app inside `%LocalAppData%\LocalDictate`. Dev `bin` / `obj` folders refuse to self-update.

## Build the installer

```powershell
dotnet publish src\LocalDictate\LocalDictate.csproj -c Release -r win-x64 --self-contained false -o artifacts\LocalDictate
iscc installer\LocalDictate.iss
```

Requires Inno Setup 6. Code signing, CI setup builds, and delta updates are follow-ups.
