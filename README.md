# LocalDictate

Строго офлайн диктовка для Windows. Русский и английский без облака.

## Что делает

1. Глобальный хоткей начинает и останавливает запись.
2. Речь распознаётся локально (Whisper / whisper.cpp через Whisper.net).
3. Текст вставляется в активное окно.
4. История хранится только на диске (`%LocalAppData%\LocalDictate`).

Сеть нужна только при первой загрузке модели. После этого приложение работает офлайн и не обращается в интернет.

## Стек

- .NET 8 / WPF (Windows 10/11 x64)
- Whisper.net
- NAudio (микрофон)
- Глобальный хоткей + вставка через буфер обмена

## Сборка на Windows

```powershell
dotnet restore
dotnet build -c Release src/LocalDictate/LocalDictate.csproj
dotnet run --project src/LocalDictate -c Release
```

## Первый запуск

1. Разрешите доступ к микрофону.
2. Дождитесь загрузки модели (`ggml-small.bin`) в `%LocalAppData%\LocalDictate\models`.
3. Правый Ctrl — запись; ещё раз — расшифровка и вставка.

## Статус

MVP: каркас, офлайн-политика, запись, распознавание, вставка, история, трей.

## Лицензия

MIT
