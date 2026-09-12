# NovelForge — SaveLoadView — дизайн

## Цель и рамки

Последний недостающий компонент раздела "UI-слой (UGUI)" мастер-спеки: `SaveLoadView` — сетка слотов сохранения/загрузки. Явно отложен дважды при предыдущих фазах (`2026-09-09-actors-dialogue-ui-design.md`: "бессмысленен без ещё не существующей Save/Load-подсистемы"; `2026-09-09-save-load-design.md`: "как и `DialogueBoxView`/`ChoiceView` в прошлой фазе, это `NovelForge.UI`-слой поверх этого ядра, отдельная фаза"). Обе причины отпали — Save/Load-подсистема (`ISaveStorage`/`SaveData`/`PlaybackSnapshot`) и `NovelForge.UI` (уже с `DialogueBoxView`/`ChoiceView`) существуют и смержены.

**Вне рамок**: восстановление визуального состояния презентеров при загрузке (фон/музыка/актёр на экране) — как и раньше, отложено до появления реального потребителя `ISaveParticipant`-подобного расширения; удаление слота; подтверждение перезаписи занятого слота (перезаписывается без диалога — тот же принцип "минимум трения", что и у остального UI-слоя).

## Почему это архитектурная, а не точечная фаза

Проверка существующего кода показала: `PlaybackController.CreateSnapshot/RestoreSnapshot`, `VariableStore.Export/Import`, `ISaveStorage.Save/Load` — вся логика ОДНОЙ операции сохранения/загрузки уже есть, но:
1. **`ISaveStorage` не умеет перечислить занятые слоты** — сетке нужно знать, что показать, до того как игрок кликнет. Требуется расширение публичного интерфейса Runtime, не только новый UI-файл.
2. **Save/Load — не пассивный presenter.** В отличие от `IDialoguePresenter`/`IChoicePresenter`, которых вызывает `PlaybackController` во время выполнения скрипта, сохранение/загрузку инициирует игрок в произвольный момент — `PlaybackController` тут ничего не оркеструет сам. Нужна новая связующая логика, а не реализация уже существующего интерфейса.

## Расширение `ISaveStorage`

```csharp
public interface ISaveStorage
{
    void Save(string slotId, SaveData data);
    SaveLoadResult Load(string slotId);
    bool SlotExists(string slotId);
}
```

`JsonSaveStorage.SlotExists` — `File.Exists(PathFor(slotId))`. Аддитивное расширение публичного интерфейса (уже смержен в `master`) — единственный ломающий момент: любая СТОРОННЯЯ реализация `ISaveStorage` (гипотетическая, вне этого пакета) перестанет компилироваться без добавления метода. Для личного проекта с одной продакшен-реализацией (`JsonSaveStorage`) это приемлемо и не требует default-реализации интерфейса/versioning.

## `SaveLoadController` (`Runtime/SaveLoad/`)

Простой C#-класс, не `MonoBehaviour` — по духу как `PlaybackController` сам: собирается игрой вручную, без синглтонов.

```csharp
public class SaveLoadController
{
    public SaveLoadController(PlaybackController playback, StoryContext context, string scriptId, ISaveStorage storage);

    public void SaveTo(string slotId);
    public SaveLoadResult LoadInto(string slotId);
    public bool SlotExists(string slotId);
}
```

- `SaveTo` — собирает `SaveData` из `playback.CreateSnapshot()` (`PointerIndex`/`CallStack`) + `context.Variables.Export()` + переданного `scriptId`, зовёт `storage.Save(slotId, data)`.
- `LoadInto` — зовёт `storage.Load(slotId)`; при `Status == Success` восстанавливает `playback.RestoreSnapshot(...)` и `context.Variables.Import(data.Variables)` на ТЕ ЖЕ переданные в конструктор `playback`/`context`; при любом другом статусе не трогает текущее состояние игры (обрыв сессии из-за битого сейва недопустим). Возвращает `SaveLoadResult` как есть — UI сам решает, что показать по `Status`.
- `SlotExists` — прямой проброс к `storage.SlotExists(slotId)`.

