# Бэклог NovelForge

Что осталось после архитектурного обзора от 2026-10-07 и трёх планов исправлений
(свои команды, save/load, уборка DSL). Порядок внутри разделов — по тому, насколько
это заметно игроку или разработчику.

## Проверить вручную

- [x] Пройти демо GettingStarted в Play Mode: сохраниться в середине, выйти на титульный
  экран, загрузиться. Фон, музыка и актёр должны появиться без мерцания до следующей
  реплики.
  Сделано автоматически: `Tests/PlayMode/SaveLoadPlayModeTests.cs` собирает сцену как в
  демо и проходит запуск → сохранение → перезапуск посреди истории → загрузку на живых
  кадрах (`unity test --mode PlayMode`). Сами UI-кнопки демо и визуальное мерцание тест
  не проверяет.
- [x] Собрать IL2CPP-билд со stripping Low или выше и проверить свою команду. Классы
  команд используются только через рефлексию и могут вырезаться — тогда в билде будет
  `Unknown command`. Если подтвердится: наследовать `NovelCommandAttribute` от
  `PreserveAttribute` или требовать `[Preserve]` на классах команд.
  Проверено и исправлено: в Windows IL2CPP-билде со stripping High класс команды
  сохранялся, но конструктор `(string rawArgs)` вырезался — команда становилась
  `Unknown command`. Теперь `Editor/NovelCommandLinkXml.cs` (`IUnityLinkerProcessor`)
  генерирует `link.xml`, сохраняющий все классы с `[NovelCommand]`. Проверка повторяема:
  `TestProject~/Assets/Il2CppSmoke` — `unity build TestProject~ --target StandaloneWindows64
  --execute-method NovelForgeSmoke.BuildSmoke.Build --output-path <exe>`, затем запустить
  exe с `-batchmode -nographics -logFile <log>` и найти в логе `NOVELFORGE_SMOKE PASS`.

## Заметные проблемы

- [x] Повторный показ сцены при загрузке только добавляет на экран
  (`SceneState.Replay`). Если в той же сессии загрузить другое сохранение, старые
  актёры и музыка остаются. Нужен `Clear()` у презентеров перед показом.
  Сделано: `ResetView()` у `ActorView`, `DialogueBoxView`, `DialoguePresenter`,
  `BackgroundPresenter`, `AudioPresenter`; `NovelRunner.Prepare` очищает экран.
- [x] `NovelScriptImporter` не зависит от набора команд. Если `.nfscript` импортирован
  раньше, чем появился класс команды, ошибка `Unknown command` висит до ручного
  Reimport. Решение: `ctx.DependsOnCustomDependency(...)` в импортёре и
  `AssetDatabase.RegisterCustomDependency(...)` с хешем имён команд в `[InitializeOnLoad]`.
  Сделано: `Editor/NovelCommandDependency.cs`, импортёр версии 2.
- [x] Позиции до первой метки хранятся как абсолютный номер команды
  (`NovelScript.ToPosition`). Правка в этой части скрипта молча сдвигает сохранение.
  Сохранение после конца истории (`{ null, Commands.Count }`) начнётся с середины, если
  скрипт потом удлинят.
  Сделано: конец истории сохраняется как `NovelScript.FinishedOffset` и всегда грузится
  завершённым; смещение за началом первой метки даёт предупреждение и старт с начала.
  Сдвиг при правке внутри части до первой метки остаётся (описан в `USAGE.md`).

## Из первого обзора, в планы не вошло

- [x] Возможный квадратичный перебор (поиск по списку внутри цикла):
  `GraphDocument.GetBrokenReferences`, `GraphDocument.GetUnreachableLabels`,
  `CharacterUsageValidator.Validate`, `DslAutocompleteProvider.GetCandidates`.
  Проверить и при необходимости заменить на `HashSet`/`Dictionary`.
  Проверено: ложная тревога — везде уже `HashSet`, индексатор принял
  `HashSet.Contains` за линейный поиск.
- [x] Цикл вызовов из 11 методов в `NovelBranchGraphWindow` (`RebuildGraphView`,
  `ApplyNodes`, `ReplaceNode`, `On*Changed`, `AddNewLabel`, `DeleteNode`,
  `BranchGraphView.Populate` и др.). Проверить, не перестраивается ли весь граф на
  каждое нажатие клавиши.
  Проверено: правки применяются по потере фокуса, не по клавише. Но текст блока и
  вариантов выбора перестраивали граф и при потере фокуса без изменений — исправлено.
- [x] Цикл вызовов `SaveLoadView.Refresh` ↔ `OnSlotClicked`. Проверить, нет ли лишних
  перерисовок.
  Проверено: ложная тревога — один `Refresh` после сохранения, рекурсии нет.
- [x] Раскладка узлов окна графа хранится в `<скрипт>.nfgraph.meta` рядом со скриптом
  (так решено в `docs/superpowers/specs/2026-09-12-editor-branch-graph-design.md`).
  Unity принимает файл за `.meta` несуществующего ассета и при каждом Refresh пишет
  ошибку «does not have a valid GUID». Нужно другое место: например, `userData`
  импортёра скрипта, `Library/` или файл с расширением, которое Unity не трактует как
  `.meta`. Найдено при написании тестов окна графа.
  Сделано: раскладка в `<скрипт>.nfgraph.json`; старый `.nfgraph.meta` переносится в новый
  файл при первом открытии графа и удаляется.

