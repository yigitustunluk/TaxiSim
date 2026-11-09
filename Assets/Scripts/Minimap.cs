using UnityEngine;
using System.Collections;

// Mini harita. GameManager'a eklenir.
//
// Nasıl çalışır?
// 1. Şehrin planından, SADECE "Minimap" katmanında görünen sade bir harita üretir:
//    bloklar, parklar, meydanlar, yollar, taksi simgesi ve hedef.
// 2. Ana kamera bu katmanı görmez (Culling Mask). Mini harita kamerası SADECE bu katmanı görür.
// 3. Mini harita kamerası taksinin üstünde durur, aşağı bakar ve gördüğünü bir
//    "Render Texture"a çizer. Arayüzdeki bir RawImage o dokuyu gösterir.
//
// Gerçek şehri yukarıdan çekmek yerine sade bir harita üretiyoruz: gece de okunur,
// binalar yolları kapatmaz.
public class Minimap : MonoBehaviour
{
    [Header("Bağlantılar (Inspector'dan sürükle)")]
    public CityLayout layout;
    public CityGenerator city;                 // Blok ve park bilgisi için
    public Transform taxi;
    public PassengerManager passengers;
    public RenderTexture minimapTexture;
    [Tooltip("Emission'ı açık şablon malzeme (SehirMalzemesi)")]
    public Material baseMaterial;

    [Header("Görünüm")]
    public float viewSize = 110f;              // Merkezden kenara gösterilen mesafe (m)
    public bool rotateWithTaxi = true;         // Harita taksiyle dönsün mü (taksi hep yukarı bakar)

    [Header("Renkler")]
    public Color backgroundColor = Hex("0B0D12");
    public Color blockColor = Hex("1A1F29");
    public Color parkColor = Hex("2E5A34");
    public Color plazaColor = Hex("2A303B");
    public Color sideRoadColor = Hex("4A5262");
    public Color mainRoadColor = Hex("6B7486");
    public Color taxiColor = Hex("F2C200");
    public Color pickupColor = new Color(0.3f, 1f, 0.3f);
    public Color dropoffColor = new Color(1f, 0.55f, 0f);

    [Header("GPS Rotası")]
    public bool showRoute = true;
    public float routeWidth = 5f;
    public float routeUpdateInterval = 0.25f;  // Rota kaç saniyede bir yeniden hesaplansın

    int layer;
    Camera cam;
    Transform taxiIcon, targetIcon;
    Material targetMat, routeMat;
    LineRenderer routeLine;
    float routeTimer;
    bool ready;

    // Şehir üreticisi de Start'ta çalışıyor. Hangisinin önce çalışacağı belli değil,
    // o yüzden bir kare bekleyip şehir kesin hazırken haritayı çiziyoruz.
    IEnumerator Start()
    {
        layer = LayerMask.NameToLayer("Minimap");
        if (layer < 0)
        {
            Debug.LogError("Minimap: 'Minimap' adında bir katman (Layer) yok. Önce onu oluştur.");
            enabled = false;
            yield break;
        }

        yield return null;

        CreateCamera();
        CreateMap();
        CreateIcons();
        CreateRouteLine();
        ready = true;
    }

    void LateUpdate()
    {
        if (!ready || taxi == null) return;

        float yaw = rotateWithTaxi ? taxi.eulerAngles.y : 0f;
        Vector3 taxiFlat = new Vector3(taxi.position.x, 0f, taxi.position.z);

        // Kamera taksinin 30 m üstünde. Yüksekte olursa sis haritayı soluklaştırır.
        cam.transform.SetPositionAndRotation(taxiFlat + Vector3.up * 30f, Quaternion.Euler(90f, yaw, 0f));
        cam.orthographicSize = viewSize;

        taxiIcon.SetPositionAndRotation(taxiFlat + Vector3.up * 1f, Quaternion.Euler(0f, taxi.eulerAngles.y, 0f));

        if (passengers != null)
        {
            // Hedef görüş alanının dışındaysa haritanın kenarına, hedefin yönüne koy
            Vector3 t = passengers.TargetPosition;
            Vector3 toTarget = new Vector3(t.x, 0f, t.z) - taxiFlat;
            float edge = viewSize * 0.85f;
            bool outside = toTarget.magnitude > edge;
            if (outside) toTarget = toTarget.normalized * edge;

            targetIcon.position = taxiFlat + toTarget + Vector3.up * 0.8f;
            targetIcon.localScale = outside ? new Vector3(14f, 0.1f, 14f) : new Vector3(20f, 0.1f, 20f);
            Color c = passengers.IsCarryingPassenger ? dropoffColor : pickupColor;
            SetEmission(targetMat, c);
            SetEmission(routeMat, c);

            routeTimer -= Time.deltaTime;
            if (showRoute && routeTimer <= 0f)
            {
                routeTimer = routeUpdateInterval;
                UpdateRoute();
            }
        }
    }

    // GPS: Taksinin bulunduğu yoldan hedefe, Dijkstra ile en kısa rota.
    // Taksi yanlış yola sapınca rota birkaç kare içinde kendiliğinden yeniden hesaplanır.
    void UpdateRoute()
    {
        RoadPoint from = layout.Network.NearestPoint(taxi.position);
        RoadPoint to = passengers.TargetRoadPoint;
        if (from.edge == null || to.edge == null) { routeLine.positionCount = 0; return; }

        // Taksinin baktığı yönü veriyoruz: rota yolun ortasında geri dönüş yaptırmasın
        Route route = layout.Network.FindRoute(from, to, taxi.forward);
        if (route == null) { routeLine.positionCount = 0; return; }

        // Çizginin noktaları: taksinin yolu üzerindeki nokta → rotadaki kavşaklar → hedef
        int count = route.nodes.Count + 2;
        routeLine.positionCount = count;
        float y = 0.5f;   // Yolların üstünde, simgelerin altında
        routeLine.SetPosition(0, Flat(from.Position, y));
        for (int i = 0; i < route.nodes.Count; i++)
            routeLine.SetPosition(i + 1, Flat(route.nodes[i].position, y));
        routeLine.SetPosition(count - 1, Flat(to.Position, y));
    }

