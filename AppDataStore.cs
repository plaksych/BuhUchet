using System;
using System.IO;
using Newtonsoft.Json;

namespace BuhUchet
{
    internal static class AppDataStore
    {
        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            Formatting = Formatting.Indented,
            DateFormatString = "yyyy-MM-dd"
        };

        public static string DefaultPath =>
            Path.Combine(AppContext.BaseDirectory, "data.json");

        public static SaveFile LoadOrCreate(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    var initial = new SaveFile
                    {
                        Journal = new(),
                        Counterparties = new(),
                        Accounts = new(),
                        SavedAt = DateTime.Now
                    };
                    Save(path, initial);
                    return initial;
                }

                string json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<SaveFile>(json, JsonSettings) ?? new SaveFile();
            }
            catch
            {
                var fallback = new SaveFile
                {
                    Journal = new(),
                    Counterparties = new(),
                    Accounts = new(),
                    SavedAt = DateTime.Now
                };
                Save(path, fallback);
                return fallback;
            }
        }

        public static void Save(string path, SaveFile data)
        {
            data.SavedAt = DateTime.Now;
            string json = JsonConvert.SerializeObject(data, JsonSettings);
            File.WriteAllText(path, json);
        }
    }
}