## Качество кода

- [x] Предупреждение анализатора Unity UAC0005 на `AppDomain.GetAssemblies()` в
  `Runtime/Parsing/CommandRegistry.cs`: может вернуть выгруженные сборки. Unity советует
  `CurrentAssemblies.GetLoadedAssemblies()` или `TypeCache`.
- [x] Generic-класс с `[NovelCommand]` проходит проверку и падает при компиляции скрипта
  с непонятным сообщением. Отклонять при `type.ContainsGenericParameters`.
- [x] `DslGrammar.Keywords` — массив, который можно изменить приведением к `string[]`.
  Заменить на `Array.AsReadOnly(...)`.
- [x] `DslGrammar` целиком публичный в пакете версии 0.1.0. Можно сделать `internal` и
  добавить `InternalsVisibleTo("NovelForge.Editor")`.
- [x] Описать в комментарии, что `NovelRunner.Timing` читается только в `Prepare` и может
  быть `null` (тогда `wait` пишет ошибку и пропускается).
- [x] Описать в документации пакета, что сборки, загруженные после первого поиска
  команд (например, через `Assembly.Load` во время игры), не учитываются. Сейчас это
  есть только в `USAGE.md`.

## Из ревью ветки исправлений бэклога (2026-10-12)

- [ ] `Editor/NovelCommandDependency.cs`: `[InitializeOnLoad]` выполняется и в процессах
  Asset Import Worker, где `SessionState` пуст, — каждый такой процесс регистрирует
  зависимость и ставит `AssetDatabase.Refresh`. Выходить раньше при
  `AssetDatabase.IsAssetImportWorkerProcess()`. Не проверено на практике.
- [ ] `AudioPresenter.ResetView` и `OnDisable` вызывают `voice.Stop()` без проверки на
  `null`: если голос sfx уничтожили, `Prepare` упадёт с `MissingReferenceException`.
- [ ] `AudioPresenter.ResetView` очищает `_busySfxSources`, пока старый `PlaySfx` может
  ещё идти; сейчас это безопасно только потому, что `Prepare` сначала останавливает
  корутины. Для самостоятельного `ResetView` нужен счётчик поколений у sfx.
- [ ] `NovelScriptImporterTests.ChangingTheCommandSet_ReimportsScripts` переимпортирует все
  `.nfscript` проекта-хоста: в проекте пользователя со скриптами с ошибками тест упадёт
  на чужих логах ошибок.
- [x] Остановка идущего воспроизведения в `NovelRunner.Prepare` (`StopAllCoroutines`) не
  покрыта тестом: в EditMode корутины `MonoBehaviour` не продвигаются. Нужен PlayMode-тест.
  Сделано: покрыто PlayMode-тестом выше.

## Из ревью PlayMode-теста и IL2CPP-проверки (2026-10-12)

- [ ] С перезагрузкой домена при входе в Play Mode (по умолчанию у большинства
  пользователей) batch-прогоны PlayMode-тестов периодически висят до таймаута: в логе сразу
  после перезагрузки «The referenced script (Unknown) on this Behaviour is missing!», потом
  простой. В `TestProject~/ProjectSettings/EditorSettings.asset` перезагрузка отключена
  (`m_EnterPlayModeOptions: 1`) — это обход, причина не найдена. Прогон, убитый по таймауту,
  оставляет `Assets/InitTestScene*.unity`, после чего следующие прогоны тоже виснут.
- [ ] `SaveLoadPlayModeTests.MusicPlays` проверяет только назначенный клип; остановленный
  источник или громкость 0 тоже пройдут. Добавить `volume > 0`.
- [ ] PlayMode-тест сохраняет и загружает через свой `SaveLoadController`, а не через
  `SaveLoadView` и титульный экран демо; имя теста шире того, что он покрывает.
- [ ] `NovelCommandLinkXml` пишет файл по пути от текущей папки процесса
  (`Path.GetFullPath("Temp/...")`); надёжнее от `Application.dataPath`. Имена типов не
  экранируются для XML (для допустимых имён C# безвредно).
- [ ] `BuildSmoke` открывает новую сцену в режиме `Single` — при ручном запуске в открытом
  редакторе потеряет несохранённую сцену. Ограничить batch-режимом или спросить про
  сохранение. После билда `ProjectSettings.asset` перезаписывается с другими переводами
  строк — откатывать перед коммитом.

## Недостающие тесты

- [x] Полный цикл парсинг → сериализация в `GraphDocumentParser`/`GraphDocumentFormatter`
  для граничных случаев `BuildNode`: пустой блок, блок из одних комментариев, варианты
  выбора без `choice`.
- [x] Отдельные тесты для `DslGrammar.TrailingPosition` и `DslGrammar.IdTag`.
- [x] Сохранение после конца истории → загрузка → `PlaybackController.IsFinished`.
- [x] Совпадение имён двух своих команд с `[NovelCommand]`.
- [x] Автодополнение меток: `label\tfoo` не предлагается.
- [x] (Низкий приоритет) `CommandRegistry.ScanCountForTesting++` не атомарен — влияет
  только на тест. Сделано: `Interlocked.Increment`.
