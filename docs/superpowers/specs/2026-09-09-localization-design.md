# NovelForge — Локализация: дизайн

## Цель и рамки

Следующая фаза после Save/Load (`docs/superpowers/plans/2026-09-09-save-load.md`, слито в `master`). Реализует базовый механизм локализации реплик и вариантов выбора: стабильный id на каждую локализуемую строку (авто-генерируемый либо явный), формат таблицы перевода на диске, резолвинг перевода в момент выполнения команды.

**Вне рамок этой фазы** (явное сужение по сравнению с исходной спекой `docs/superpowers/specs/2026-09-05-novelforge-design.md`, раздел "Локализация"):
- Реальное воспроизведение voice-over по репликам — этой фичи нет ни в одной существующей фазе (Audio-подсистема играет музыку/sfx, но не VO, привязанное к конкретной реплике), поэтому и опциональное поле "путь к озвучке" в переводе сейчас добавлять незачем — нечему было бы его использовать. Добавится вместе с самой фичей VO-воспроизведения.
- Реестр локалей / переключение языка "одной строкой" внутри движка — какие локали существуют, откуда грузить файл для каждой, где хранить выбор игрока — целиком ответственность вызывающего кода, как и выбор `CharacterLibrary`/`AudioLibrary`-ассета. Ядро принимает уже готовую загруженную таблицу перевода.
- Редакторские инструменты (Unity-инспектор для перевода, экспорт/импорт под конкретный TMS) — отдельная фаза, если вообще понадобится; `LocalizationTable` — обычный конкретный класс поверх JSON, не `ScriptableObject`, тем же принципом, что `SaveData` в прошлой фазе.

## Почему это не чисто аддитивная фаза

Как и Save/Load, эта фаза трогает уже смерженный код ядра (`Runtime/Parsing/ScriptCompiler.cs`, `Runtime/Commands/StoryContext.cs`, `Runtime/Commands/Builtin/SayLineCommand.cs`, `Runtime/Commands/Builtin/ChoiceCommand.cs`) — не потому что там что-то не так, а потому что ни у одной реплики или варианта выбора сейчас нет стабильного идентификатора, без которого перевод физически не к чему привязать. `SayLineCommand`/`ChoiceCommand` получают новые конструкторные параметры (id), `ChoiceCommand` — новый параметр вообще (список id по числу опций); публичные интерфейсы (`IDialoguePresenter`, `IChoicePresenter`) не трогаются вовсе — резолвинг перевода происходит целиком внутри команды, до вызова presenter, тем же принципом изоляции, что уже применён в Save/Load к `IStoryPointer`.

Исходная спека предполагала статический `LocaleManager.Current`. Это был бы первый синглтон в проекте — везде до сих пор сквозное состояние (переменные, презентеры, теперь снапшоты сохранения) идёт через `StoryContext`, ни одного статического держателя состояния в кодовой базе нет. Локаль в этом дизайне — обычное свойство `StoryContext`, консистентно с остальными.

## Изменения в DSL и `ScriptCompiler`

Новый опциональный тег `@id_name` — в строке реплики (наравне с уже существующими `#emotion` и bare-word позицией `left`/`right`/`center`) и в строке варианта выбора (после закрывающей кавычки, перед `->`):

```
label greet
Alice: Привет! #happy left
Alice: Как дела? @custom_greet_check
choice
  "Хорошо" @choice_good -> good_path
  "Плохо" -> bad_path
```

Если явный `@id` не указан, компилятор генерирует его сам: `<label>_<порядковый_номер>`.
- `<label>` — ближайшая предшествующая метка `label ...`; если реплика встретилась раньше первой метки скрипта — используется зарезервированный псевдо-label `_start`.
- `<порядковый_номер>` — общий счётчик локализуемых строк (реплики и варианты выбора вместе, по порядку появления в скрипте), начинается с `1` и сбрасывается на каждой новой `label`. Строки с явным `@id` тоже учитываются в счётчике (занимают свой порядковый номер), чтобы порядковые номера соседних авто-id не "прыгали" при добавлении explicit-id строки между ними.

Дубликат id — ошибка компиляции (`ParseException`, та же обработка, что у `Duplicate label`): явный id совпал с другим явным, или явный совпал со сгенерированным авто-id. Проверка идёт по всему скрипту целиком, не только в пределах одной `label` — таблица перевода на диске плоская и глобальная (см. ниже), поэтому и уникальность id должна быть глобальной. Авто-id между собой коллизий не дают (у каждого свой префикс-`label`), риск — только у явных id, случайно совпавших друг с другом или с чьим-то авто-id из другого блока. Это статическая проверка на этапе `Compile`, не требует прогона скрипта.

## `StoryContext` и `LocalizationTable`

Новый конкретный класс `Runtime/Localization/LocalizationTable.cs` — по образцу `VariableStore`/`CharacterLibrary`, не `ScriptableObject` (нет ссылок на Unity-объекты, только текст):

