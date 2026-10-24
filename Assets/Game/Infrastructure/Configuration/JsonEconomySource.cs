using System;
using UnityEngine;

namespace Game.Infrastructure.Configuration
{
    public static class JsonEconomySource
    {
        public static int InitialGold(string json)
        {
            var data = JsonUtility.FromJson<Document>(json);
            if (data == null || data.schemaVersion != 1 || data.initialGold < 0)
                throw new FormatException("Invalid economy configuration.");
            return data.initialGold;
        }
        [Serializable] private sealed class Document
        {
            public int schemaVersion;
            public int initialGold;
        }
    }
}
