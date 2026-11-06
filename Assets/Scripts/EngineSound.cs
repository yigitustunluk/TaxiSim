using UnityEngine;
using UnityEngine.InputSystem;

// Motor ve korna sesi. Hiç ses dosyası kullanmaz: sesi kodla üretir.
// Taksi objesine eklenir.
//
// Nasıl çalışır?
// - Gerçek bir motorda her silindir patlaması kısa bir "pat" sesi çıkarır.
//   Bu patlamalar hızlı tekrarlanınca birleşip motorun uğultusunu oluşturur.
// - Biz de devre (RPM) göre saniyede onlarca kez kısa bir gürültü darbesi üretip
//   bunu süzgeçten (low-pass filtre) geçiriyoruz. Sonuç, düz bir tondan çok daha "tok" bir ses.
// - Hızlandıkça devir yükselir, belli hızlarda vites değişir ve devir düşer.
// - H tuşu: korna.
[RequireComponent(typeof(AudioSource))]
public class EngineSound : MonoBehaviour
{
    public TaxiController taxi;

    [Header("Gerçek Kayıt (boş bırakılırsa ses kodla üretilir)")]
    public AudioClip engineClip;           // Rölanti (idle) motor kaydı
    public float minPitch = 0.8f;          // Rölantide çalma hızı
    public float maxPitch = 1.9f;          // En yüksek devirde çalma hızı

    [Header("Motor")]
    public float idleRpm = 850f;
    public float maxRpm = 3500f;   // Yüksek devir testere sesi gibi tizleşir
    public int gearCount = 4;
    [Range(0f, 1f)] public float volume = 0.5f;

    [Header("Korna")]
    [Range(0f, 1f)] public float hornVolume = 0.25f;

    // Ana iş parçacığında (Update) yazılır, ses iş parçacığında okunur
    float targetRpm, targetLoad;
    bool hornPressed;
    bool useClip;
    AudioSource src;
    float smoothRpm, smoothLoad;           // Kayıt modunda ana iş parçacığında yumuşatılan değerler

    // Ses iş parçacığının kendi durumu
    float rpm, load, firePhase, pulse, pulseTarget, lp1, lp2, lp3, bodyPhase, hornPhaseA, hornPhaseB, hornLevel;
    int sampleRate;
    System.Random noise = new System.Random();

    void Start()
    {
        if (taxi == null) taxi = GetComponent<TaxiController>();
        sampleRate = AudioSettings.outputSampleRate;
        rpm = idleRpm;

        src = GetComponent<AudioSource>();
        useClip = engineClip != null;
        smoothRpm = idleRpm;

        if (useClip)
        {
            // Gerçek kayıt: devire göre hızlandırıp yavaşlatacağız (pitch)
            src.clip = engineClip;
        }
        else
        {
            // Kodla üretim: Audio Source'un çalışması için içinde tek bir "1" olan minicik bir klip.
            // Asıl sesi OnAudioFilterRead'de bu değerin üstüne yazacağız.
            AudioClip dummy = AudioClip.Create("Bos", 1, 1, sampleRate, false);
            dummy.SetData(new float[] { 1f }, 0);
            src.clip = dummy;
        }
        src.loop = true;
        src.spatialBlend = 0f;   // 2D: kendi aracımızın sesi, yönü yok
        src.Play();
    }

    void Update()
    {
        if (taxi == null) return;

        // Hızı vitesler arasında paylaştır. Her vitesin içinde devir alttan üste çıkar.
        float speed01 = Mathf.Clamp01(taxi.SpeedKmh / taxi.MaxSpeedKmh);
        float gearPos = speed01 * gearCount;
        float inGear = gearPos >= gearCount ? 1f : gearPos - Mathf.Floor(gearPos);
        float rpmInGear = Mathf.Lerp(0.35f, 1f, inGear);   // Vites değişince devir %35'e düşer
        targetRpm = speed01 < 0.02f ? idleRpm : Mathf.Lerp(idleRpm, maxRpm, rpmInGear);

        targetLoad = Mathf.Abs(taxi.Throttle);              // Gaza basınca ses sertleşir
        hornPressed = Keyboard.current != null && Keyboard.current.hKey.isPressed;

        if (useClip)
        {
            // Kaydı devire göre hızlı/yavaş çal: hızlı çalınan kayıt daha tiz, yani yüksek devir gibi duyulur
            smoothRpm = Mathf.Lerp(smoothRpm, targetRpm, 5f * Time.deltaTime);
            smoothLoad = Mathf.Lerp(smoothLoad, targetLoad, 8f * Time.deltaTime);
            float rpm01 = Mathf.InverseLerp(idleRpm, maxRpm, smoothRpm);
            src.pitch = Mathf.Lerp(minPitch, maxPitch, rpm01);
            src.volume = volume * Mathf.Lerp(0.6f, 1f, smoothLoad);   // Gaza basınca biraz daha yüksek
        }
    }