```csharp
public class LocalizationTable
{
    private readonly Dictionary<string, string> _entries;

    private LocalizationTable(Dictionary<string, string> entries) => _entries = entries;

    public bool TryGetText(string lineId, out string text) => _entries.TryGetValue(lineId, out text);

    public static LocalizationTable FromJson(string json)
    {
        var entries = JsonConvert.DeserializeObject<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        return new LocalizationTable(entries);
    }
}
```

Формат файла на диске — плоский словарь `id → переведённый текст`, тем же `Newtonsoft.Json`, что уже используется для `SaveData`:
```json
{
  "greet_1": "Привет!",
  "choice_good": "Хорошо"
}
```

`StoryContext` получает одно новое свойство:
```csharp
public LocalizationTable Localization { get; set; }
```
По умолчанию `null`, как и остальные необязательные поля контекста (`Dialogue`, `Choices`, `Audio`, ...) — означает "локализация не активна, показывать исходный текст скрипта". Загрузка файла с диска/Resources и выбор локали — забота вызывающего кода; смена языка — обычное переприсваивание `context.Localization = LocalizationTable.FromJson(...)`, тем же способом, каким уже сегодня переключаются `Audio`/`Backgrounds` пресентеры.

## Изменения в командах

Обе команды резолвят текст в момент `Execute`, до передачи presenter'у — сам presenter ничего не знает о локализации:

```csharp
// SayLineCommand.cs
private readonly string _lineId;

public SayLineCommand(string characterId, string text, string emotion, string position, string lineId) { ... }

public override IEnumerator Execute(StoryContext context, IStoryPointer pointer)
{
    if (context.Dialogue == null) { Debug.LogError("NovelForge: no IDialoguePresenter wired — skipping dialogue line."); yield break; }
    yield return context.Dialogue.ShowLine(_characterId, ResolveText(context), _emotion, _position);
}

private string ResolveText(StoryContext context)
{
    if (context.Localization == null) return _text;
    if (context.Localization.TryGetText(_lineId, out string translated)) return translated;
    Debug.LogWarning($"NovelForge: no translation for line id '{_lineId}' — falling back to source text.");
    return _text;
}
```

`ChoiceCommand` получает параллельный конструкторный параметр `optionIds` (тот же порядок и длина, что `optionTexts`) и резолвит каждый вариант тем же способом перед вызовом `context.Choices.PresentChoices(...)` — так что один choice-блок может быть переведён частично (например, только часть вариантов) без падения.

## Обработка ошибок

- Дубликат id на этапе компиляции → `ParseException`, тот же путь, что `Duplicate label 'x'.` — ошибка обнаруживается сразу при загрузке скрипта, не во время игры.
- Отсутствие перевода для конкретного id в момент выполнения → **не** `Debug.LogError`. Везде в проекте `LogError` сигналит аварийную ситуацию (не подключён presenter, presenter вернул некорректный ответ); отсутствие перевода — ожидаемое, не аварийное состояние во время работы переводчиков над неполной таблицей. Используется `Debug.LogWarning` — первый случай `LogWarning` в проекте, поведение то же самое (одно сообщение на каждую отдельную причину), просто менее тревожный уровень. Фолбэк — исходный текст скрипта, игра не останавливается и не показывает пустую строку.
- `LocalizationTable.FromJson` на `null`/пустой JSON — не бросает исключение, возвращает пустую таблицу (симметрично `VariableStore.Import(null)` из прошлой фазы).

## Тестирование

EditMode, без Play Mode:

- **`ScriptCompiler`** — авто-генерация id с `label` и без (через `_start`); сброс счётчика на новой `label`; общий счётчик для реплик и choice-опций вместе; явный `@id` не потребляет и не сбивает нумерацию авто-id соседних строк; дубликат id (явный/явный и явный/авто) → `ParseException`.
- **`LocalizationTable`** — `FromJson` парсит обычный словарь; `TryGetText` — хит и промах; `FromJson(null)`/пустая строка не бросает исключение, `TryGetText` на пустой таблице — промах.
- **`SayLineCommand`** — `Localization == null` → исходный текст передан presenter'у; таблица с найденным id → перевод передан presenter'у; таблица без нужного id → исходный текст передан presenter'у + один `LogWarning`.
- **`ChoiceCommand`** — то же самое, но по каждому варианту независимо; отдельный тест на частичный перевод (один вариант переведён, другой — нет, оба факта видны в списке текстов, переданном presenter'у).
- **Интеграционный тест**: реальный `ScriptCompiler.Compile(...)` на небольшом сценарии с несколькими репликами (авто-id и явный id вперемешку) и одним choice, реальный `LocalizationTable.FromJson(...)` с частично заполненной таблицей, прогон через `PlaybackController.RunAll()` с `RecordingDialoguePresenter`/`RecordingChoicePresenter` — проверка, что записанные вызовы presenter'ов содержат ожидаемую смесь переведённого и исходного текста. Главный тест, доказывающий сквозную работу механизма, а не только каждого куска по отдельности.

Существующие тесты `IDialoguePresenter`/`IChoicePresenter`/UI-слоя не меняются — контракт этих интерфейсов не тронут.

Визуальных компонентов в этой фазе нет — раздела "проверяется вручную через демо-сцену" тоже нет.
