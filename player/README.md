# FPlayer (Media Player)

Нативный Windows-видеоплеер на **WinUI 3** (.NET 8) с движком **LibVLC**. Поддерживает MP4, MKV, AVI, WebM, MOV, FLV, WMV, M2TS, ASF, RMVB, MXF, VOB, HEVC, AV1 и другие контейнеры, которые декодирует LibVLC — без установки кодеков. **Фотографии не поддерживаются** (JPG, PNG, GIF, WebP, HEIC и др.).

**Версия:** задаётся в [`Directory.Build.props`](Directory.Build.props) (сейчас **1.0.0**). Отображается в заголовке окна.

## Скачать готовую сборку

Не нужно собирать из исходников — скачайте ZIP из [GitHub Releases](https://github.com/Fenoty/MediaUtilities/releases):

| Шаг | Действие |
|-----|----------|
| 1 | [Releases](https://github.com/Fenoty/MediaUtilities/releases) → последний релиз `player-v*` |
| 2 | Скачать **`FPlayer-1.0.0-win-x64.zip`** (имя зависит от версии) |
| 3 | Распаковать и запустить **`MediaPlayer.exe`** |

Сборка автономная (self-contained): **.NET Runtime устанавливать не нужно**. Нужны Windows 10 19041+ / Windows 11.

## Требования (для разработки)

- Windows 10 19041+ / Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows App SDK (подтягивается через NuGet)

## Запуск

**Из папки `player/` (рекомендуется):**

```bat
run.bat
```

**Или через dotnet** (файл проекта называется `MediaPlayer.csproj`, не `MediaPlayer.WinUI.csproj`):

```bash
cd player
dotnet run --project MediaPlayer.WinUI/MediaPlayer.csproj -c Debug
```

**Не запускай** `dotnet run --project MediaPlayer` — это старый WPF-плеер в папке `MediaPlayer/`.

## Сборка

```bash
cd player
dotnet build MediaPlayer.sln -c Release -p:Platform=x64
```

Исполняемый файл (Debug): `MediaPlayer.WinUI/bin/Debug/net8.0-windows10.0.19041.0/win-x64/MediaPlayer.exe`

### Release ZIP (локально)

```powershell
cd player
.\scripts\package-release.ps1
```

Результат: `player/artifacts/FPlayer-<версия>-win-x64.zip` и распакованная папка рядом.

Для публикации на GitHub создайте тег `player-v<версия>` (версия из `Directory.Build.props`) и запушьте — workflow соберёт ZIP и прикрепит к Release.

```bash
git tag player-v1.0.0
git push origin player-v1.0.0
```

> **Важно:** не используйте `dotnet run --project MediaPlayer` — это старая WPF-версия.  
> Не запускайте exe из папки `player/` — путь неверный. Используйте `run.bat` или полный путь выше.

## Возможности

- **Mica** — системный полупрозрачный фон окна (Windows 11)
- Тёмная Fluent-тема из коробки
- LibVLCSharp.WinUI — универсальные форматы, HW-декод (D3D11)
- Открытие файлов (Ctrl+O) и drag-and-drop — любые медиафайлы кроме изображений; в диалоге доступен фильтр «Все файлы (*.*)»
- Play / Pause, ±10 сек, плейлист с превью
- Клик по видео — play / pause
- Субтитры (.srt, .ass, .vtt)
- Полноэкранный режим (F / Esc)
- **Размещение видео** — вписать, заполнить или растянуть (меню **Вид**, **Настройки**, кнопка на панели у полноэкранного)
- Настройки в `%AppData%/MediaUtilities/player/`

## Размещение видео в окне

| Режим | Описание |
|-------|----------|
| **Вписать в экран** | Пропорции сохраняются, возможны чёрные полосы (по умолчанию) |
| **Заполнить экран** | Без полос, лишнее по краям обрезается |
| **Растянуть на весь экран** | Видео на всю область, возможно искажение |

Где настроить:
- Меню **Вид** → три пункта внизу списка
- Меню **Настройки** → внизу списка
- Кнопка с иконкой рамки на нижней панели (слева от полноэкранного режима)
- На узком окне: меню **⋯** → те же пункты

Выбор сохраняется между запусками.

## Горячие клавиши

| Клавиша | Действие |
|---------|----------|
| Space | Play / Pause |
| F | Полный экран |
| Esc | Выход из полноэкранного |
| ← / → | −10с / +10с |
| ↑ / ↓ | Громкость ±5% |
| M | Mute |
| Ctrl+O | Открыть файл |
| Ctrl+L | Загрузить субтитры |
| N / P | Следующий / предыдущий |

## Стек

- WinUI 3 + Windows App SDK 2.2
- LibVLCSharp.WinUI + VideoLAN.LibVLC.Windows
- CommunityToolkit.Mvvm

## Старый WPF-проект

Папка `MediaPlayer/` — предыдущая реализация на WPF. Не входит в solution; оставлена для справки.
