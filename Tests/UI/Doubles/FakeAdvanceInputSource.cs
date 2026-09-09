namespace NovelForge.UI.Tests
{
    public class FakeAdvanceInputSource : IAdvanceInputSource
    {
        public bool Pending;

        public bool ConsumeAdvanceRequest()
        {
            if (!Pending)
                return false;

            Pending = false;
            return true;
        }
    }
}
