using System.Collections;

namespace NovelForge.Runtime.Tests
{
    public static class CoroutineTestUtil
    {
        public static void RunToCompletion(IEnumerator routine)
        {
            while (routine.MoveNext())
            {
                if (routine.Current is IEnumerator nested)
                    RunToCompletion(nested);
            }
        }
    }
}
