# NovelForge — Save/Load: дизайн

## Цель и рамки

Следующая фаза после Actors + Dialogue/Choice UI (`docs/superpowers/plans/2026-09-09-actors-dialogue-ui.md`, слито в `master`). Реализует ядро сохранения/загрузки состояния VM: снятие и восстановление снапшота указателя/стека вызовов/переменных, JSON-хранилище на диске, обработка несовместимой версии сейва.

**Вне рамок этой фазы** (явное сужение по сравнению с исходной спекой `docs/superpowers/specs/2026-09-05-novelforge-design.md`, раздел "Save/Load и переменные"):
- Восстановление визуального состояния презентеров (фон/музыка/актёр на экране) при загрузке — загруженная игра показывает пустой/последний экран, не переигрывает визуальные команды. Спека упоминала `ISaveParticipant` как расширение под это, но проектировать этот интерфейс сейчас — до появления хотя бы одного реального потребителя — значит почти наверняка спроектировать его неправильно. Откладывается до фазы, где восстановление экрана действительно понадобится.
- `SaveLoadView` (UGUI-экран со слотами) — как и `DialogueBoxView`/`ChoiceView` в прошлой фазе, это `NovelForge.UI`-слой поверх этого ядра, отдельная фаза.
- Локализация, редакторские инструменты — как и раньше, отдельные фазы.

## Почему это не чисто аддитивная фаза

В отличие от двух предыдущих фаз (Audio/Backgrounds, Actors+Dialogue UI), эта фаза трогает уже смерженный код ядра VM (`Runtime/Commands/PlaybackController.cs`, `Runtime/Commands/StoryPointer.cs`, `Runtime/Commands/VariableStore.cs`) — не потому что там что-то не так, а потому что ни у `PlaybackController`, ни у `VariableStore` сейчас нет способа отдать/принять своё состояние снаружи. Изменения — точечные, `internal`/новые публичные методы, без изменения существующих публичных интерфейсов (`IStoryPointer` не трогается вовсе, тот же принцип, что уже использован для `StoryPointer.Moved`).

Отдельно: у `NovelScript` сейчас нет идентичности (id/имя) — он просто результат `ScriptCompiler.Compile(text)`, без ассета и `ScriptedImporter` (та часть — в будущей фазе "редакторские инструменты"). Поэтому `SaveData.ScriptId` — непрозрачная строка, которую вызывающий код сам придумывает при старте сценария и сам же интерпретирует при загрузке (находит нужный `NovelScript` любым удобным ему способом — `Resources.Load`, свой словарь и т.д.). Эта подсистема не участвует в поиске скрипта по id.

## Модульная структура

Всё — в существующей `NovelForge.Runtime` (спека явно относит Save/Load к Runtime, не к UI-слою). Новых сборок в этой фазе нет. Новая папка `Runtime/SaveLoad/`.

Новая пакетная зависимость: `com.unity.nuget.newtonsoft-json` (пакет для `Dictionary<string, object>` с динамическими типами — `VariableStore` хранит int/float/bool/string как `object`, а встроенный `JsonUtility` не умеет ни в `Dictionary`, ни в `object`-поля). Тот же паттерн bootstrap-риска, что был с TextMeshPro в прошлой фазе: точная разрешаемая версия пакета подтверждается эмпирически на этапе выполнения первой задачи плана, план фиксирует её по факту.

## Точечные добавления к ядру VM

- **`VariableStore`**: `public IReadOnlyDictionary<string, object> Export()`, `public void Import(IReadOnlyDictionary<string, object> values)`. Newtonsoft сериализует `Dictionary<string, object>` напрямую; после раунд-трипа число, сохранённое как `int`/`float`, может прийти обратно как `long`/`double` (обычное поведение JSON-десериализации в `object`) — это не проблема, потому что `GetInt`/`GetFloat`/`GetBool` уже используют `Convert.ToInt32`/`ToSingle`/`ToBoolean`, которые принимают любой числовой источник.
- **`StoryPointer`**: `internal int[] ExportCallStack()` (через `Stack<int>.ToArray()`, порядок — "вершина стека первая"), `internal void RestoreCallStack(IReadOnlyList<int> saved)` (`Clear()`, затем `Push` в обратном порядке, чтобы вершина осталась вершиной). Оба метода — `internal`, тот же принцип видимости, что уже применён к `Moved`/`ResetMoved`: нужны только `PlaybackController` в той же сборке, `IStoryPointer` не меняется.
- **`PlaybackController`**: `public PlaybackSnapshot CreateSnapshot()` и `public void RestoreSnapshot(PlaybackSnapshot snapshot)`. `PlaybackController` не знает про `ScriptId` — это ответственность вызывающего кода, который и так знает, какой скрипт он запустил.

## Новые компоненты (`Runtime/SaveLoad/`)

