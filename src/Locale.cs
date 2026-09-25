using System.Collections.Generic;
using System.IO;
using Colossal;
using Newtonsoft.Json;

namespace SuperTrafficSpawnBlock
{
    // These same JSON files are also shipped as loose lang/*.json for I18NEveryWhere.
    // Only built-in languages are registered here, so community locales can fill missing entries.
    internal sealed class Locale : IDictionarySource
    {
        private readonly Dictionary<string, string> entries;
        public Locale(string locale)
        {
            using (var stream = typeof(Locale).Assembly.GetManifestResourceStream("SuperTrafficSpawnBlock.lang." + locale + ".json"))
            using (var reader = new StreamReader(stream))
                entries = JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd());
        }
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts) => entries;
        public void Unload() { }
    }
}
