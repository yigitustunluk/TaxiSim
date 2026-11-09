using UnityEngine;
using System.Collections.Generic;

// Şehrin yollarında kendi başına dolaşan bir trafik aracı.
//
// Nasıl çalışır?
// - Şehir bir yol ağı: kavşaklar (düğüm) ve aralarındaki yollar (kenar).
// - Araç bir kavşaktan, bir yol boyunca komşu kavşağa gider. Her kavşakta o kavşaktan
//   çıkan yollardan birini rastgele seçer, ama geldiği yola geri dönmez (U dönüşü yapmaz).
// - Sağ şeritten gider (Türkiye'deki gibi). Şeridin yeri yolun genişliğine göre hesaplanır.
// - Önünde başka bir araç (taksi dahil) varsa durur. Kırmızı ışıkta durur.
[RequireComponent(typeof(Rigidbody))]
public class TrafficCar : MonoBehaviour
{
    [Header("Bağlantılar (Inspector'dan sürükle)")]
    public CityLayout layout;            // Yol ağı
    public TrafficLightSystem lights;    // Boşsa ya da kapalıysa ışıklara uymaz

    [Header("Sürüş")]
    public float speed = 10f;            // m/s (≈ 36 km/s)
    public float acceleration = 8f;      // Kalkış ve duruş yumuşaklığı
    public float turnSpeed = 180f;       // Saniyede kaç derece döner
    public float brakeDistance = 8f;     // Önündeki araca bu mesafede durur
    // Kırmızıda aracın MERKEZİ kavşak kenarından ne kadar önce durur.
    // Araç 4 m uzun, merkezi ortasında: ön tamponu merkezden 2 m önde.
    // Işık direği kavşaktan 2.5 m önde. 2.5 + 2 + 0.5 boşluk = 5 m: tampon direğin hemen gerisinde durur.
    public float stopMargin = 5f;

    Rigidbody rb;

    RoadNode currentNode;                // Çıktığımız kavşak
    RoadNode nextNode;                   // Gittiğimiz kavşak
    RoadEdge currentEdge;                // Üzerinde gittiğimiz yol
    Vector3 direction;                   // Yolun yönü (birim vektör)

    // Her yol parçasında iki hedef nokta var: parçanın başı ve sonu (sağ şeritte).
    // Kavşağın içindeki dönüş, bir parçanın sonundan diğerinin başına giderken olur.
    Vector3[] waypoints = new Vector3[2];
    int waypointIndex;

    float currentSpeed;
    // Bu kavşağı geçmeye karar verildi mi? Çizgiye yakınken yeşil/sarı görüp "geçiyorum"
    // dediysek, ışık sonradan kırmızıya dönse de durmayız (çizginin ortasında kalmamak için).
    bool committedToCross;
    System.Random rng = new System.Random();

    // KAVŞAK REZERVASYONU (kilit / mutex): Bir kavşağın içinde aynı anda tek araç olabilir.
    // Kavşak doluysa gelen araç dur çizgisinde bekler. Bu, araçların kavşağın ortasında
    // birbirini bekleyip sonsuza kadar kilitlenmesini (deadlock) önler.
    // "static": bütün araçlar aynı tabloyu paylaşır.
    static Dictionary<RoadNode, TrafficCar> intersectionOwner = new Dictionary<RoadNode, TrafficCar>();
    RoadNode reservedNode;

    // YEDEK EMNİYET: Başka bir araç yüzünden uzun süre takılırsak kısa süre diğer araçları yok say
    float stuckTimer, ghostTimer;

    // KAVŞAKTA DÖNÜŞ EĞRİSİ (Bézier eğrisi)
    // Üç noktayla tanımlanan yumuşak bir eğri: başlangıç (p0), bitiş (p2) ve eğriyi kendine
    // doğru "çeken" bir kontrol noktası (p1). p1'i iki şeridin kesiştiği köşeye koyunca
    // araç köşeyi keskin dönmek yerine yay çizerek döner.
    bool onCurve;
    Vector3 curveP0, curveP1, curveP2;
    float curveT;          // Eğrinin neresindeyiz (0 = başı, 1 = sonu)
    float curveLength;
    bool curveIsTurn;      // Dönüş mü, yoksa kavşaktan düz geçiş mi

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;   // Fizik motoru itmez, onu biz hareket ettiririz. Taksi yine de ona çarpabilir.
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Başlangıç konumuna en yakın kavşaktan başla
        currentNode = layout.Network.NearestNode(transform.position);
        currentEdge = null;              // Henüz bir yoldan gelmedik, ilk seçimde her yol serbest
        ChooseNextEdge();
        PlanSegment();