`scriptId` не вычисляется автоматически (в `Runtime` сейчас нет типа, однозначно определяющего "какой скрипт сейчас играется по отношению к загруженному `NovelScript`") — передаётся игрой явно при создании контроллера, тот же принцип, что и остальной ручной сборки `StoryContext`.

## `SaveLoadView` (`UI/`)

Тот же паттерн, что `ChoiceView`: фиксированный массив слотов, назначаемых в Inspector, без runtime-инстанцирования префаба.

```csharp
public class SaveLoadView : MonoBehaviour
{
    [Serializable]
    public struct SlotUI
    {
        public string slotId;
        public Button button;
        public TMP_Text label;
    }

    [SerializeField] internal SlotUI[] slots;
    [SerializeField] internal TMP_Text statusText;

    public event Action OnSaveCompleted;
    public event Action OnLoadSucceeded;

    public void Initialize(SaveLoadController controller);
    public void ShowSaveMode();
    public void ShowLoadMode();
}
```

- `Initialize` — внешняя связка с `SaveLoadController`, вызывается игрой один раз после создания обоих.
- `ShowSaveMode()`/`ShowLoadMode()` — переключают режим и перерисовывают сетку: каждый слот получает `label.text` = "Occupied"/"Empty" по `controller.SlotExists(slot.slotId)`. В режиме Load кнопка пустого слота — `button.interactable = false` (бессмысленный клик отсекается на уровне UI, не попыткой загрузки с последующим сообщением об ошибке).
- Клик по слоту:
  - Save → `controller.SaveTo(slotId)` → перерисовать сетку → `OnSaveCompleted?.Invoke()`.
  - Load → `controller.LoadInto(slotId)` → по `SaveLoadResult.Status`: `Success` → `OnLoadSucceeded?.Invoke()`; `NotFound`/`Incompatible` → `statusText.text` с понятным сообщением (не exception, тот же принцип "не exception наружу", что и во всём проекте).
- Что происходит после `OnSaveCompleted`/`OnLoadSucceeded` (закрыть панель, возобновить геймплей) — решает игра через подписку на события; `SaveLoadView` ничего не знает про остальной UI-слой, тот же принцип независимости, что и `DialogueBoxView`/`ChoiceView`.
- Отсутствующее подключение (`slots` пуст, `button`/`label` не назначены на слоте) — `Debug.LogError` + слот пропускается при перерисовке, без падения окна — тот же "missing wiring"-паттерн, что везде в `NovelForge.UI`.

## Тестирование

- **`JsonSaveStorage.SlotExists`** — EditMode-тест на том же temp-directory паттерне, что уже использует `JsonSaveStorageTests` (существующий файл, не создаётся заново).
- **`SaveLoadController`** — чистая логика без UI-зависимости, полностью юнит-тестируема: `SaveTo` → `LoadInto` round-trip через реальный `JsonSaveStorage` на temp-папке (тот же принцип, что `SaveLoadRoundTripTests`); кейсы `NotFound` (пустой слот) и `Incompatible` (руками записанный файл с чужим `SchemaVersion`) — оба не должны трогать переданные `playback`/`context`.
- **`SaveLoadView`** — **исправление после первичного написания этой спеки**: `MonoBehaviour`/UGUI-компоненты в этом проекте УЖЕ имеют установленный, работающий паттерн автотестирования в EditMode — `Tests/UI/ChoiceViewTests.cs` программно создаёт `GameObject`+`Button`+`TMP_Text` (без сцены/Play Mode), крутит корутину через `.MoveNext()`, проверяет состояние кнопок/текста напрямую, с `[TearDown]`-очисткой через `Object.DestroyImmediate`. Утверждение "без автотестов, как ChoiceView" было ошибочным — у ChoiceView есть автотесты, и `SaveLoadView` получает такие же: программная сборка слотов (`GameObject`+`Button`+`TMP_Text` на каждый), вызов `Initialize`/`ShowSaveMode`/`ShowLoadMode`, проверка `label.text`/`button.interactable`/событий напрямую. Никакого Play Mode, никакой ручной проверки не требуется — те же гарантии, что и у остального `NovelForge.UI`.