- **`PlaybackSnapshot`** — `{ int PointerIndex; int[] CallStack }`. Продукт `PlaybackController.CreateSnapshot()`, вход для `RestoreSnapshot(...)`.
- **`SaveData`** — простой POCO, сериализуемый Newtonsoft'ом: `int SchemaVersion`, `string ScriptId`, `int PointerIndex`, `int[] CallStack`, `Dictionary<string, object> Variables`.
- **`ISaveStorage`** — `void Save(string slotId, SaveData data)`, `SaveLoadResult Load(string slotId)`. Слот — произвольная строка, без встроенного понятия количества/сетки слотов (это решает будущий `SaveLoadView`).
- **`SaveLoadStatus`** (enum) — `Success`, `NotFound`, `Incompatible`.
- **`SaveLoadResult`** — `{ SaveLoadStatus Status; SaveData Data; int FoundSchemaVersion }` (`Data` заполнено только при `Success`, `FoundSchemaVersion` — только при `Incompatible`). Это и есть "типизированное событие" из исходной спеки — тот же принцип, что уже используют `Try*`-методы по всему проекту (`AudioLibrary.TryGetMusicClip` и т.д.), просто с тремя исходами вместо двух, поэтому оформлено как результат, а не `bool`.
- **`JsonSaveStorage : ISaveStorage`** — дефолтная реализация: `Application.persistentDataPath`, файл `<slotId>.json`, сериализация через `Newtonsoft.Json.JsonConvert`. Директория настраивается через `internal`-конструктор — тесты пишут во временную папку, не в реальный `persistentDataPath`. `Save` сама проставляет актуальный `SchemaVersion` (вызывающий код его не задаёт).

## Поток данных

**Сохранение:** игра вызывает `playbackController.CreateSnapshot()` → получает `PlaybackSnapshot`; сама собирает `SaveData` (свой `ScriptId` + снапшот + `context.Variables.Export()`); передаёт в `saveStorage.Save(slotId, data)`.

**Загрузка:** игра вызывает `saveStorage.Load(slotId)` → получает `SaveLoadResult`. При `Success` — сама находит нужный `NovelScript` по `Data.ScriptId` (вне зоны ответственности этой подсистемы), создаёт `PlaybackController` с этим скриптом и контекстом, вызывает `RestoreSnapshot(...)` и `context.Variables.Import(Data.Variables)`, дальше продолжает `RunAll()` с восстановленного места.

## Обработка ошибок

Тот же принцип, что везде: деградация вместо падения, конкретный сигнал на каждый класс проблемы.

- Слота нет на диске → `NotFound`. Не exception, не `LogError` — это нормальный сценарий ("сейва ещё не было"), не ошибка.
- Версия схемы в файле не совпадает с ожидаемой → `Incompatible` + `FoundSchemaVersion`. Не exception — вызывающий код сам решает, что показать игроку.
- Файл повреждён / не парсится как JSON → единственный по-настоящему исключительный случай в этой подсистеме: в отличие от остального проекта здесь невозможно тихо деградировать до "используем заглушку", потому что нет безопасного дефолта для указателя/стека вызовов. Трактуется как частный случай `Incompatible` (`FoundSchemaVersion = -1`) плюс `Debug.LogError` с деталью — чтобы вызывающему коду не нужно было ловить exception отдельным путём, но проблема была явно видна в консоли.

## Тестирование

EditMode, без Play Mode:

- **`VariableStore.Export`/`Import`** — раунд-трип int/float/bool/string; `GetInt`/`GetFloat`/`GetBool` продолжают работать корректно после импорта, даже если Newtonsoft вернул `long`/`double` вместо `int`/`float`.
- **`StoryPointer` call-stack export/restore** — через публичный API `PlaybackController` (`CreateSnapshot`/`RestoreSnapshot`), не напрямую: снапшот с непустым стеком вызовов (после нескольких `gosub`), восстановление в новый `PlaybackController` того же скрипта, `return` уходит на правильный адрес — прямая проверка, что порядок при экспорте/импорте не переворачивается.
- **`JsonSaveStorage`** — реальная запись/чтение файла во временную директорию: `Save` → `Load` → `Success` с идентичными данными; `Load` несуществующего слота → `NotFound`; `Load` файла с намеренно неверным `SchemaVersion` → `Incompatible` с правильным `FoundSchemaVersion`; `Load` файла с намеренно битым JSON → `Incompatible` (`FoundSchemaVersion = -1`) + `LogError`.
- **Интеграционный тест**: скомпилировать через реальный `ScriptCompiler` небольшой сценарий с `gosub`/`set`, прогнать `PlaybackController` на несколько шагов, снять снапшот + экспортировать переменные, создать новый `PlaybackController` с тем же `NovelScript`, восстановить оба, доиграть до конца — сравнить результат с "доиграть без сохранения вообще". Главный тест, доказывающий, что весь механизм работает end-to-end, а не только что каждый кусок по отдельности не падает.

Визуальных компонентов в этой фазе нет — раздела "проверяется вручную через демо-сцену" тоже нет.
