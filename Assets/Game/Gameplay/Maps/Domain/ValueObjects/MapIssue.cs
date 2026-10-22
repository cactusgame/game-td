namespace Game.Maps.Domain
{
    public sealed class MapIssue
    {
        public bool IsError { get; }
        public string Message { get; }
        public string ObjectId { get; }
        public int X { get; }
        public int Y { get; }
        public MapIssue(string message, int x = -1, int y = -1, string objectId = null, bool isError = true)
        {
            Message = message;
            X = x;
            Y = y;
            ObjectId = objectId;
            IsError = isError;
        }
    }

}
