using UnityEngine;
using System.Collections.Generic;

public enum SignalColor { Red, Yellow, Green }

// Şehrin bütün kavşaklarındaki trafik ışıkları. City objesine eklenir.
//
// Nasıl çalışır?
// - Her kavşağın iki "yönü" var: Kuzey-Güney ve Doğu-Batı. Aynı anda sadece biri yeşil yanar.
// - Döngü: K-G yeşil → K-G sarı → hepsi kırmızı → D-B yeşil → D-B sarı → hepsi kırmızı → başa dön.
// - Her kavşağın döngüsü farklı bir anda başlar, yoksa bütün şehir aynı anda yeşil olurdu.
// - Işığın rengi zamandan HESAPLANIR, bir yerde saklanmaz. Böylece hem araçlar hem görüntü
//   aynı fonksiyonu sorup her zaman aynı cevabı alır.
public class TrafficLightSystem : MonoBehaviour
{
    [Header("Bağlantılar (Inspector'dan sürükle)")]
    public CityGenerator city;
    [Tooltip("Emission'ı açık şablon malzeme (SehirMalzemesi)")]
    public Material baseMaterial;

    [Header("Oyuncu")]
    public Transform taxi;                    // Kırmızıda geçip geçmediğini izlemek için
    public float minViolationSpeed = 2f;      // Bundan yavaş girişler sayılmaz (m/s)

    // OLAY: Oyuncu kırmızıda kavşağa girdi. PassengerManager buna abone olup ceza verir.
    // (Taksinin OnCrash olayıyla aynı yöntem.)
    public System.Action OnRedLightRun;

    [Header("Süreler (saniye)")]
    // Gerçek hayattan kısa: oyunda 10 saniyeden uzun kırmızı beklemek sıkıcı olur
    public float greenTime = 6f;
    public float yellowTime = 1.5f;
    public float allRedTime = 0.5f;    // İki yön de kırmızı: kavşak boşalsın diye

    // Bir yaklaşım yönündeki ışık başlığı ve üç lambası
    class SignalHead
    {
        public Vector2Int node;
        public bool northSouth;
        public Renderer red, yellow, green;
        public int shown = -1;           // Şu an gösterilen renk (-1 = henüz hiç)
    }

    List<SignalHead> heads = new List<SignalHead>();
    Dictionary<Vector2Int, float> offsets = new Dictionary<Vector2Int, float>();
    float cell;
    int minIndex, maxIndex;
    Material offMat, redMat, yellowMat, greenMat, poleMat;
    Vector2Int? playerIntersection;           // Oyuncunun şu an içinde olduğu kavşak (yoksa null)
    Vector3 lastTaxiPos;

    void Start()
    {
        cell = city.blockSize + city.roadWidth;
        int half = city.blocksPerSide / 2;
        minIndex = -half;
        maxIndex = city.blocksPerSide - half;

        offMat = MakeMaterial(new Color(0.12f, 0.12f, 0.12f), Color.black);
        redMat = MakeMaterial(new Color(1f, 0.1f, 0.05f), new Color(1f, 0.1f, 0.05f) * 3f);
        yellowMat = MakeMaterial(new Color(1f, 0.7f, 0f), new Color(1f, 0.7f, 0f) * 3f);
        greenMat = MakeMaterial(new Color(0.1f, 1f, 0.3f), new Color(0.1f, 1f, 0.3f) * 3f);
        poleMat = MakeMaterial(new Color(0.15f, 0.15f, 0.16f), Color.black);

        System.Random rng = new System.Random(city.seed + 7);
        float cycle = CycleLength();
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        for (int x = minIndex; x <= maxIndex; x++)
        {
            for (int z = minIndex; z <= maxIndex; z++)
            {
                Vector2Int node = new Vector2Int(x, z);
                offsets[node] = (float)rng.NextDouble() * cycle;   // Her kavşak farklı anda başlasın

                // Bu kavşağa gelinebilen her yön için bir ışık başlığı.
                // Şehrin dışından gelen yol olmadığı için kenarlarda bazı yönler atlanır.
                foreach (Vector2Int d in dirs)
                {
                    if (IsInside(node - d)) CreateHead(node, d);
                }
            }
        }

        if (taxi != null) lastTaxiPos = taxi.position;
    }

    void Update()
    {
        CheckPlayer();

        // Rengi değişen başlıkların lambalarını güncelle
        foreach (SignalHead h in heads)
        {
            int c = (int)GetSignal(h.node, h.northSouth);
            if (c == h.shown) continue;
            h.shown = c;
            h.red.sharedMaterial = c == (int)SignalColor.Red ? redMat : offMat;
            h.yellow.sharedMaterial = c == (int)SignalColor.Yellow ? yellowMat : offMat;
            h.green.sharedMaterial = c == (int)SignalColor.Green ? greenMat : offMat;
        }
    }

    // ---------- Diğer script'lerin kullanacağı fonksiyonlar ----------

    // "travelDirection" yönünde giden biri "node" kavşağına girerken hangi ışığı görür?
    public SignalColor GetSignal(Vector2Int node, Vector2Int travelDirection)
    {
        return GetSignal(node, travelDirection.x == 0);   // x değişmiyorsa kuzey-güney gidiyordur
    }

