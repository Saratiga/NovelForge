using System.Linq;
using NovelForge.Runtime;
using NovelForge.UI;
using UnityEngine;
using UnityEngine.UI;

public class DemoFlow : MonoBehaviour
{
    private const string ScriptId = "getting-started";

    [SerializeField] private NovelRunner runner;
    [SerializeField] private NovelScriptAsset story;
    [SerializeField] private GameObject titleScreen;
    [SerializeField] private GameObject gameplayUi;
    [SerializeField] private GameObject saveLoadPanel;
    [SerializeField] private SaveLoadView saveLoadView;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button closeSaveLoadButton;
    // Must match the slot ids DemoSceneBuilder assigns to saveLoadView.slots — used only to
    // decide whether the title screen's "Load Game" button should be enabled; SaveLoadView
    // itself is the source of truth for which of these slots (if any) is actually occupied.
    [SerializeField] private string[] slotIds = { "slot-0", "slot-1" };

    private JsonSaveStorage _storage;
    private SaveLoadController _saveLoad;

    private void Awake()
    {
        newGameButton.onClick.AddListener(OnNewGame);
        loadGameButton.onClick.AddListener(OnOpenLoad);
        saveButton.onClick.AddListener(OnOpenSave);
        closeSaveLoadButton.onClick.AddListener(() => saveLoadPanel.SetActive(false));
        saveLoadView.OnSaveCompleted += () => saveLoadPanel.SetActive(false);
        saveLoadView.OnLoadSucceeded += OnLoadSucceeded;
        runner.OnFinished += ShowTitle;
    }

    private void Start()
    {
        _storage = new JsonSaveStorage();
        ShowTitle();
    }

    private void ShowTitle()
    {
        titleScreen.SetActive(true);
        gameplayUi.SetActive(false);
        saveLoadPanel.SetActive(false);
        loadGameButton.interactable = slotIds.Any(_storage.SlotExists);
    }

    private void OnNewGame()
    {
        titleScreen.SetActive(false);
        gameplayUi.SetActive(true);
        runner.Prepare(story);
        _saveLoad = new SaveLoadController(runner.Playback, runner.Context, ScriptId, _storage);
        saveLoadView.Initialize(_saveLoad);
        runner.Begin();
    }

    private void OnOpenLoad()
    {
        runner.Prepare(story);
        _saveLoad = new SaveLoadController(runner.Playback, runner.Context, ScriptId, _storage);
        saveLoadView.Initialize(_saveLoad);
        saveLoadPanel.SetActive(true);
        saveLoadView.ShowLoadMode();
    }

    private void OnOpenSave()
    {
        saveLoadPanel.SetActive(true);
        saveLoadView.ShowSaveMode();
    }

    private void OnLoadSucceeded()
    {
        saveLoadPanel.SetActive(false);
        titleScreen.SetActive(false);
        gameplayUi.SetActive(true);
        runner.Begin();
    }
}
