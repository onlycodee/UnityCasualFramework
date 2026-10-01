using System;
using System.Collections.Generic;
using System.IO;

namespace HyperFrame.Services
{
    /// <summary>Where save files live. Swap for cloud save or an obfuscating wrapper (SV-02) later.</summary>
    public interface ISaveStorage
    {
        bool TryRead(string key, out string data);
        void Write(string key, string data);
        void Delete(string key);
    }

    public sealed class InMemorySaveStorage : ISaveStorage
    {
        public readonly Dictionary<string, string> Files = new Dictionary<string, string>();
        public bool TryRead(string key, out string data) => Files.TryGetValue(key, out data);
        public void Write(string key, string data) => Files[key] = data;
        public void Delete(string key) => Files.Remove(key);
    }

    /// <summary>Files under a directory (Application.persistentDataPath in the app). Writes are atomic.</summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        readonly string _directory;

        public FileSaveStorage(string directory)
        {
            _directory = directory;
            Directory.CreateDirectory(directory);
        }

        string PathFor(string key) => Path.Combine(_directory, key + ".json");

        public bool TryRead(string key, out string data)
        {
            var path = PathFor(key);
            if (!File.Exists(path)) { data = null; return false; }
            data = File.ReadAllText(path);
            return true;
        }

        public void Write(string key, string data)
        {
            var path = PathFor(key);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, data);
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }

        public void Delete(string key)
        {
            var path = PathFor(key);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
