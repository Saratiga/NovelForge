# NovelForge — Редакторские инструменты, часть 1: ScriptedImporter — дизайн

## Цель и рамки

Первая из трёх независимых частей фазы "Редакторские инструменты" (исходная мастер-спека `docs/superpowers/specs/2026-09-05-novelforge-design.md`, раздел "Инструменты редактора", объединяла их в одну фазу — при брейнсторминге решено разбить, т.к. полноценный read/write граф ветвлений на `GraphView` с round-trip сериализацией сам по себе не по масштабу одной фазы наравне с остальными). Даёт скриптам `.nfscript` путь ассета: кастомный `ScriptedImporter`, который на импорте валидирует DSL-текст (ошибка парсинга → ошибка импорта с номером строки в консоли Unity, не билд-ошибка после релиза) и производит `NovelScriptAsset` — ссылку на скрипт, на которую игра может навесить `[SerializeField]`.

**Вне рамок этой фазы**:
- Окно редактора скрипта с подсветкой синтаксиса и автодополнением — вторая часть декомпозиции, отдельная фаза.
- Граф ветвлений (`GraphView`, round-trip сериализация, side-car файл позиций, конфликт-резолюшн) — третья часть, отдельная фаза, сама по себе может потребовать декомпозиции ещё раз.
- Расширяемость под кастомные команды на этапе импорта (в проекте пока нет механизма регистрации кастомных команд для конкретной игры за пределами `CommandRegistry.CreateDefault()` — добавлять точку расширения раньше первого реального потребителя значит спроектировать её вслепую).

## Почему это, в отличие от двух предыдущих фаз, чисто аддитивно

Save/Load и Localization обе трогали уже смерженный код ядра VM. Эта фаза — нет: она добавляет совершенно новую сборку `NovelForge.Editor` и не меняет ни строчки в `NovelForge.Runtime`/`NovelForge.UI`. `ScriptCompiler.Compile(string)` уже публичный и достаточен как есть — импортёр просто вызывает его.

## Почему асcет хранит текст, а не скомпилированный результат

`NovelScript` — обычный C#-класс (не `ScriptableObject`), `Command` — полиморфные объекты без атрибутов сериализации Unity. Компилировать на этапе импорта и пытаться сохранить результат в ассет означало бы либо делать `Command`-иерархию Unity-сериализуемой (большая, самостоятельная работа, которую при этом всё равно не даёт обойти полиморфизм `Command` без кастомного `ISerializationCallbackReceiver`-слоя), либо держать в ассете сразу оба представления (текст + сериализованный IR) и following синхронизировать их — источник дублирования и рассинхрона.

Вместо этого `NovelScriptAsset` хранит только исходный текст. Импорт всё равно гоняет `ScriptCompiler.Compile(...)` — но исключительно ради валидации, результат сразу отбрасывается. Компиляция в реальный `NovelScript` происходит лениво, на стороне игры, при запуске сцены (`ScriptCompiler.Compile(asset.Source)`), тем же способом, каким уже сегодня любой тест получает `NovelScript` из сырой строки. Компиляция скрипта такого масштаба — миллисекунды, преждевременная оптимизация не нужна.

## Модуль и файлы

Новая сборка `Editor/NovelForge.Editor.asmdef` — editor-only (`"includePlatforms": ["Editor"]`), `"references": ["NovelForge.Runtime"]`. Первая Editor-сборка в проекте.

- **`Runtime/Parsing/NovelScriptAsset.cs`**:
  ```csharp
  using UnityEngine;

  namespace NovelForge.Runtime
  {
      public class NovelScriptAsset : ScriptableObject
      {
          [TextArea(10, 40)]
          public string Source;
      }
  }
  ```
  Публичное поле, не `[SerializeField] private` со свойством — игра читает `Source` напрямую для собственного вызова `ScriptCompiler.Compile(...)`; это не "конфигурационный" ассет со скрытой логикой, а простой контейнер текста.

  Ассет живёт в `NovelForge.Runtime`, а не в `NovelForge.Editor` — вывод по итогам финального ревью: раз игра должна уметь навесить на него `[SerializeField]` и сериализовать ссылку, класс обязан жить в обычной (не editor-only) сборке, потому что non-editor сборка не может ссылаться на editor-only сборку — в билде плеера её попросту нет. `[TextArea]` — плейн-атрибут `UnityEngine`, доступен в runtime-сборках без проблем, так что перенос не добавляет новых зависимостей.

