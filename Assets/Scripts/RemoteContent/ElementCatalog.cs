using System;

namespace DataScripts
{
    [Serializable]
    public class ElementCatalog
    {
        public int version;
        public RemoteElement[] elements;
    }

    [Serializable]
    public class RemoteElement
    {
        public string id;
        public string name;
        public string nameLatin;
        public string shortDescription;
        public string fullDescription;
        public string iconUrl;
        public string[] imageUrls; // Legacy catalogs remain readable.
        public RemoteElementImage[] images;
    }
    [Serializable]
    public class RemoteElementImage
    {
        public string title;
        public string caption;
        public string url;
    }
}
