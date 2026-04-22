using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace FormulaSim.Network
{
    public class ConnectivityManager : MonoBehaviour
    {
        const string PING_URL        = "https://api.formulasimpro.com/v1/ping";
        const float  PING_TIMEOUT    = 4f;
        const float  RECHECK_SECS    = 60f;

        public bool  IsOnline   { get; private set; }
        public string OfflineCircuit => "silverstone";

        float recheckTimer;

        public IEnumerator CheckAsync()
        {
            yield return _DoPing();
            recheckTimer = RECHECK_SECS;
        }

        void Update()
        {
            recheckTimer -= Time.deltaTime;
            if (recheckTimer <= 0)
            {
                recheckTimer = RECHECK_SECS;
                StartCoroutine(_DoPing());
            }
        }

        IEnumerator _DoPing()
        {
            using var req = UnityWebRequest.Head(PING_URL);
            req.timeout   = (int)PING_TIMEOUT;
            yield return req.SendWebRequest();
            bool prev = IsOnline;
            IsOnline = req.result == UnityWebRequest.Result.Success;
            if (prev && !IsOnline)
                Core.GameManager.Instance?.TransitionTo(Core.GameState.MainMenu);
        }
    }
}
