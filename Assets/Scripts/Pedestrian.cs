using UnityEngine;

// Bir yaya: bir bloğun kaldırımında, köşeden köşeye döngü hâlinde yürür.
// Köşelerde ara sıra durup bekler. Kaldırıma çıkan taksiden kaçar, çarpılırsa düşer.
// PedestrianSpawner tarafından oluşturulur.
public class Pedestrian : MonoBehaviour
{
    // STATİK OLAY: Herhangi bir yaya taksi tarafından çarpıldığında tetiklenir.
    // "static" olduğu için bütün yayalar için TEK bir olay var; 60 yayaya ayrı ayrı
    // abone olmak gerekmez. Abone olan, sahne kapanırken aboneliği bırakmak ZORUNDA.
    public static System.Action OnHitByTaxi;

    static TaxiController taxi;             // Bütün yayalar aynı taksiyi izler, bir kez bulmak yeterli

    [Header("Taksiden Kaçma")]
    public float dangerDistance = 8f;       // Taksi bu kadar yakındaysa tehlike
    public float dangerSpeedKmh = 10f;      // ve bu hızdan hızlıysa
    public float fleeSpeed = 4f;            // Kaçarken koşma hızı
    public float fleeTime = 1.2f;
    public float knockdownTime = 3f;        // Çarpılınca yerde kalma süresi

    public float walkSpeed = 2f;            // m/s (karakterler 2 kat büyütüldüğü için adımları da büyük)
    public float animationBaseSpeed = 1.3f; // Yürüme animasyonunun "doğal" hızı: ayaklar kaymasın diye
    public float pauseChance = 0.2f;        // Köşeye gelince durma ihtimali
    public Vector2 pauseRange = new Vector2(1.5f, 4f);

    Animator anim;
    Vector3[] corners;                      // Bloğun etrafındaki yürüme yolunun dört köşesi
    int target;                             // Gidilen köşe
    int step;                               // +1 saat yönü, -1 ters yön
    float pauseTimer;
    float fleeTimer, knockdownTimer;
    Vector3 fleeDir;
    Rect walkRect;                          // Kaçarken bu alanın dışına çıkma (yola atlamasın)
    bool returningToPath;                   // Kaçtıktan sonra yürüme çizgisine geri dönüyor mu
    string currentState;
    System.Random rng;

    // Spawner bunu çağırır: hangi bloğun etrafında, kenardan ne kadar içeride yürüyeceği
    public void Init(Rect block, float inset, float groundY, System.Random random)
    {
        rng = random;
        anim = GetComponentInChildren<Animator>();

        Rect r = new Rect(block.xMin + inset, block.yMin + inset, block.width - 2f * inset, block.height - 2f * inset);
        walkRect = new Rect(block.xMin + 0.6f, block.yMin + 0.6f, block.width - 1.2f, block.height - 1.2f);
        if (taxi == null) taxi = FindFirstObjectByType<TaxiController>();

        // Çarpışmayı yakalamak için: tetikleyici (trigger) kapsül. Taksiyi durdurmaz, sadece haber verir.
        CapsuleCollider col = gameObject.AddComponent<CapsuleCollider>();
        col.isTrigger = true;
        col.radius = 0.4f;
        col.height = 1.8f;
        col.center = new Vector3(0f, 0.9f, 0f);
        corners = new Vector3[]
        {
            new Vector3(r.xMin, groundY, r.yMin),
            new Vector3(r.xMax, groundY, r.yMin),
            new Vector3(r.xMax, groundY, r.yMax),
            new Vector3(r.xMin, groundY, r.yMax),
        };

        // Rastgele bir kenarın rastgele bir yerinden başla, rastgele yöne yürü
        int from = rng.Next(4);
        step = rng.NextDouble() < 0.5 ? 1 : -1;
        target = (from + step + 4) % 4;
        transform.position = Vector3.Lerp(corners[from], corners[target], (float)rng.NextDouble());
        transform.rotation = Quaternion.LookRotation(corners[target] - corners[from]);

        walkSpeed *= 0.85f + 0.3f * (float)rng.NextDouble();   // Herkes aynı hızda yürümesin
        Play("walk");
    }

