namespace NovelForge.Runtime
{
    public class StoryContext
    {
        public VariableStore Variables { get; } = new VariableStore();
        public SceneState Scene { get; } = new SceneState();
        public IDialoguePresenter Dialogue { get; set; }
        public IChoicePresenter Choices { get; set; }
        public IAudioPresenter Audio { get; set; }
        public IBackgroundPresenter Backgrounds { get; set; }
        public ITimingPresenter Timing { get; set; }
        public LocalizationTable Localization { get; set; }
    }
}