        // Aracı ilk yol parçasının başına koy ve doğru yöne çevir
        transform.position = waypoints[0];
        transform.rotation = Quaternion.LookRotation(direction);
        waypointIndex = 1;
    }

    void FixedUpdate()
    {
        if (onCurve) { FollowCurve(); return; }

        Vector3 target = waypoints[waypointIndex];
        Vector3 toTarget = target - rb.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        // Gideceğimiz yönde başka bir araç var mı?
        // İnce bir ışın yerine araç genişliğinde bir "kutu" fırlatıyoruz (BoxCast) ve bunu
        // aracın burnunun baktığı yöne değil, GİDECEĞİ yöne atıyoruz. Böylece dönüş yaparken
        // yolunun üstündeki araçları da görür.
        Vector3 moveDirection = distance > 0.01f ? toTarget.normalized : transform.forward;
        bool blocked = IsPathBlocked(moveDirection);

        // Biri bize çarptıysa bir süre bekle (taksiyi sürüklemesin diye)
        if (waitTimer > 0f)
        {
            waitTimer -= Time.fixedDeltaTime;
            blocked = true;
            currentSpeed = 0f;
        }

        // Hızı yumuşakça hedef hıza yaklaştır (engel varsa 0'a)
        float desiredSpeed = blocked ? 0f : speed;

        // KAVŞAK DOLU MU? Kavşağa yaklaşırken içinde başka araç varsa çizgide bekle
        bool mustYield = waypointIndex == 1 && distance < stopMargin + 6f && IsIntersectionBusy(nextNode);

        // TRAFİK IŞIĞI: Kavşağa yaklaşıyorsak (yol parçasının sonuna gidiyorsak) ışığa bak.
        // waypoints[1], kavşağın tam kenarında, yani "dur çizgisinde".
        bool mustStop = false;
        if (lights != null && lights.isActiveAndEnabled && waypointIndex == 1 && !committedToCross)
        {
            SignalColor signal = lights.GetSignal(nextNode, direction);
            // Sarıda: çizgiye çok yakınsak geç (aniden duramayız), uzaksak dur
            mustStop = signal == SignalColor.Red || (signal == SignalColor.Yellow && distance > stopMargin + 4f);

            // Durmamız gerekmiyor ve çizgiye yakınız: geçme kararı verildi, artık geri dönüş yok
            if (!mustStop && distance <= stopMargin + 4f) committedToCross = true;

            if (mustStop)
            {
                // Çizgiye yaklaştıkça hedef hızı düşür: 13 metreden itibaren yavaşla, çizgide dur
                float slowDown = Mathf.Clamp01((distance - stopMargin) / 12f);
                desiredSpeed = Mathf.Min(desiredSpeed, speed * slowDown);
            }
        }

        if (mustYield)
        {
            // Kavşağın kenarında, kısa bir mesafede dur (ışıktaki kadar geride değil)
            float slowDown = Mathf.Clamp01((distance - 1.5f) / 6f);
            desiredSpeed = Mathf.Min(desiredSpeed, speed * slowDown);
        }

        currentSpeed = Mathf.MoveTowards(currentSpeed, desiredSpeed, acceleration * Time.fixedDeltaTime);

        if (distance > 0.01f)
        {
            // Hedefe doğru dön
            Quaternion look = Quaternion.LookRotation(toTarget);
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, look, turnSpeed * Time.fixedDeltaTime));

            // Hedefe doğru ilerle (hedefi geçmeyecek kadar).
            // Kırmızıda dur çizgisini asla geçme.
            float maxStep = mustStop ? Mathf.Max(0f, distance - stopMargin)
                          : mustYield ? Mathf.Max(0f, distance - 1.5f)
                          : distance;
            float step = Mathf.Min(currentSpeed * Time.fixedDeltaTime, maxStep);
            rb.MovePosition(rb.position + toTarget.normalized * step);
        }

        if (distance < 0.5f && !mustStop && !mustYield) AdvanceWaypoint();
    }

    // Önümüzdeki yolu araç genişliğinde bir kutuyla tarar.
    // Kendi çarpışma kutumuzu ve Rigidbody'si olmayan şeyleri (bina, kaldırım) yok sayar.
    bool IsPathBlocked(Vector3 moveDirection)
    {
        Vector3 origin = rb.position + Vector3.up * 0.75f;
        Vector3 halfSize = new Vector3(1.1f, 0.5f, 0.5f);   // Aracın yarı genişliği, yarı yüksekliği
        Quaternion orientation = Quaternion.LookRotation(moveDirection);

        if (ghostTimer > 0f) ghostTimer -= Time.fixedDeltaTime;

        RaycastHit[] hits = Physics.BoxCastAll(origin, halfSize, moveDirection, orientation,
                                               brakeDistance + 2f, ~0, QueryTriggerInteraction.Ignore);
        bool blockedByTraffic = false;
        foreach (RaycastHit h in hits)
        {
            if (h.rigidbody == null) continue;   // Araç değil
            if (h.rigidbody == rb) continue;     // Kendimiz

            bool isTraffic = h.rigidbody.GetComponent<TrafficCar>() != null;
            if (!isTraffic) return true;          // Taksi: her zaman dur, asla yok sayma
            if (ghostTimer > 0f) continue;        // Kurtulma modu: diğer trafik araçlarını yok say
            blockedByTraffic = true;
        }

        // Trafik aracı yüzünden 8 saniyeden uzun takıldıysak 2 saniye kurtulma moduna geç
        stuckTimer = blockedByTraffic ? stuckTimer + Time.fixedDeltaTime : 0f;
        if (stuckTimer > 8f) { ghostTimer = 2f; stuckTimer = 0f; }
        return blockedByTraffic;
    }

    // Bir araçla temas ettiğimiz sürece dur. Kinematik araç kimseden etkilenmez,
    // bu yüzden durmazsa taksiyi buldozer gibi iter.
    float waitTimer;
    void OnCollisionStay(Collision collision)
    {
        if (collision.rigidbody != null) waitTimer = 1.5f;
    }

    void AdvanceWaypoint()
    {
        if (waypointIndex == 0)
        {
            waypointIndex = 1;          // Parçanın başına geldik, şimdi sonuna git
        }
        else
        {
            Vector3 oldDirection = direction;
            Vector3 start = rb.position;

            currentNode = nextNode;     // Parçanın sonuna, yani kavşağa geldik
            ChooseNextEdge();           // Yeni yol seç
            PlanSegment();

            // Kavşağı rezerve et, içini bir eğriyle geç, sonra yeni yolun sonuna doğru devam et
            Reserve(currentNode);
            StartCurve(start, oldDirection, waypoints[0], direction);
            waypointIndex = 1;
        }
    }

    void StartCurve(Vector3 start, Vector3 oldDir, Vector3 end, Vector3 newDir)
    {
        curveP0 = new Vector3(start.x, 0f, start.z);
        curveP2 = new Vector3(end.x, 0f, end.z);

        if (Vector3.Dot(oldDir, newDir) < -0.5f)
        {
            // Geri dönüş (çıkmaz sokak): ileri doğru bir yay çiz
            curveP1 = (curveP0 + curveP2) / 2f + oldDir * 6f;
        }
        else if (Vector3.Dot(oldDir, newDir) > 0.9f)
        {
            // Düz geçiş: kontrol noktası tam ortada, eğri düz bir çizgi olur
            curveP1 = (curveP0 + curveP2) / 2f;
        }
        else
        {
            // Sağa/sola dönüş: eski şerit çizgisi ile yeni şerit çizgisinin kesiştiği köşe
            curveP1 = curveP0 + oldDir * Vector3.Dot(curveP2 - curveP0, oldDir);
        }

        // Eğrinin uzunluğu için yaklaşık hesap: doğrudan mesafe ile köşeden geçen yolun ortalaması
        curveLength = (Vector3.Distance(curveP0, curveP2) +
                       Vector3.Distance(curveP0, curveP1) + Vector3.Distance(curveP1, curveP2)) / 2f;
        curveLength = Mathf.Max(curveLength, 0.1f);
        curveT = 0f;
        onCurve = true;
        curveIsTurn = Vector3.Dot(oldDir, newDir) < 0.9f;
    }

    void FollowCurve()
    {
        Vector3 tangent = BezierTangent(curveT);
        if (tangent.sqrMagnitude < 0.0001f) tangent = transform.forward;
        tangent.Normalize();

        // Eğrideyken de önümüzdeki araçlara dikkat et
        bool blocked = IsPathBlocked(tangent);
        if (waitTimer > 0f)
        {
            waitTimer -= Time.fixedDeltaTime;
            blocked = true;
            currentSpeed = 0f;
        }
        // Dönüşte biraz yavaşla (gerçek sürücüler de öyle yapar), düz geçişte yavaşlama
        float desiredSpeed = blocked ? 0f : speed * (curveIsTurn ? 0.7f : 1f);
        currentSpeed = Mathf.MoveTowards(currentSpeed, desiredSpeed, acceleration * Time.fixedDeltaTime);

        // t'yi eşit adımlarla artırmak eşit mesafe demek DEĞİL: Bézier eğrisinde bazı yerlerde
        // t yavaş, bazı yerlerde hızlı ilerler. Eğrinin o noktadaki "hızına" (türevin uzunluğu)
        // bölerek adım atıyoruz, böylece araç eğri boyunca sabit hızla gider.
        float curveSpeed = Mathf.Max(BezierTangent(curveT).magnitude, 0.5f);
        curveT = Mathf.Min(1f, curveT + currentSpeed * Time.fixedDeltaTime / curveSpeed);
        rb.MovePosition(Bezier(curveT));
        rb.MoveRotation(Quaternion.LookRotation(BezierTangent(curveT).sqrMagnitude > 0.0001f ? BezierTangent(curveT) : tangent));

        if (curveT >= 1f)
        {
            onCurve = false;   // Eğri bitti, yeni yolda düz devam
            Release();         // Kavşaktan çıktık, başkası girebilir
        }
    }

    bool IsIntersectionBusy(RoadNode node)
    {
        return intersectionOwner.TryGetValue(node, out TrafficCar owner)
               && owner != null && owner != this;
    }

    void Reserve(RoadNode node)
    {
        intersectionOwner[node] = this;
        reservedNode = node;
    }

    void Release()
    {
        if (reservedNode != null && intersectionOwner.TryGetValue(reservedNode, out TrafficCar owner) && owner == this)
            intersectionOwner.Remove(reservedNode);
        reservedNode = null;
    }

    // Araç yok olursa (ör. sahne kapanınca) rezervasyonu bırak, yoksa kavşak sonsuza kadar dolu kalır
    void OnDestroy()
    {
        Release();
    }

    // İkinci derece Bézier eğrisi: t, 0'dan 1'e giderken p0'dan p2'ye, p1'e doğru bükülerek gider
    Vector3 Bezier(float t)
    {
        float u = 1f - t;
        return u * u * curveP0 + 2f * u * t * curveP1 + t * t * curveP2;
    }

    // Eğrinin o noktadaki yönü (türevi): aracın burnu bu yöne bakar
    Vector3 BezierTangent(float t)
    {
        return 2f * (1f - t) * (curveP1 - curveP0) + 2f * t * (curveP2 - curveP1);
    }

    // Bulunduğumuz kavşaktan çıkan yollardan birini rastgele seçer
    void ChooseNextEdge()
    {
        List<RoadEdge> options = new List<RoadEdge>();
        foreach (RoadEdge e in currentNode.edges)
            if (e != currentEdge) options.Add(e);        // Geldiğimiz yola geri dönme

        if (options.Count == 0) options.Add(currentEdge); // Çıkmaz: mecburen geri dön

        currentEdge = options[rng.Next(options.Count)];
        nextNode = currentEdge.Other(currentNode);
        direction = currentEdge.DirectionFrom(currentNode);
    }

    // Seçilen yol parçasının başlangıç ve bitiş noktalarını hesaplar (sağ şeritte).
    // Noktalar kavşağın dışında: kavşağın bu yöndeki yarı genişliği kadar içeride.
    void PlanSegment()
    {
        committedToCross = false;   // Yeni yol, yeni kavşak: karar sıfırlanır
        Vector3 right = Vector3.Cross(Vector3.up, direction);       // Gidiş yönüne göre sağ
        Vector3 laneOffset = right * (currentEdge.width / 4f);      // Sağ yarının ortası

        waypoints[0] = currentNode.position + direction * IntersectionHalfSize(currentNode) + laneOffset;
        waypoints[1] = nextNode.position - direction * IntersectionHalfSize(nextNode) + laneOffset;
    }

    // Kavşağın, gidiş yönümüz boyunca yarı genişliği = kesişen yolun yarı genişliği
    float IntersectionHalfSize(RoadNode node)
    {
        bool alongX = Mathf.Abs(direction.x) > 0.5f;
        return (alongX ? layout.xWidths[node.gridIndex.x] : layout.zWidths[node.gridIndex.y]) / 2f;
    }
}
