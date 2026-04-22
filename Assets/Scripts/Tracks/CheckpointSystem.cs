using UnityEngine;
using FormulaSim.Cars;
using FormulaSim.Audio;

namespace FormulaSim.Tracks
{
    public class CheckpointData
    {
        public int  sectorId;       // 0=S1 end, 1=S2 end, 2=S3 end (finish)
        public bool isSector3End;
        public bool isDrsZone;
        public bool isDrsZoneEnd;
        public bool isTunnel;
    }

    [RequireComponent(typeof(Collider2D))]
    public class CheckpointTrigger : MonoBehaviour
    {
        [SerializeField] int  sectorId     = 0;
        [SerializeField] bool isDrsZone    = false;
        [SerializeField] bool isDrsZoneEnd = false;
        [SerializeField] bool isTunnel     = false;

        AudioManager audio;

        void Awake()
        {
            audio = FindObjectOfType<AudioManager>();
            GetComponent<Collider2D>().isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            var car = other.GetComponent<F1CarController>();
            if (car == null) return;

            var data = new CheckpointData
            {
                sectorId    = sectorId,
                isSector3End= sectorId == 2,
                isDrsZone   = isDrsZone,
                isDrsZoneEnd= isDrsZoneEnd,
                isTunnel    = isTunnel,
            };
            car.OnCheckpointHit(data);

            if (isDrsZone)    car.SetDrsZone(true);
            if (isDrsZoneEnd) car.SetDrsZone(false);

            // Sector sound feedback for player car
            if (car.CompareTag("Player"))
            {
                switch (sectorId)
                {
                    case 0: audio?.PlayUI(UISound.SectorGreen);  break;
                    case 1: audio?.PlayUI(UISound.SectorGreen);  break;
                    case 2: audio?.PlayUI(UISound.NewBestLap);   break;
                }
            }
        }
    }
}
