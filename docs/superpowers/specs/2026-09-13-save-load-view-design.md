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
- **`SaveLoadView`** — `MonoBehaviour`/UGUI, как `ChoiceView`/`DialogueBoxView` — без автотестов, тот же установленный в проекте принцип.

## План живой проверки (Play Mode, впервые для рантайм UI-компонента в этом проекте)

Все предыдущие живые QA-проходы в этом проекте проверяли редакторские инструменты (Editor-режим). Это первая фаза, где живой QA распространяется на рантайм UI — тот же принцип "не доверять код-ревью визуальных/интерактивных вещей", применённый к Play Mode вместо Editor-режима:

1. Собрать минимальную тестовую сцену в `TestProject~` — `Canvas` с несколькими слотами (`Button`+`TMP_Text` на каждый), `statusText`, компонент `SaveLoadView`, и bootstrap-скрипт, вручную создающий `PlaybackController`/`StoryContext`/`JsonSaveStorage`/`SaveLoadController` и вызывающий `SaveLoadView.Initialize(...)`.
2. Войти в Play Mode. Вызвать `ShowLoadMode()` на пустом хранилище — все слоты показывают "Empty", кнопки неактивны.
3. Переключить в `ShowSaveMode()`, кликнуть по слоту — слот сохраняется, сетка обновляется на "Occupied" (переключить обратно в Load-режим и увидеть смену).
4. В Load-режиме кликнуть по занятому слоту — загрузка происходит, `OnLoadSucceeded` срабатывает (проверить, например, логом в тестовом bootstrap-скрипте).
5. Руками испортить `SchemaVersion` в JSON-файле занятого слота на диске, попытаться загрузить — `statusText` показывает сообщение о несовместимости, игра не падает.
6. Проверить консоль на отсутствие непредвиденных ошибок на всех шагах.
7. Удалить тестовую сцену/скрипты/файлы слотов после проверки — как и `.nfscript`-заглушки в предыдущих фазах, ничего не остаётся в проекте.
