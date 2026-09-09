using UnityEngine.UI;

namespace NovelForge.UI
{
    public class ButtonAdvanceInputSource : IAdvanceInputSource
    {
        private bool _pending;

        public ButtonAdvanceInputSource(Button button)
        {
            button.onClick.AddListener(() => _pending = true);
        }

        public bool ConsumeAdvanceRequest()
        {
            if (!_pending)
                return false;

            _pending = false;
            return true;
        }
    }
}
