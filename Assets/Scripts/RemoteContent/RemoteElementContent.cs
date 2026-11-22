using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using InteracrSystem;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace DataScripts
{
    // Attach to one scene object. Consumers can use cached data in Start or subscribe to ContentChanged.
    public class RemoteElementContent : MonoBehaviour
    {
        public static RemoteElementContent Instance { get; private set; }
        [SerializeField] private string _catalogUrl =
            "https://zgmwmaustzdsqybamalu.supabase.co/storage/v1/object/public/element-content/data/elements.example.json";
        [SerializeField] private TextAsset _fallbackCatalog;
        [SerializeField] private Transform _elementsRoot;
        [SerializeField] private int _timeoutSeconds = 20;
        public event Action ContentChanged;
        public bool IsUpdating { get; private set; }
        public bool InitialCheckComplete { get; private set; }
        public string Status { get; private set; } = "Чтение сохранённых данных…";
        public float Progress { get; private set; }
        public bool HasContent => _catalog != null;
        public string LastError { get; private set; }
        public int Version => _catalog != null ? _catalog.version : 0;

        private ElementCatalog _catalog;
        private string _activeDirectory;
        private string _root;
        private readonly Dictionary<string, RemoteElement> _entries = new Dictionary<string, RemoteElement>();
        private readonly Dictionary<string, RemoteElement> _bundledEntries = new Dictionary<string, RemoteElement>();
        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        // Old objects stay valid if a UI still displays them after an update.
        private readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();
        private bool _refreshRequested;

        private void Awake()
        {
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
            _root = Path.Combine(Application.persistentDataPath, "ElementContent");
            try
            {
                Directory.CreateDirectory(_root);
                string pointer = Path.Combine(_root, "active.txt");
                if (File.Exists(pointer))
                {
                    string key = File.ReadAllText(pointer).Trim();
                    if (key.Length != 64 || !IsHex(key)) throw new IOException("Invalid cache pointer.");
                    string directory = Path.Combine(_root, key);
                    string json = File.ReadAllText(Path.Combine(directory, "catalog.json"));
                    var catalog = Parse(json);
                    if (!CacheComplete(catalog, directory)) throw new IOException("Incomplete content cache.");
                    Apply(catalog, directory);
                }
            }
            catch (Exception e) { Report("Не удалось прочитать кеш: " + e.Message); }
            if (!_fallbackCatalog) _fallbackCatalog = Resources.Load<TextAsset>("ElementContent/elements");
            if (_fallbackCatalog)
            {
                try { foreach (var entry in Parse(_fallbackCatalog.text).elements) _bundledEntries[entry.id] = entry; }
                catch (Exception e) { Report("Ошибка базового JSON: " + e.Message); }
            }
            if (!HasContent && _fallbackCatalog)
            {
                try { Apply(Parse(_fallbackCatalog.text), null); }
                catch (Exception e) { Report("Ошибка базового JSON: " + e.Message); }
            }
        }

        private IEnumerator Start()
        {
            if (!string.IsNullOrWhiteSpace(_catalogUrl)) yield return RefreshRoutine();
            else { LastError = "Не задан адрес сервера."; InitialCheckComplete = true; }
        }

        public void Refresh()
        {
            if (!IsUpdating && !_refreshRequested && !string.IsNullOrWhiteSpace(_catalogUrl))
            {
                _refreshRequested = true;
                StartCoroutine(RefreshRoutine());
            }
        }

        public bool TryGetData(string id, out RemoteElement data)
        {
            data = null;
            return !string.IsNullOrWhiteSpace(id) && (_entries.TryGetValue(id, out data) || _bundledEntries.TryGetValue(id, out data));
        }

        // JSON stores URLs only. Unity objects are created separately, on demand, from cached image files.
        public Sprite GetSprite(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || _activeDirectory == null) return null;
            string key = ImagePath(_activeDirectory, url);
            if (_sprites.TryGetValue(key, out var sprite)) return sprite;
            sprite = LoadSprite(url);
            if (sprite) _sprites.Add(key, sprite);
            return sprite;
        }

        private IEnumerator RefreshRoutine()
        {
            _refreshRequested = false;
            if (IsUpdating) yield break;
            IsUpdating = true;
            Progress = 0;
            Status = "Проверка данных на сервере…";
            LastError = null;
            var routine = DownloadAndActivate();
            while (true)
            {
                bool running;
                try { running = routine.MoveNext(); }
                catch (Exception e) { Report(e.Message); break; }
                if (!running) break;
                yield return routine.Current;
            }
            IsUpdating = false;
            InitialCheckComplete = true;
            Status = LastError == null ? "Данные готовы" : (HasContent ? "Сервер недоступен. Используем сохранённые данные." : "Не удалось загрузить данные.");
            if (LastError == null) Progress = 1;
        }

        private IEnumerator DownloadAndActivate()
        {
            if (!ValidUrl(_catalogUrl)) { Report("Адрес каталога должен начинаться с https://"); yield break; }
            string json;
            using (var request = UnityWebRequest.Get(_catalogUrl))
            {
                request.timeout = Mathf.Max(1, _timeoutSeconds);
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) { Report(request.error); yield break; }
                json = request.downloadHandler.text;
            }
            ElementCatalog next = null;
            string error = null;
            try { next = Parse(json); }
            catch (Exception e) { error = e.Message; }
            if (error != null) { Report(error); yield break; }
            string key = Hash(json);
            string directory = Path.Combine(_root, key);
            if (_activeDirectory == directory && CacheComplete(next, directory)) yield break;
            try { Directory.CreateDirectory(directory); }
            catch (Exception e) { error = e.Message; }
            if (error != null) { Report(error); yield break; }

            var imageUrls = ImageUrls(next);
            int completed = 0;
            foreach (string url in imageUrls)
            {
                Status = $"Загрузка изображений: {++completed}/{imageUrls.Count}";
                Progress = (float)(completed - 1) / Mathf.Max(1, imageUrls.Count);
                string path = ImagePath(directory, url);
                if (File.Exists(path) && ImageValid(File.ReadAllBytes(path))) continue;
                using (var request = UnityWebRequest.Get(url))
                {
                    request.timeout = Mathf.Max(1, _timeoutSeconds);
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success) { Report(url + ": " + request.error); yield break; }
                    byte[] bytes = request.downloadHandler.data;
                    if (!ImageValid(bytes)) { Report("Не удалось декодировать изображение: " + url); yield break; }
                    try { File.WriteAllBytes(path, bytes); }
                    catch (Exception e) { error = e.Message; }
                    if (error != null) { Report(error); yield break; }
                }
            }
            try
            {
                File.WriteAllText(Path.Combine(directory, "catalog.json"), json);
                string pointer = Path.Combine(_root, "active.txt");
                string temporary = pointer + ".tmp";
                File.WriteAllText(temporary, key);
                // Both files are on the same filesystem: replace the pointer only after the complete download.
                if (File.Exists(pointer)) File.Replace(temporary, pointer, null);
                else File.Move(temporary, pointer);
            }
            catch (Exception e) { error = e.Message; }
            if (error != null) { Report(error); yield break; }
            Apply(next, directory);
        }

        private void Apply(ElementCatalog catalog, string directory)
        {
            _catalog = catalog;
            _activeDirectory = directory;
            _entries.Clear();
            foreach (var entry in catalog.elements) _entries.Add(entry.id, entry);
            BindScene(SceneManager.GetActiveScene());
            ContentChanged?.Invoke();
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => BindScene(scene);
        private void BindScene(Scene scene)
        {
            if (_elementsRoot)
            {
                foreach (var element in _elementsRoot.GetComponentsInChildren<ElementDog>(true)) element.SetContent(this);
                return;
            }
            foreach (var root in scene.GetRootGameObjects())
                foreach (var element in root.GetComponentsInChildren<ElementDog>(true)) element.SetContent(this);
        }

        private Sprite LoadSprite(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || _activeDirectory == null) return null;
            try
            {
                var texture = new Texture2D(2, 2);
                if (!texture.LoadImage(File.ReadAllBytes(ImagePath(_activeDirectory, url))))
                { Destroy(texture); return null; }
                _owned.Add(texture);
                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
                _owned.Add(sprite);
                return sprite;
            }
            catch (Exception e) { Report(e.Message); return null; }
        }

        private static ElementCatalog Parse(string json)
        {
            var catalog = JsonUtility.FromJson<ElementCatalog>(json);
            if (catalog == null || catalog.elements == null || catalog.elements.Length == 0)
                throw new FormatException("JSON должен содержать непустой массив elements.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in catalog.elements)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id) || !ids.Add(entry.id))
                    throw new FormatException("Пустой или повторяющийся id элемента.");
            }
            foreach (string url in ImageUrls(catalog))
                if (!ValidUrl(url)) throw new FormatException("Картинки должны иметь абсолютные HTTPS-ссылки: " + url);
            return catalog;
        }

        private static HashSet<string> ImageUrls(ElementCatalog catalog)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in catalog.elements)
            {
                if (!string.IsNullOrWhiteSpace(entry.iconUrl)) result.Add(entry.iconUrl);
                foreach (var image in entry.images ?? Array.Empty<RemoteElementImage>())
                    if (image != null && !string.IsNullOrWhiteSpace(image.url)) result.Add(image.url);
                foreach (string url in entry.imageUrls ?? Array.Empty<string>())
                    if (!string.IsNullOrWhiteSpace(url)) result.Add(url);
            }
            return result;
        }

        private static bool CacheComplete(ElementCatalog catalog, string directory)
        {
            foreach (string url in ImageUrls(catalog))
            {
                string path = ImagePath(directory, url);
                if (!File.Exists(path) || new FileInfo(path).Length == 0) return false;
            }
            return true;
        }

        private static bool ImageValid(byte[] bytes)
        {
            var texture = new Texture2D(2, 2);
            bool valid = texture.LoadImage(bytes);
            Destroy(texture);
            return valid;
        }

        private static bool ValidUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https";
        private static string ImagePath(string directory, string url) => Path.Combine(directory, Hash(url) + ".image");
        private static bool IsHex(string key)
        {
            foreach (char c in key) if (!Uri.IsHexDigit(c)) return false;
            return true;
        }
        private static string Hash(string value)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }
        private void Report(string message) { LastError = message; Debug.LogWarning("[ElementContent] " + message, this); }
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (var obj in _owned) if (obj) Destroy(obj);
        }
    }
}