- **`Editor/NovelScriptImporter.cs`**:
  ```csharp
  using System.IO;
  using UnityEditor.AssetImporters;
  using NovelForge.Runtime;

  namespace NovelForge.Editor
  {
      [ScriptedImporter(1, "nfscript")]
      public class NovelScriptImporter : ScriptedImporter
      {
          public override void OnImportAsset(AssetImportContext ctx)
          {
              string source = File.ReadAllText(ctx.assetPath);

              try
              {
                  new ScriptCompiler().Compile(source);
              }
              catch (ParseException e)
              {
                  ctx.LogImportError($"NovelForge: {e.Message}");
              }

              var asset = ScriptableObject.CreateInstance<NovelScriptAsset>();
              asset.Source = source;
              ctx.AddObjectToAsset("main", asset);
              ctx.SetMainObject(asset);
          }
      }
  }
  ```
  Компиляция при импорте — исключительно ради валидации, результат отброшен. `ParseException.Message` уже содержит `"Line N: ..."` (см. `Runtime/Parsing/ParseException.cs`), так что номер строки автоматически попадает в консоль. Ассет создаётся в обоих случаях (валидный и невалидный скрипт) — автор видит файл в проекте и может его поправить прямо там, импорт не блокируется целиком из-за одной ошибки.

Без кастомного `Inspector` в этой фазе — дефолтный `[TextArea]`-виджет уже показывает текст читаемо; окно редактора (будущая фаза) полностью его заменит.

## Тестирование

Новый `Tests/Editor/NovelForge.Editor.Tests.asmdef` (editor-only, `"references": ["NovelForge.Runtime", "NovelForge.Editor"]`, `precompiledReferences: ["nunit.framework.dll"]`, по образцу `NovelForge.Runtime.Tests`/`NovelForge.UI.Tests`).

Тесты гоняют реальный импорт через `AssetDatabase`, не мокают Unity API — тот же принцип, что `JsonSaveStorage`-тесты используют реальную файловую систему:

- Тест пишет `.nfscript`-файл в `TestProject~/Assets/Temp_NovelScriptImporterTests/<name>.nfscript` (абсолютный путь через `Application.dataPath`, Unity-путь для `AssetDatabase` — `"Assets/Temp_NovelScriptImporterTests/<name>.nfscript"`), вызывает `AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport)`, затем `AssetDatabase.LoadAssetAtPath<NovelScriptAsset>(path)`.
- **Валидный скрипт** → ассет создан, `asset.Source` совпадает с записанным текстом, ошибок в консоли нет.
- **Невалидный скрипт** (например, `jump nowhere` без метки) → ассет всё равно создан с исходным текстом, `LogAssert.Expect(LogType.Error, ...)` на сообщение импортёра. Точный формат, в котором Unity показывает `AssetImportContext.LogImportError` в консоли (добавляет ли сама Unity префикс с путём файла к переданному тексту, или строка ровно та, что передана) — не задокументированное однозначно поведение; подтверждается эмпирически на этапе выполнения первого теста, план фиксируется по факту — тот же принцип, что уже применялся к версиям пакетов в прошлых фазах.
- `[TearDown]` — `AssetDatabase.DeleteAsset(path)` (сам удаляет и `.meta`).

`Assets/` внутри `TestProject~` не в репозитории (гитигнорится целиком), так что временные файлы тестов не загрязняют git.

Визуальных компонентов в этой фазе нет.
