# NovelForge — Actors + Dialogue/Choice UI: дизайн

## Цель и рамки

Следующая фаза после Content Presenters (Audio/Backgrounds, `docs/superpowers/plans/2026-09-08-content-audio-backgrounds.md`, слито в `master`). Реализует Actors-подсистему и первую рабочую реализацию `IDialoguePresenter`/`IChoicePresenter` — то есть первый по-настоящему играбельный вертикальный срез: сценарий с персонажами, репликами и выбором можно будет запустить и увидеть на экране.

Вне рамок этой фазы (следующие отдельные фазы): `SaveLoadView`/Save-Load-подсистема, локализация, редакторские инструменты (редактор сценариев, граф ветвлений). `SaveLoadView` спека числит рядом с `DialogueBoxView`/`ChoiceView` в разделе "UI-слой", но по факту он бессмысленен без ещё не существующей Save/Load-подсистемы — откладывается вместе с ней.

## Модульная структура: впервые вводим `NovelForge.UI`

Согласно исходной спеке (`docs/superpowers/specs/2026-09-05-novelforge-design.md`, "Модульная структура пакета"), Actors — часть `NovelForge.Runtime` (та же сборка, где уже живут Audio/Backgrounds), а дефолтная UGUI-реализация экрана диалога/выбора — отдельная сборка `NovelForge.UI`, зависящая от `NovelForge.Runtime` только через интерфейсы/типы, которые Runtime уже публично экспортирует (`IDialoguePresenter`, `IChoicePresenter`, а также публичный API `CharacterDefinition`/`CharacterLibrary`/`ActorView`, которые UI использует, но не наоборот).

Predыдущий план (`2026-09-08-content-audio-backgrounds.md`) явно фиксировал: "This phase is Runtime-only: no `NovelForge.UI` or `NovelForge.Editor` asmdefs yet — those arrive in later phases." Эта фаза — тот самый более поздний этап.

Практические следствия:
- Новая папка `UI/` в корне пакета (рядом с `Runtime/`, `Tests/`), свой `NovelForge.UI.asmdef`, референс на `NovelForge.Runtime`.
- Новая тестовая сборка `Tests/UI/` (`NovelForge.UI.Tests.asmdef`), референс на оба — `NovelForge.Runtime` и `NovelForge.UI`.
- Пакетная зависимость на TextMeshPro (`com.unity.textmeshpro` для сборки; в `TestProject~/Packages/manifest.json` уже как минимум `com.unity.ugui` зарегистрирован ядром Unity — актуальный способ подключения TMP уточняется на этапе плана, поскольку он менялся между версиями Unity).
- `AssemblyInfo.cs` в `UI/` с собственным `[assembly: InternalsVisibleTo("NovelForge.UI.Tests")]` — по аналогии с существующим в `Runtime/`.

## Actors (`Runtime/Actors/`)

- **`CharacterDefinition`** (ScriptableObject-ассет на персонажа): публичные read-only свойства `Id`, `DisplayName`, `NameColor` поверх `[SerializeField] internal`-полей (паттерн уже отработан в `AudioLibrary`/`BackgroundLibrary` — внутренние поля для прямого тестового доступа через `InternalsVisibleTo("NovelForge.Runtime.Tests")`, публичное API для остальных сборок, включая будущую `NovelForge.UI`). Плюс `[Serializable] struct Pose { string emotion; Sprite sprite; }`, массив `Pose[]`, метод `public bool TryGetSprite(string emotion, out Sprite sprite)`.
- **`CharacterLibrary`** (ScriptableObject-агрегатор): `[SerializeField] internal CharacterDefinition[] characters`, `public bool TryGetCharacter(string id, out CharacterDefinition character)` — тот же `Try*`-паттерн, что `AudioLibrary.TryGetMusicClip`. Запись с `null`-ссылкой на `CharacterDefinition` в массиве или с `Id`, не совпадающим ни с одним — не найдено.
- **`ActorView`** (`MonoBehaviour`, один слот на экране, один `SpriteRenderer`): `public IEnumerator ShowSprite(Sprite sprite)` — мгновенная смена `sprite`, затем fade-in альфы через `IDeltaTimeSource` (тот же паттерн, что `BackgroundPresenter.ShowCg`: один слот, не двойной буфер, потому что здесь нет кроссфейда между двумя изображениями — старое обрезается сразу, новое проявляется). Отсутствующий `spriteRenderer` — тот же "missing wiring"-паттерн, что везде: `LogError` + `yield break`, ничего не рисуем. Нет метода "спрятать" — `IDialoguePresenter`/сценарий такого не запрашивает, актёр просто продолжает показывать последний заданный спрайт.

