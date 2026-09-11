# NovelForge — Редактор персонажей — дизайн

## Цель и рамки

Собственное `EditorWindow` для `CharacterDefinition`: обзорный список всех персонажей проекта + панель деталей выбранного (id, имя, цвет реплик, список поз "эмоция → спрайт" с превью), плюс блок сверки использования персонажа в `.nfscript`-скриптах ("эта эмоция встречается в диалогах, но для неё нет позы" / "эта поза определена, но нигде не используется").

Не входила в исходный список "Инструменты редактора" мастер-спеки (`docs/superpowers/specs/2026-09-05-novelforge-design.md`, там были только редактор сценариев и граф ветвлений) — отдельная фаза, добавленная по итогам двух уже смерженных частей редакторских инструментов (`ScriptedImporter`, окно редактора скрипта). Граф ветвлений (`GraphView`, round-trip сериализация) по-прежнему вне рамок — отдельная будущая фаза, если понадобится.

**Вне рамок этой фазы**:
- Удаление персонажа из окна — обычное удаление ассета через Project-панель Unity, дублировать не нужно.
- Массовое редактирование нескольких персонажей одновременно.
- Автоисправление проблем валидации (авто-добавление позы под найденную неопределённую эмоцию, авто-удаление неиспользуемой) — только отображение, правки руками.
- Превью анимации/переходов позы — только статичная спрайт-картинка.

## Почему это аддитивно

Не трогает `NovelForge.UI`. В `NovelForge.Runtime` — одно точечное дополнение: `SayLineCommand` получает два публичных read-only свойства (`CharacterId`, `Emotion`) поверх уже существующих `private readonly`-полей — только для чтения editor-тулингом, не меняет поведение выполнения. Всё остальное — новые файлы в `NovelForge.Editor`.

## Окно и точки входа

`Editor/CharacterEditorWindow.cs`, master-detail layout:

- Открывается через пункт меню `NovelForge/Character Editor` (`[MenuItem]`).
- Двойной клик по `CharacterDefinition`-ассету в Project-панели (`[OnOpenAsset]`, тот же приём, что у `NovelScriptEditorWindow`/`.nfscript`) открывает окно и сразу выбирает этот персонаж. Если окно уже открыто — фокусирует существующее и переключает выбор, а не открывает второе.
- Слева — прокручиваемый список всех `CharacterDefinition` в проекте (`AssetDatabase.FindAssets("t:CharacterDefinition")`, отображается `DisplayName`, если пусто — `Id`, если и то пусто — имя файла), плюс кнопка "New Character" внизу списка: открывает `EditorUtility.SaveFilePanelInProject`, создаёт новый ассет через `ScriptableObject.CreateInstance<CharacterDefinition>()` + `AssetDatabase.CreateAsset`, сразу выбирает его.
- Справа — панель деталей выбранного персонажа. Если ничего не выбрано — плейсхолдер-подсказка.

## Панель деталей: редактирование через `SerializedObject`

