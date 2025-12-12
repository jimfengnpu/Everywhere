namespace Everywhere.Extensions;

public static class ListExtension
{
    extension<TList>(TList list) where TList : IList
    {
        public void SafeRemoveAt(int index)
        {
            if (index >= 0 && index < list.Count)
            {
                list.RemoveAt(index);
            }
        }
    }
}