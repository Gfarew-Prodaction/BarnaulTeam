using UnityEngine;
using DataScripts;

namespace InteracrSystem
{
    public class ElementDog : MonoBehaviour, IElementVEt
    {
        [SerializeField] private string _id;
        private RemoteElementContent _content;

        public string Id => _id;
        public RemoteElement Data => _content && _content.TryGetData(Id, out var data) ? data : null;
        public Sprite GetImage(string url) => _content ? _content.GetSprite(url) : null;

        public void SetContent(RemoteElementContent content) => _content = content;

        private void Start()
        {
            // Direct Play from the game scene needs the same JSON service as Startup.
            if (_content) return;
            var content = RemoteElementContent.Instance;
            if (!content)
            {
                var service = new GameObject("Persistent Element Content");
                content = service.AddComponent<RemoteElementContent>();
                DontDestroyOnLoad(service);
            }
            _content = content;
        }

        public void Interact()
        {
            throw new System.NotImplementedException();
        }

        public void Pointed()
        {
            throw new System.NotImplementedException();
        }
    }
}
