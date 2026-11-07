using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace DTK.Game
{
    /// <summary>Downloads TikTok profile photos once, keeps them in memory, at most a few downloads at a time.</summary>
    public class AvatarCache : MonoBehaviour
    {
        public static AvatarCache I { get; private set; }
        readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        readonly Dictionary<string, List<Action<Texture2D>>> waiting = new Dictionary<string, List<Action<Texture2D>>>();
        readonly Queue<string> queue = new Queue<string>();
        int running;
        const int MaxParallel = 3, MaxCached = 600;

        void Awake() { I = this; }

        public void Get(string url, Action<Texture2D> done)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith("http")) { done(null); return; }
            if (cache.TryGetValue(url, out var t)) { done(t); return; }
            if (waiting.TryGetValue(url, out var list)) { list.Add(done); return; }
            waiting[url] = new List<Action<Texture2D>> { done };
            queue.Enqueue(url);
        }

        /// <summary>Fills an avatar widget made by Ui.Avatar.</summary>
        public static void Apply(RawImage photo, Text initials, string url, string nickname)
        {
            initials.text = Ui.Initials(nickname);
            photo.enabled = false;
            photo.texture = null;
            if (I == null || string.IsNullOrEmpty(url)) return;
            string expected = url;
            photo.name = expected;
            I.Get(url, tex =>
            {
                if (photo == null || photo.name != expected || tex == null) return;
                photo.texture = tex;
                photo.enabled = true;
            });
        }

        void Update()
        {
            while (running < MaxParallel && queue.Count > 0) StartCoroutine(Download(queue.Dequeue()));
        }

        IEnumerator Download(string url)
        {
            running++;
            Texture2D tex = null;
            using (var req = UnityWebRequestTexture.GetTexture(url, true))
            {
                req.timeout = 8;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success) tex = DownloadHandlerTexture.GetContent(req);
            }
            running--;
            if (cache.Count > MaxCached) cache.Clear();
            if (tex != null) cache[url] = tex;
            if (waiting.TryGetValue(url, out var list))
            {
                waiting.Remove(url);
                foreach (var cb in list) { try { cb(tex); } catch { } }
            }
        }
    }
}
