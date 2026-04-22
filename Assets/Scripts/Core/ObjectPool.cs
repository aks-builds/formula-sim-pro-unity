using System.Collections.Generic;
using UnityEngine;

namespace FormulaSim.Core
{
    /// <summary>
    /// Generic object pool — eliminates runtime instantiation/destruction costs.
    /// Used for particles, audio sources, skid mark segments, UI toasts.
    /// </summary>
    public class ObjectPool : MonoBehaviour
    {
        public static ObjectPool Instance { get; private set; }

        [System.Serializable]
        public class PoolEntry
        {
            public string     key;
            public GameObject prefab;
            public int        initialSize = 10;
            public bool       expandable  = true;
        }

        [SerializeField] List<PoolEntry> pools;

        readonly Dictionary<string, Queue<GameObject>>  available = new();
        readonly Dictionary<string, GameObject>         prefabMap = new();
        readonly Dictionary<string, Transform>          containers = new();

        void Awake()
        {
            if (Instance) { Destroy(gameObject); return; }
            Instance = this;

            foreach (var entry in pools)
            {
                var container = new GameObject($"Pool_{entry.key}").transform;
                container.SetParent(transform);
                containers[entry.key] = container;
                prefabMap[entry.key]  = entry.prefab;
                available[entry.key]  = new Queue<GameObject>();

                for (int i = 0; i < entry.initialSize; i++)
                    _Create(entry.key, entry.prefab, container);
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        public GameObject Get(string key, Vector3 position, Quaternion rotation)
        {
            if (!available.TryGetValue(key, out var q)) return null;

            GameObject obj;
            if (q.Count > 0)
            {
                obj = q.Dequeue();
            }
            else if (prefabMap.TryGetValue(key, out var prefab))
            {
                obj = _Create(key, prefab, containers[key]);
            }
            else return null;

            obj.transform.SetPositionAndRotation(position, rotation);
            obj.SetActive(true);
            return obj;
        }

        public void Return(string key, GameObject obj)
        {
            obj.SetActive(false);
            if (containers.TryGetValue(key, out var c))
                obj.transform.SetParent(c);
            if (available.TryGetValue(key, out var q))
                q.Enqueue(obj);
            else
                Destroy(obj);
        }

        public void ReturnAfter(string key, GameObject obj, float delay)
            => StartCoroutine(_ReturnAfterDelay(key, obj, delay));

        System.Collections.IEnumerator _ReturnAfterDelay(string key, GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            Return(key, obj);
        }

        GameObject _Create(string key, GameObject prefab, Transform parent)
        {
            var obj = Instantiate(prefab, parent);
            obj.SetActive(false);
            available[key].Enqueue(obj);
            return obj;
        }
    }

    // ── Pooled Audio Source ────────────────────────────────────────────────────

    /// <summary>
    /// Pooled audio source: plays and auto-returns itself to the pool.
    /// Attach this to audio source prefabs used in the pool.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PooledAudioSource : MonoBehaviour
    {
        AudioSource src;
        string      poolKey;

        void Awake() => src = GetComponent<AudioSource>();

        public void Play(string key, AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            poolKey     = key;
            src.clip    = clip;
            src.volume  = volume;
            src.pitch   = pitch;
            src.Play();
            ObjectPool.Instance?.ReturnAfter(key, gameObject, clip.length + 0.1f);
        }
    }
}