## UI-слой (`UI/`, новая сборка `NovelForge.UI`)

- **`IAdvanceInputSource`** (интерфейс, `UI/`): `bool ConsumeAdvanceRequest()` — возвращает true и сбрасывает внутренний флаг ровно один раз на запрос игрока продолжить. `ButtonAdvanceInputSource` — продакшен-реализация, оборачивает `UnityEngine.UI.Button.onClick`, копит один "ожидающий" клик до следующего опроса. Тестовая реализация — `FakeAdvanceInputSource` с публично выставляемым флагом (по аналогии с `FakeDeltaTimeSource`).
- **`DialogueBoxView`** (`MonoBehaviour`): поля `TMP_Text nameText`, `TMP_Text bodyText`, `float secondsPerCharacter`, инжектируемые `IDeltaTimeSource TimeSource` и `IAdvanceInputSource AdvanceInput` (по аналогии с `TimeSource` у `AudioPresenter`/`BackgroundPresenter`; `AdvanceInput` без дефолтного значения в поле — лениво создаётся в `Awake()` из опционального `[SerializeField] internal Button advanceButton`, если тест не подставил свою реализацию раньше). `public IEnumerator ShowText(string speakerName, Color nameColor, string text)`:
  1. Ставит имя/цвет.
  2. Печатает `text` посимвольно, продвигая индекс через `TimeSource.DeltaTime / secondsPerCharacter`; на каждом кадре также опрашивает `AdvanceInput.ConsumeAdvanceRequest()` — если true и печать не завершена, текст мгновенно дописывается целиком и печатающий цикл прерывается (клик "пропускает" печать, не продолжает сцену).
  3. После полной печати входит в отдельный цикл ожидания: опрашивает `AdvanceInput.ConsumeAdvanceRequest()` каждый кадр, выходит (и тем самым завершает корутину — VM идёт дальше), когда true.
  4. Если `AdvanceInput == null` (не удалось создать: нет ни инжектированной реализации, ни `advanceButton`) — `LogError`, текст всё равно допечатывается мгновенно и целиком, ожидания клика не происходит (деградация, не бесконечное зависание).
- **`ChoiceView`** (`MonoBehaviour`, реализация `IChoicePresenter`): `[SerializeField] internal Button[] optionButtons` — фиксированный, заранее размещённый в инспекторе пул (не runtime `Instantiate` префаба: проще и детерминированнее тестировать, тот же принцип, что фиксированные `ActorView`-слоты у `DialoguePresenter`, а не динамический список). `PresentChoices(optionTexts, onSelected)`: если `optionTexts.Count > optionButtons.Length` — `LogError`, лишние варианты обрезаются до вместимости массива. Показывает первые `min(count, optionButtons.Length)` кнопок с текстом варианта, остальные — `SetActive(false)`. Подписывает каждую активную кнопку на одноразовый обработчик клика (снимает подписку сразу после первого клика любой из них), скрывает все кнопки обратно, вызывает `onSelected(i)`, корутина завершается.
- **`DialoguePresenter`** (`MonoBehaviour`, реализация `IDialoguePresenter`): `[SerializeField] internal CharacterLibrary library`, `[SerializeField] internal DialogueBoxView dialogueBox`, `[Serializable] struct PositionSlot { string position; ActorView view; }`, `[SerializeField] internal PositionSlot[] positionSlots`. `public IEnumerator ShowLine(string characterId, string text, string emotion, string position)`:
  1. `dialogueBox == null` — фатально для вызова: `LogError`, `yield break` (поломка сборки сцены, не контента).
  2. Персонаж не найден в `library` (или `library == null`) — `LogError`, в качестве отображаемого имени используется сам `characterId`, цвет по умолчанию, спрайт не трогаем.
  3. Персонаж найден, но `TryGetSprite(emotion, ...)` не находит позу — `LogError`, спрайт не трогаем (остаётся прежний), имя/цвет — от найденного персонажа.
  4. `position` не совпадает ни с одним `PositionSlot.position` — `LogError`, актёра не показываем вовсе (нет `ActorView`, на который выводить).
  5. Если слот и спрайт оба разрешились — запускает `actorView.ShowSprite(sprite)` как отдельную, не блокирующую корутину (визуальный fade персонажа не должен задерживать чтение — обычно короче времени чтения текста в любом случае).
  6. Дожидается (`yield return`) `dialogueBox.ShowText(displayName, nameColor, text)` — это и есть основная длительность вызова.