    Vector3 Flat(Vector3 p, float y) => new Vector3(p.x, y, p.z);

    void CreateRouteLine()
    {
        routeMat = MakeMaterial(pickupColor);
        routeLine = new GameObject("HaritaRota").AddComponent<LineRenderer>();
        routeLine.gameObject.layer = layer;
        routeLine.sharedMaterial = routeMat;
        routeLine.widthMultiplier = routeWidth;
        routeLine.numCornerVertices = 4;            // Dönüşlerde köşeler yuvarlak olsun
        routeLine.numCapVertices = 4;
        routeLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        routeLine.positionCount = 0;
    }

    void CreateCamera()
    {
        cam = new GameObject("MinimapCamera").AddComponent<Camera>();
        cam.orthographic = true;                   // Perspektifsiz, düz harita görünümü
        cam.cullingMask = 1 << layer;              // Sadece Minimap katmanını görsün
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = backgroundColor;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 60f;
        cam.targetTexture = minimapTexture;        // Ekrana değil, dokuya çizsin
    }

    // Katmanlar alttan üste: bloklar → parklar/meydanlar → yollar. Üstteki öne çizilir.
    void CreateMap()
    {
        if (city != null)
        {
            Material block = MakeMaterial(blockColor);
            Material park = MakeMaterial(parkColor);
            Material plaza = MakeMaterial(plazaColor);

            foreach (Rect r in city.blockRects) MakeFlat(r, 0.05f, block);
            foreach (Rect r in city.parkRects) MakeFlat(r, 0.1f, park);
            foreach (Rect r in city.plazaRects) MakeFlat(r, 0.1f, plaza);
        }

        Material side = MakeMaterial(sideRoadColor);
        Material main = MakeMaterial(mainRoadColor);
        foreach (RoadEdge e in layout.Network.edges)
        {
            Vector3 mid = (e.a.position + e.b.position) / 2f;
            Vector3 dir = (e.b.position - e.a.position).normalized;
            // Kavşak köşeleri dolsun diye şeridi iki yanda yarım yol genişliği kadar uzat
            Vector3 size = new Vector3(e.width, 0.1f, e.Length + e.width);
            MakeShape(PrimitiveType.Cube, "HaritaYol", mid + Vector3.up * (e.isMain ? 0.3f : 0.2f),
                      Quaternion.LookRotation(dir), size, e.isMain ? main : side).transform.SetParent(transform, true);
        }
    }

    void MakeFlat(Rect r, float y, Material mat)
    {
        MakeShape(PrimitiveType.Cube, "HaritaAlan", new Vector3(r.center.x, y, r.center.y), Quaternion.identity,
                  new Vector3(r.width, 0.05f, r.height), mat).transform.SetParent(transform, true);
    }

    void CreateIcons()
    {
        // Hedef: daire (taksinin altında kalsın diye taksiden biraz aşağıda)
        targetMat = MakeMaterial(pickupColor);
        targetIcon = MakeShape(PrimitiveType.Cylinder, "HaritaHedef", Vector3.zero, Quaternion.identity,
                               new Vector3(14f, 0.1f, 14f), targetMat).transform;

        // Taksi: ok şeklinde, iki parçadan. Harita 220 m gösterdiği için 12 m boyunda.
        taxiIcon = new GameObject("HaritaTaksi").transform;
        taxiIcon.gameObject.layer = layer;
        Material taxiMat = MakeMaterial(taxiColor);
        MakeShape(PrimitiveType.Cube, "Govde", Vector3.zero, Quaternion.identity,
                  new Vector3(6f, 0.2f, 12f), taxiMat).transform.SetParent(taxiIcon, false);
        GameObject tip = MakeShape(PrimitiveType.Cube, "Uc", Vector3.zero, Quaternion.Euler(0f, 45f, 0f),
                                   new Vector3(10f, 0.2f, 10f), taxiMat);
        tip.transform.SetParent(taxiIcon, false);
        tip.transform.localPosition = new Vector3(0f, 0f, 6f);
    }

    GameObject MakeShape(PrimitiveType type, string objName, Vector3 pos, Quaternion rot, Vector3 size, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = objName;
        go.layer = layer;
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = size;
        Renderer rend = go.GetComponent<Renderer>();
        rend.sharedMaterial = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Destroy(go.GetComponent<Collider>());     // Haritadaki şekiller fiziğe karışmasın
        return go;
    }

    // Işıktan etkilenmeyen düz renk: temel rengi siyah yapıp rengi "parlama"dan veriyoruz.
    // Böylece harita gece de gündüz de aynı görünür.
    Material MakeMaterial(Color c)
    {
        Material m = baseMaterial != null
            ? new Material(baseMaterial)
            : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", Color.black);
        m.EnableKeyword("_EMISSION");
        SetEmission(m, c);
        return m;
    }

    void SetEmission(Material m, Color c)
    {
        m.SetColor("_EmissionColor", c);
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }
}