    void Update()
    {
        if (corners == null) return;

        // Yerdeyse kalkmayı bekle
        if (knockdownTimer > 0f)
        {
            knockdownTimer -= Time.deltaTime;
            if (knockdownTimer <= 0f) { Play("idle"); returningToPath = true; }
            return;
        }

        // Kaçıyorsa koş
        if (fleeTimer > 0f)
        {
            fleeTimer -= Time.deltaTime;
            if (fleeTimer <= 0f) returningToPath = true;   // Kaçış bitti: önce çizgiye dön
            Play("sprint");
            Vector3 next = transform.position + fleeDir * fleeSpeed * Time.deltaTime;
            next.x = Mathf.Clamp(next.x, walkRect.xMin, walkRect.xMax);
            next.z = Mathf.Clamp(next.z, walkRect.yMin, walkRect.yMax);
            transform.position = next;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(fleeDir), 12f * Time.deltaTime);
            return;
        }

        CheckDanger();

        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.deltaTime;
            Play("idle");
            return;
        }

        // Kaçtıktan/düştükten sonra: önce yürüme çizgisindeki en yakın noktaya dön
        Vector3 goal = corners[target];
        if (returningToPath)
        {
            int prev = (target - step + 4) % 4;
            goal = ClosestPointOnSegment(corners[prev], corners[target], transform.position);
            if ((goal - transform.position).magnitude < 0.1f) { returningToPath = false; goal = corners[target]; }
        }

        Vector3 toTarget = goal - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        if (distance < 0.05f && !returningToPath)
        {
            // Köşeye vardık: sonraki köşeye dön, belki biraz dur
            target = (target + step + 4) % 4;
            if (rng.NextDouble() < pauseChance)
                pauseTimer = Mathf.Lerp(pauseRange.x, pauseRange.y, (float)rng.NextDouble());
            return;
        }

        Play("walk");
        Vector3 dir = toTarget / distance;
        transform.position += dir * Mathf.Min(walkSpeed * Time.deltaTime, distance);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 8f * Time.deltaTime);
    }

    // Taksi kaldırımda, yakın, hızlı ve bize doğru geliyorsa kaç
    void CheckDanger()
    {
        if (taxi == null || !taxi.IsOnSidewalk || taxi.SpeedKmh < dangerSpeedKmh) return;

        Vector3 toMe = transform.position - taxi.transform.position;
        toMe.y = 0f;
        if (toMe.magnitude > dangerDistance) return;
        if (Vector3.Dot(taxi.transform.forward, toMe.normalized) < 0.5f) return;   // Bize doğru gelmiyor

        // Taksinin gidiş çizgisinden yana kaç: taksinin sağına mı soluna mı daha yakınsak o tarafa
        Vector3 side = Vector3.Cross(Vector3.up, taxi.transform.forward);
        fleeDir = Vector3.Dot(toMe, side) >= 0f ? side : -side;
        fleeTimer = fleeTime;
        pauseTimer = 0f;
    }

    // Tetikleyiciye bir şey girdi: taksiyse ve hızlıysa yere düş
    void OnTriggerEnter(Collider other)
    {
        if (knockdownTimer > 0f || taxi == null) return;
        if (other.attachedRigidbody == null || other.attachedRigidbody.gameObject != taxi.gameObject) return;
        if (taxi.SpeedKmh < 5f) return;   // Çok yavaş temas sayılmaz

        knockdownTimer = knockdownTime;
        fleeTimer = 0f;
        Play("die");          // Yere düşme animasyonu; knockdownTime sonra kalkar
        OnHitByTaxi?.Invoke();
    }

    static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
        return a + ab * t;
    }

    // Animator'da o isimdeki duruma yumuşak geçiş (aynı duruma tekrar tekrar geçmez)
    void Play(string state)
    {
        if (anim == null || currentState == state) return;
        // Yürürken animasyonu hızla orantılı oynat (hızlı yürüyen adımlarını da hızlı atar,
        // ayaklar kaymaz). Diğer animasyonlar normal hızda.
        anim.speed = state == "walk" ? walkSpeed / animationBaseSpeed : 1f;
        anim.CrossFade(state, 0.2f);
        currentState = state;
    }
}