## Поток данных (без изменений в существующих командах)

`SayLineCommand` (существует, Phase 1, без изменений в этой фазе) → `context.Dialogue.ShowLine(...)` → `DialoguePresenter` как выше.
`ChoiceCommand` (существует, Phase 1, без изменений) → `context.Choices.PresentChoices(...)` → `ChoiceView` как выше.

Никаких изменений в `Runtime/Commands/`, `Runtime/Parsing/`, `Runtime/Commands/Presenters/*` (существующие интерфейсы) — фаза только добавляет конкретные реализации, как и предыдущая (Audio/Backgrounds).

## Обработка ошибок

Общий принцип не меняется: конкретное сообщение на каждый класс проблемы, деградация вместо падения, "missing wiring" (отсутствие ссылки на компонент сцены) — фатально только для конкретного вызова, не для всего плеера.

Единственное новое: у `DialogueBoxView.ShowText` есть путь "нет способа поймать клик" (`AdvanceInput == null`) — в этом случае сцена продолжается без ожидания, а не зависает навсегда. Бесконечное ожидание клика, которого физически некому прислать (сцена не настроена), было бы куда хуже тихого пропуска.

## Тестирование

EditMode, без Play Mode, тот же принцип, что в Audio/Backgrounds:

- Печать текста и ожидание клика оба управляются инъекцией (`IDeltaTimeSource` уже есть; новый `IAdvanceInputSource`/`FakeAdvanceInputSource`) — тесты водят обе координаты вручную, без реального времени или реального клика.
- Обязательны тесты на промежуточное состояние печати (не только "мгновенно долетело до конца одним `MoveNext()`") — этот урок явно вынесен из финального ревью прошлой фазы (находка про непроверенную середину fade-кривой).
- Тест "клик во время печати — текст мгновенно дописывается, но корутина не завершается" отдельно от теста "клик после печати — корутина завершается".
- `ChoiceView` — `Button.onClick.Invoke()` напрямую, без сцены/реального клика; отдельный тест на обрезку при избытке вариантов.
- `ActorView`/`DialoguePresenter`/`DialogueBoxView`/`ChoiceView` — та же схема `GameObject` + `[TearDown]` с `Object.DestroyImmediate`, что уже используется в `AudioPresenterTests`/`BackgroundPresenterTests`.

Визуальная/вёрсточная корректность — вручную, через демо-сцену в `Samples~` (по-прежнему отложенную с прошлой фазы; здесь наконец появляется полноценный вертикальный срез, который стоит показать).

## Открытые вопросы для этапа планирования (не блокируют спеку, но план должен явно решить)

- Точный способ подключения TextMeshPro в `TestProject~/Packages/manifest.json` под Unity 6000.6 (пакет `com.unity.textmeshpro` напрямую, либо через `com.unity.ugui`/встроенные ресурсы) — зависит от того, что реально доступно в установленной версии редактора; уточняется и фиксируется во время выполнения первой задачи плана, которая касается TMP.
- Именование ассемблей: `NovelForge.UI` / `NovelForge.UI.Tests`, `rootNamespace` — по аналогии с уже существующими `NovelForge.Runtime`/`NovelForge.Runtime.Tests`, ожидается `NovelForge.UI` / `NovelForge.UI.Tests` без сюрпризов, но план должен зафиксировать это явно как Global Constraint.