Поля `CharacterDefinition` — `[SerializeField] internal`, без публичных сеттеров. Вместо того чтобы трогать их доступность (что открыло бы прямую мутацию рантайм-кода откуда угодно в сборке), редактирование идёт через `SerializedObject`/`SerializedProperty` — стандартный Unity-идиом для кастомных редакторов, который:
- работает с `internal`-полями наравне с `public` (сериализация видит их независимо от C#-модификатора доступа);
- даёт Undo/Redo бесплатно (`Undo.RecordObject` не нужен вручную — `SerializedObject` сам интегрирован с Undo-стеком);
- сам помечает ассет dirty при `ApplyModifiedProperties()` — Unity персистит его как обычный ассет (при сохранении проекта/потере фокуса), отдельная кнопка "Save" и ручной `File`/`AssetDatabase.SaveAssets` не нужны.

Поля панели:
- `id`, `displayName` — `EditorGUILayout.PropertyField` (текстовые поля).
- `nameColor` — `PropertyField` (даёt `ColorField` из коробки по типу `Color`).
- `poses` (массив `Pose{emotion, sprite}`) — построчный вывод без `EditorGUILayout.PropertyField(posesProp, true)` "из коробки" (даёт неудобный default array UI): вручную по `posesProp.GetArrayElementAtIndex(i)`, в каждой строке — текстовое поле `emotion`, `ObjectField` для `sprite`, превью-миниатюра (`EditorGUI.DrawPreviewTexture` с `sprite.texture`, фиксированный небольшой квадрат — простой, не завязанный на IMGUI-оверлеи техника, без риска той хрупкости, что была у подсветки синтаксиса), кнопка "✕" удалить строку (`posesProp.DeleteArrayElementAtIndex`). Под списком — кнопка "+ Add Pose" (`posesProp.InsertArrayElementAtIndex` в конец, новая строка с пустыми `emotion`/`sprite`).

## Валидация использования в скриптах

Новый чистый класс **`Editor/CharacterUsageValidator.cs`**:

```csharp
public static class CharacterUsageValidator
{
    public readonly struct Result
    {
        public IReadOnlyList<string> UsedButNotDefined { get; }
        public IReadOnlyList<string> DefinedButUnused { get; }
    }

    public static Result Validate(CharacterDefinition character, IEnumerable<NovelScript> scripts);
}
```

- Проходит по `script.Commands` каждого переданного `NovelScript`, отбирает `SayLineCommand`, где `CharacterId == character.Id` и `Emotion != null`, собирает множество использованных эмоций.
- Сравнивает с `character`'s позами (через существующий `TryGetSprite` по каждой встреченной эмоции — не нужен прямой доступ к `internal poses`, интерфейс уже публичный):
  - `UsedButNotDefined` — эмоции из скриптов, для которых `TryGetSprite` возвращает `false`.
  - `DefinedButUnused` — для полноты нужен список всех определённых эмоций персонажа: раз `poses` `internal`, в самом валидаторе (класс `NovelForge.Editor`) массив недоступен по C#-модификатору — читается через `SerializedObject` на переданном `character` (тот же механизм, что и для редактирования), не как рантайм-API.

Чистая функция, без обращения к `EditorWindow`/`AssetDatabase` внутри — на вход получает уже готовый `CharacterDefinition` и коллекцию `NovelScript`, легко юнит-тестируется на сконструированных вручную объектах.

Сборка входных данных — в `CharacterEditorWindow` (не в валидаторе): `AssetDatabase.FindAssets("t:NovelScriptAsset")`, для каждого — `new ScriptCompiler().Compile(asset.Source)` в try/catch; скрипт с ошибкой компиляции пропускается с `Debug.LogWarning(path + ": " + exception.Message)` — окно не падает из-за постороннего битого скрипта, тот же принцип "не exception наружу", что и в остальной обработке ошибок проекта. Пересчёт — синхронно при каждом выборе персонажа в списке (компиляция всех скриптов проекта — операция на миллисекунды, как уже установлено в части 2 фазы редакторских инструментов; дебаунс/кеш не нужны).

Отображение — под позами в панели деталей два списка (простые `EditorGUILayout.HelpBox`, без Foldout/сворачивания — тот же принцип "проще и надёжнее", что и для превью спрайта): "Used but not defined" (`MessageType.Warning` на каждую строку) и "Defined but unused" (`MessageType.Info`); список без элементов не отображается вовсе, оба списка пустые — не рисуется весь блок "Usage".

## Тестирование

- **`CharacterUsageValidator`** — EditMode-тесты на сконструированных `CharacterDefinition` (через `ScriptableObject.CreateInstance` + `SerializedObject` для заполнения `poses` в тесте) и вручную собранных `NovelScript` (напрямую через `new NovelScript(commands, labels)` со списком `SayLineCommand`, без прохода через `ScriptCompiler`): пустой пересекающийся случай, эмоция используется и определена (не в обоих списках), используется без позы, определена без использования, персонаж без единой реплики в скриптах.
- **`CharacterEditorWindow`** — не тестируется автоматически, как и `NovelScriptEditorWindow`. Проверяется живым QA перед мержем (см. ниже).

## План живой проверки (обязательный шаг этой фазы)

После случая с полностью нерабочим смерженным оверлеем подсветки синтаксиса в прошлой части — живой QA через computer-use закладывается как штатный шаг выполнения этой фазы, до финального ревью и мержа, а не как проверка постфактум:
1. Открыть окно через меню, убедиться, что список персонажей проекта отображается.
2. Создать нового персонажа через "New Character", заполнить поля, добавить позу со спрайтом — увидеть превью.
3. Открыть существующего персонажа двойным кликом по ассету — окно фокусируется/выбирает его.
4. На персонаже, реально используемом в тестовом `.nfscript` с известными `#emotion`-тегами — увидеть корректные списки "Used but not defined"/"Defined but unused".
5. Закрыть и переоткрыть Unity/окно — убедиться, что изменения персистентны (сохранены через `SerializedObject`, не потеряны).