    // SES İŞ PARÇACIĞI: Unity, hoparlöre gidecek her küçük ses parçasında (~20 ms) bunu çağırır.
    // Burada Unity'nin çoğu fonksiyonu kullanılamaz, sadece matematik.
    void OnAudioFilterRead(float[] data, int channels)
    {
        float dt = 1f / sampleRate;
        if (useClip)
        {
            // Kayıt zaten çalıyor, biz sadece üstüne kornayı ekliyoruz
            for (int i = 0; i < data.Length; i += channels)
            {
                float horn = HornSample(1f / sampleRate);
                for (int c = 0; c < channels; c++)
                    data[i + c] = Mathf.Clamp(data[i + c] + horn, -1f, 1f);
            }
            return;
        }

        for (int i = 0; i < data.Length; i += channels)
        {
            rpm += (targetRpm - rpm) * 5f * dt;
            load += (targetLoad - load) * 8f * dt;

            // 4 silindirli motor: her devirde 2 patlama
            float fireRate = rpm / 60f * 2f;
            firePhase += fireRate * dt;
            if (firePhase >= 1f)
            {
                firePhase -= 1f;
                pulseTarget = 1f;                  // Yeni patlama
            }
            // Patlama aniden değil, çok kısa bir sürede yükselir (ani sıçrama "cızırtı" yapar)
            pulse += (pulseTarget - pulse) * 0.5f;
            pulseTarget *= 1f - 180f * dt;         // Patlama söner

            // Patlama = zarf, içinde az miktarda gürültü (fazlası testere sesi yapar)
            float white = (float)noise.NextDouble() * 2f - 1f;
            float raw = pulse * (0.9f + 0.1f * white) * (0.6f + 0.4f * load);

            // Üç kademeli low-pass filtre: tizleri iyice keser, tok bir uğultu bırakır.
            float cutoff = 120f + 250f * load;
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff * dt);
            lp1 += (raw - lp1) * a;
            lp2 += (lp1 - lp2) * a;
            lp3 += (lp2 - lp3) * a;

            // Gövde: devrin yarı frekansında yumuşak bir bas ton. Motora "ağırlık" verir.
            bodyPhase += fireRate * 0.5f * dt;
            if (bodyPhase > 1f) bodyPhase -= 1f;
            float body = Mathf.Sin(bodyPhase * 2f * Mathf.PI) * 0.25f;

            float engine = (lp3 * 4f + body) * volume;

            float horn = HornSample(dt);

            float sample = Mathf.Clamp(engine + horn, -1f, 1f);
            for (int c = 0; c < channels; c++) data[i + c] = sample;   // Tüm kanallara (sol/sağ) aynı ses
        }
    }

    // Korna: iki notanın birlikte çaldığı klasik araba kornası
    float HornSample(float dt)
    {
        hornLevel += ((hornPressed ? 1f : 0f) - hornLevel) * 60f * dt;
        hornPhaseA += 420f * dt; if (hornPhaseA > 1f) hornPhaseA -= 1f;
        hornPhaseB += 528f * dt; if (hornPhaseB > 1f) hornPhaseB -= 1f;
        return (Square(hornPhaseA) + Square(hornPhaseB)) * 0.5f * hornVolume * hornLevel;
    }

    // Yumuşatılmış kare dalga: kornaya "bas" bir ton verir
    static float Square(float ph)
    {
        return Mathf.Clamp(Mathf.Sin(ph * 2f * Mathf.PI) * 3f, -1f, 1f);
    }
}
