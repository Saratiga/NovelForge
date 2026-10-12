# Бэклог NovelForge

Что осталось после архитектурного обзора от 2026-10-07 и трёх планов исправлений
(свои команды, save/load, уборка DSL). Порядок внутри разделов — по тому, насколько
это заметно игроку или разработчику.

## Проверить вручную

- [ ] Пройти демо GettingStarted в Play Mode: сохраниться в середине, выйти на титульный
  экран, загрузиться. Фон, музыка и актёр должны появиться без мерцания до следующей
  реплики.
- [ ] Собрать IL2CPP-билд со stripping Low или выше и проверить свою команду. Классы
  команд используются только через рефлексию и могут вырезаться — тогда в билде будет
  `Unknown command`. Если подтвердится: наследовать `NovelCommandAttribute` от
  `PreserveAttribute` или требовать `[Preserve]` на классах команд.

## Заметные проблемы

- [x] Повторный показ сцены при загрузке только добавляет на экран
  (`SceneState.Replay`). Если в той же сессии загрузить другое сохранение, старые
  актёры и музыка остаются. Нужен `Clear()` у презентеров перед показом.
  Сделано: `ResetView()` у `ActorView`, `DialogueBoxView`, `DialoguePresenter`,
  `BackgroundPresenter`, `AudioPresenter`; `NovelRunner.Prepare` очищает экран.
- [ ] `NovelScriptImporter` не зависит от набора команд. Если `.nfscript` импортирован
  раньше, чем появился класс команды, ошибка `Unknown command` висит до ручного
  Reimport. Решение: `ctx.DependsOnCustomDependency(...)` в импортёре и
  `AssetDatabase.RegisterCustomDependency(...)` с хешем имён команд в `[InitializeOnLoad]`.
- [x] Позиции до первой метки хранятся как абсолютный номер команды
  (`NovelScript.ToPosition`). Правка в этой части скрипта молча сдвигает сохранение.
  Сохранение после конца истории (`{ null, Commands.Count }`) начнётся с середины, если
  скрипт потом удлинят.
  Сделано: конец истории сохраняется как `NovelScript.FinishedOffset` и всегда грузится
  завершённым; смещение за началом первой метки даёт предупреждение и старт с начала.
  Сдвиг при правке внутри части до первой метки остаётся (описан в `USAGE.md`).

## Из первого обзора, в планы не вошло

- [ ] Возможный квадратичный перебор (поиск по списку внутри цикла):
  `GraphDocument.GetBrokenReferences`, `GraphDocument.GetUnreachableLabels`,
  `CharacterUsageValidator.Validate`, `DslAutocompleteProvider.GetCandidates`.
  Проверить и при необходимости заменить на `HashSet`/`Dictionary`.
- [ ] Цикл вызовов из 11 методов в `NovelBranchGraphWindow` (`RebuildGraphView`,
  `ApplyNodes`, `ReplaceNode`, `On*Changed`, `AddNewLabel`, `DeleteNode`,
  `BranchGraphView.Populate` и др.). Проверить, не перестраивается ли весь граф на
  каждое нажатие клавиши.
- [ ] Цикл вызовов `SaveLoadView.Refresh` ↔ `OnSlotClicked`. Проверить, нет ли лишних
  перерисовок.

## Качество кода

- [ ] Предупреждение анализатора Unity UAC0005 на `AppDomain.GetAssemblies()` в
  `Runtime/Parsing/CommandRegistry.cs`: может вернуть выгруженные сборки. Unity советует
  `CurrentAssemblies.GetLoadedAssemblies()` или `TypeCache`.
- [ ] Generic-класс с `[NovelCommand]` проходит проверку и падает при компиляции скрипта
  с непонятным сообщением. Отклонять при `type.ContainsGenericParameters`.
- [ ] `DslGrammar.Keywords` — массив, который можно изменить приведением к `string[]`.
  Заменить на `Array.AsReadOnly(...)`.
- [ ] `DslGrammar` целиком публичный в пакете версии 0.1.0. Можно сделать `internal` и
  добавить `InternalsVisibleTo("NovelForge.Editor")`.
- [ ] Описать в комментарии, что `NovelRunner.Timing` читается только в `Prepare` и может
  быть `null` (тогда `wait` пишет ошибку и пропускается).
- [ ] Описать в документации пакета, что сборки, загруженные после первого поиска
  команд (например, через `Assembly.Load` во время игры), не учитываются. Сейчас это
  есть только в `USAGE.md`.

## Недостающие тесты

- [ ] Полный цикл парсинг → сериализация в `GraphDocumentParser`/`GraphDocumentFormatter`
  для граничных случаев `BuildNode`: пустой блок, блок из одних комментариев, варианты
  выбора без `choice`.
- [ ] Отдельные тесты для `DslGrammar.TrailingPosition` и `DslGrammar.IdTag`.
- [x] Сохранение после конца истории → загрузка → `PlaybackController.IsFinished`.
- [ ] Совпадение имён двух своих команд с `[NovelCommand]`.
- [ ] Автодополнение меток: `label\tfoo` не предлагается.
- [ ] (Низкий приоритет) `CommandRegistry.ScanCountForTesting++` не атомарен — влияет
  только на тест.