    public SignalColor GetSignal(Vector2Int node, bool northSouth)
    {
        if (!offsets.TryGetValue(node, out float offset)) return SignalColor.Green;

        float halfCycle = greenTime + yellowTime + allRedTime;
        float t = (Time.time + offset) % (halfCycle * 2f);

        bool northSouthTurn = t < halfCycle;              // Döngünün ilk yarısı K-G'nin
        if (northSouth != northSouthTurn) return SignalColor.Red;

        float p = northSouthTurn ? t : t - halfCycle;     // Kendi yarısının içindeki zaman
        if (p < greenTime) return SignalColor.Green;
        if (p < greenTime + yellowTime) return SignalColor.Yellow;
        return SignalColor.Red;
    }

    // Oyuncu bir kavşağa GİRDİĞİ anı yakalar ve o anda ışık kırmızı mı diye bakar.
    void CheckPlayer()
    {
        if (taxi == null || Time.deltaTime <= 0f) return;   // Oyun duraklatılmışsa bakma

        Vector3 pos = taxi.position;
        Vector3 delta = pos - lastTaxiPos;                   // Bu karede gidilen yol (yönü de verir)
        lastTaxiPos = pos;

        // En yakın kavşak ve taksinin onun içinde olup olmadığı
        Vector2Int node = new Vector2Int(Mathf.RoundToInt(pos.x / cell), Mathf.RoundToInt(pos.z / cell));
        float half = city.roadWidth / 2f;
        bool inside = IsInside(node)
                      && Mathf.Abs(pos.x - node.x * cell) < half
                      && Mathf.Abs(pos.z - node.y * cell) < half;

        if (!inside) { playerIntersection = null; return; }
        if (playerIntersection == node) return;              // Zaten içerideydik
        playerIntersection = node;                           // Tam şu an girdik

        float speed = delta.magnitude / Time.deltaTime;
        if (speed < minViolationSpeed) return;

        // Hangi yönden girdik? Yatay hareket büyükse doğu-batı, değilse kuzey-güney
        bool northSouth = Mathf.Abs(delta.z) > Mathf.Abs(delta.x);
        if (GetSignal(node, northSouth) == SignalColor.Red)
            OnRedLightRun?.Invoke();
    }

    float CycleLength()
    {
        return (greenTime + yellowTime + allRedTime) * 2f;
    }

    bool IsInside(Vector2Int n)
    {
        return n.x >= minIndex && n.x <= maxIndex && n.y >= minIndex && n.y <= maxIndex;
    }

    // ---------- Görüntü ----------

    // Kavşağa "d" yönünde gelen araçların göreceği direk + ışık başlığı.
    // Sağ şeritten gidildiği için direk, gelen aracın sağ tarafında, kavşaktan biraz önce durur.
    void CreateHead(Vector2Int node, Vector2Int d)
    {
        Vector3 forward = new Vector3(d.x, 0f, d.y);
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 nodePos = new Vector3(node.x * cell, 0f, node.y * cell);
        Vector3 basePos = nodePos + right * (city.roadWidth / 2f + 0.5f)
                                  - forward * (city.roadWidth / 2f + 2.5f);

        float poleHeight = 3.2f;
        GameObject pole = MakeCube("IsikDirek", basePos + Vector3.up * (0.2f + poleHeight / 2f),
                                   new Vector3(0.15f, poleHeight, 0.15f), poleMat);
        pole.transform.SetParent(transform, true);

        // Başlık: lambaları gelen trafiğe dönük
        Transform holder = new GameObject("IsikBasligi").transform;
        holder.SetParent(transform, false);
        holder.position = basePos + Vector3.up * (0.2f + poleHeight);
        holder.rotation = Quaternion.LookRotation(-forward);

        GameObject box = MakeCube("Kasa", Vector3.zero, new Vector3(0.5f, 1.3f, 0.35f), poleMat);
        box.transform.SetParent(holder, false);
        Destroy(box.GetComponent<Collider>());

        SignalHead h = new SignalHead { node = node, northSouth = d.x == 0 };
        h.red = MakeLamp(holder, 0.4f);
        h.yellow = MakeLamp(holder, 0f);
        h.green = MakeLamp(holder, -0.4f);
        heads.Add(h);
    }

    Renderer MakeLamp(Transform holder, float y)
    {
        GameObject lamp = MakeCube("Lamba", Vector3.zero, new Vector3(0.3f, 0.3f, 0.05f), offMat);
        lamp.transform.SetParent(holder, false);
        lamp.transform.localPosition = new Vector3(0f, y, 0.18f);
        Destroy(lamp.GetComponent<Collider>());
        return lamp.GetComponent<Renderer>();
    }

    GameObject MakeCube(string objName, Vector3 pos, Vector3 size, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = objName;
        go.transform.position = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    Material MakeMaterial(Color baseColor, Color emission)
    {
        Material m = baseMaterial != null
            ? new Material(baseMaterial)
            : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", baseColor);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", emission);
        return m;
    }
}
