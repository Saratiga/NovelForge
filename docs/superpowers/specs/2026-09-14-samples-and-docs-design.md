# NovelForge — README/USAGE и Samples~ демо-сцена — дизайн

## Цель

Последний обязательный пункт MVP из мастер-спеки
(`docs/superpowers/specs/2026-09-05-novelforge-design.md`): "Руководство пользователя
(README/USAGE) с примерами" и `Samples~`-демо, которая по мастер-спеке также служит
основным способом ручной визуальной проверки пакета ("Визуальные вещи ... проверяются
вручную через демо-сцену в `Samples~`").

Аудитория — только сам автор в будущих проектах. Не цель: маркетинг, документация
уровня Asset Store, множественные сэмплы на каждую фичу по отдельности.

## Архитектурное решение: `NovelRunner`

Сейчас в пакете нет ни одного компонента, который бы собирал `PlaybackController` +
`StoryContext` + презентеры в работающую сцену — каждая предыдущая фаза делала это
вручную и одноразово для live-QA. Второй пробел: `ITimingPresenter` (нужен команде
`wait`) не имеет ни одной конкретной Unity-реализации, только тестовый дубль.

Рассматривались два варианта:

- **Тонкий `NovelRunner`** — компилирует `NovelScriptAsset`, собирает `StoryContext` из
  назначенных в инспекторе презентеров, сам реализует `ITimingPresenter`, стартует/
  останавливает корутину `PlaybackController.RunAll()`. Save/load и навигация между
  экранами (title/gameplay) — не его забота.
- **Толстый `NovelRunner`** — дополнительно владеет `ISaveStorage`/`SaveLoadController`
  и даёт `NewGame()`/`SaveGame()`/`LoadGame()` напрямую.

**Выбран тонкий вариант** — соответствует уже установленному в проекте паттерну малых
несвязанных компонентов (`StoryContext` — settable-properties DI без синглтонов,
`SaveLoadController` уже сегодня конструируется снаружи и ничего не знает о сцене).
Толстый вариант зашил бы в переиспользуемый API конкретные предположения об экранах
меню, которые у разных игр будут разными.

### API

Новый файл `UI/NovelRunner.cs`, часть публичного API пакета (не демо-only):

```csharp
public class NovelRunner : MonoBehaviour, ITimingPresenter
{
    [SerializeField] internal NovelScriptAsset script;
    [SerializeField] internal DialoguePresenter dialogue;
    [SerializeField] internal ChoiceView choices;
    [SerializeField] internal AudioPresenter audio;
    [SerializeField] internal BackgroundPresenter backgrounds;
    [SerializeField] internal TextAsset localizationJson; // опционально, может быть null

    public StoryContext Context { get; private set; }
    public PlaybackController Playback { get; private set; }
    public event Action OnFinished;

    public void Prepare(NovelScriptAsset scriptToPlay = null);
    public void Begin();
    public void Play(NovelScriptAsset scriptToPlay = null); // Prepare(scriptToPlay) + Begin()
    public void Stop();

    public IEnumerator Wait(float seconds) { yield return new WaitForSeconds(seconds); }
}
```

- Unity запускает корутину синхронно вплоть до первой настоящей точки приостановки —
  один `StartCoroutine` может молча прогнать несколько команд скрипта за один кадр, если
  среди них нет `wait`/выбора. Поэтому подготовка `Context`/`Playback` отделена от старта
  корутины:
  - `Prepare()` компилирует `script`, создаёт `StoryContext` (заполняет `Dialogue`/
    `Choices`/`Audio`/`Backgrounds`/`Timing`=`this` из назначенных полей) и
    `PlaybackController`, ничего не выполняет.
  - `Begin()` стартует `StartCoroutine(RunAll())` на уже подготовленном `Playback`.
  - `Play()` = `Prepare()` + `Begin()` — используется кнопкой "New Game".
  - Для "Continue": `runner.Prepare(script)` → `saveLoadController.LoadInto(slotId)`
    (восстанавливает снапшот на ещё не стартовавший `Playback`) → `runner.Begin()`.
    Порядок гарантирует, что ни одна команда не выполнится до восстановления позиции.
- Отсутствующий `script` при `Play()` — `Debug.LogError`, никакого исключения (тот же
  принцип "missing wiring", что и во всех презентерах проекта).
- `LocalizationTable` — не ассет (обычный класс, строится из JSON-строки), поэтому в
  инспекторе — `TextAsset`; `null` означает "без локализации", `SayLineCommand` и так
  падает на исходный текст при `context.Localization == null`.
- `NovelRunner` сам реализует `ITimingPresenter` — закрывает единственный найденный
  пробел (в рантайме не было ни одной конкретной Unity-реализации этого интерфейса вне
  тестового дубля `RecordingTimingPresenter`).

## Демо-сюжет (`Samples~/GettingStarted/Story.nfscript`)

Один короткий сюжет (~40-60 строк), проходящий через весь DSL и все подсистемы:

```
label start
bg classroom
music theme
alice: Hi! I'm Alice. #happy left
bob: And I'm Bob. #neutral right
set trust = 0
alice: Would you like to hear a joke?
choice
"Sure!" @yes -> joke
"No thanks" @no -> skip_joke

label joke
gosub tell_joke
set trust += 1
jump after_joke

label skip_joke
bob: Suit yourself. #neutral right

label after_joke
if trust >= 1
    alice: Thanks for listening! #happy left
else
    alice: Okay then. #neutral left
endif
cg sunset
wait 0.5
alice: Let's see what's outside.
sfx door_open
jump ending

label tell_joke
bob: Why did the developer quit? #laughing right
bob: Because they didn't get arrays! #laughing right
return

label ending
alice: That's the end of this demo. #happy left
return
```

Покрывает: `label`/`jump`, диалог с `#emotion`/`left`/`right`, `set`/`if`/`else`/`endif`,
`choice`, `gosub`/`return`, `bg`/`cg`/`music`/`sfx`/`wait`, явные `@id` для локализации.

## Структура демо-сцены (`Samples~/GettingStarted/GettingStarted.unity`)

**Ассеты** (создаются один раз редакторской C#-утилитой при реализации — сама утилита не
шипится, только результат):

- `Art/`: одноцветные `Sprite`-текстуры на каждую эмоцию Alice/Bob (`#happy`/`#neutral`/
  `#laughing`) и на `bg classroom` / `cg sunset`.
- `Audio/`: процедурные 1-2-секундные `AudioClip` (синус-тон) для `music theme` и
  `sfx door_open`.
- `CharacterLibrary`, `BackgroundLibrary`, `AudioLibrary` — стандартные
  `[CreateAssetMenu]`-ассеты, ссылающиеся на созданные выше файлы.
- `Localization/ru.json` — 2-3 переведённые строки по `@id` из скрипта, демонстрируют
  `LocalizationTable`.

**Иерархия сцены:**

- `TitleScreen` (Canvas): кнопки **New Game** и **Load Game** (последняя активна, только
  если `SaveLoadController.SlotExists` истинно хотя бы для одного слота). Переиспользует
  существующий `SaveLoadView` в режиме `ShowLoadMode()`.
- `GameplayUI` (Canvas, скрыт на старте): `DialoguePresenter` + два `ActorView` (left/
  right) + `DialogueBoxView`, `ChoiceView`, кнопка **Save** в углу, открывающая тот же
  `SaveLoadView` в режиме `ShowSaveMode()`.
- `NovelRunner` (GameObject): ссылки на все презентеры выше + `Story.nfscript`.
- `DemoFlow.cs` — единственный демо-only скрипт (живёт только в `Samples~`, не часть
  публичного API пакета): слушает клики Title/Save-кнопок, переключает Canvas'ы,
  конструирует `SaveLoadController` (используя `new JsonSaveStorage()` — реальный
  `Application.persistentDataPath`) и связывает его с `NovelRunner.Context`/`.Playback`.
  Это тот самый "склеивающий" код, который README/USAGE пошагово объясняет.

## Упаковка `Samples~`

Конвенция UPM: папка `Samples~` (с тильдой — Unity не импортирует её содержимое как
ассеты самого пакета) плюс запись в `package.json`:

```json
"samples": [
  {
    "displayName": "Getting Started",
    "description": "End-to-end demo: dialogue, branching, choices, save/load, localization, audio and backgrounds.",
    "path": "Samples~/GettingStarted"
  }
]
```

При импорте через Package Manager → NovelForge → Samples → Import Unity копирует папку
без тильды в `Assets/Samples/NovelForge/0.1.0/Getting Started/`.

Содержимое `Samples~/GettingStarted/`: `GettingStarted.unity`, `Story.nfscript`,
`DemoFlow.cs`, `Art/`, `Audio/`, `Localization/ru.json`, ассеты-библиотеки.

## `README.md` (корень пакета)

1. Что это — один абзац, на основе `package.json.description`.
2. Установка через UPM (git URL / local path в `manifest.json`).
3. Быстрый старт — импорт `Getting Started` через Package Manager → Samples.
4. Список возможностей (буллеты из MVP мастер-спеки).
5. Ссылка на `USAGE.md`.
6. Ссылка на `docs/superpowers/specs/` для архитектурных решений.

## `USAGE.md` (корень пакета)

1. **Структура проекта** — где хранить `.nfscript`, `CharacterDefinition`/
   `CharacterLibrary`, `BackgroundLibrary`/`AudioLibrary`, файлы локализации, кастомные
   `ISaveStorage`.
2. **Синтаксис DSL** — таблица всех конструкций (`label`, диалог
   `Id: text #emotion @id left/right/center`, `set`/`if`/`else`/`endif`, `choice`,
   `jump`/`gosub`/`return`, `bg`/`cg`/`music`/`sfx`/`wait`) с примером на каждую.
3. **Редакторские инструменты** — как открыть Script Editor Window (подсветка,
   автодополнение), Character Editor, Branch Graph; что делает `NovelScriptImporter`.
4. **Пошаговый пример с нуля** — те же шаги, что в демо: создать `.nfscript` → создать
   библиотеки → расставить презентеры на сцене → добавить `NovelRunner` → запустить.
5. **Save/Load** — `ISaveStorage`/`JsonSaveStorage`, `SaveLoadController`, `SaveLoadView`.
6. **Локализация** — формат JSON, `@id`, авто-генерация id, `LocalizationTable.FromJson`.

## Тестирование

- `Tests/UI/NovelRunnerTests.cs` (EditMode) — тот же паттерн, что `SaveLoadViewTests`/
  `ChoiceViewTests`: реальные компоненты на `GameObject`, доступ к `internal`-полям
  напрямую (существующий `InternalsVisibleTo("NovelForge.UI.Tests")` в `UI/AssemblyInfo.cs`),
  корутина продвигается вручную через `.MoveNext()`.
  - `Play_WiresAssignedPresentersIntoContext` — после `Play()` `Context.Dialogue`/
    `.Choices`/`.Audio`/`.Backgrounds`/`.Timing` указывают на назначенные компоненты.
  - `Play_ScriptRunsToCompletion_RaisesOnFinished` — тривиальный
    `"label start\nreturn\n"`, `Playback.IsFinished` истинно и `OnFinished` выстрелил
    ровно один раз после прогона корутины.
  - `Play_MissingScript_LogsError_DoesNotThrow`.
- Визуальная/сценовая часть (сама демо-сцена, титульный экран, переключение Canvas'ов,
  реальные спрайты/тоны, `SaveLoadView` в сцене) — вручную через Play Mode в редакторе,
  как и предписывает мастер-спека для `Samples~`.

## Deliverable

- `Runtime`-изменение: `UI/NovelRunner.cs` + тесты.
- `Samples~/GettingStarted/` — полная рабочая демо-сцена.
- `package.json` — секция `"samples"`.
- `README.md`, `USAGE.md` в корне пакета.
