using UnityEngine;
using System.Collections.Generic;

public enum SignalColor { Red, Yellow, Green }

// Trafik ışıkları. City objesine eklenir.
// Hangi kavşakta ışık olacağına CityLayout karar verir (RoadNode.hasTrafficLight).
// Bu script ışıkların zamanlamasını, direkleri, dur çizgilerini ve kırmızıda geçme tespitini yapar.
//
// - Her ışıklı kavşağın iki "yönü" var: Kuzey-Güney ve Doğu-Batı. Aynı anda sadece biri yeşil.
// - Döngü: K-G yeşil → K-G sarı → hepsi kırmızı → D-B yeşil → D-B sarı → hepsi kırmızı → başa.
// - Işığın rengi zamandan HESAPLANIR, bir yerde saklanmaz. Lambalar, trafik araçları ve
//   kırmızı ışık tespiti aynı fonksiyonu sorar, hep aynı cevabı alır.
public class TrafficLightSystem : MonoBehaviour
{
    [Header("Bağlantılar (Inspector'dan sürükle)")]
    public CityLayout layout;
    [Tooltip("Emission'ı açık şablon malzeme (SehirMalzemesi)")]
    public Material baseMaterial;

    [Header("Oyuncu")]
    public Transform taxi;
    public float minViolationSpeed = 2f;      // Bundan yavaş girişler sayılmaz (m/s)

    // OLAY: Oyuncu kırmızıda kavşağa girdi. PassengerManager buna abone olup ceza verir.
    public System.Action OnRedLightRun;

    [Header("Süreler (saniye)")]
    public float greenTime = 6f;
    public float yellowTime = 1.5f;
    public float allRedTime = 0.5f;

    class SignalHead
    {
        public RoadNode node;
        public bool northSouth;
        public Renderer red, yellow, green;
        public int shown = -1;
    }

    List<SignalHead> heads = new List<SignalHead>();
    Dictionary<RoadNode, float> offsets = new Dictionary<RoadNode, float>();
    Material offMat, redMat, yellowMat, greenMat, poleMat, lineMat;
    RoadNode playerIntersection;              // Oyuncunun şu an içinde olduğu kavşak (yoksa null)
    Vector3 lastTaxiPos;

    void Start()
    {
        offMat = MakeMaterial(new Color(0.12f, 0.12f, 0.12f), Color.black);
        redMat = MakeMaterial(new Color(1f, 0.1f, 0.05f), new Color(1f, 0.1f, 0.05f) * 3f);
        yellowMat = MakeMaterial(new Color(1f, 0.7f, 0f), new Color(1f, 0.7f, 0f) * 3f);
        greenMat = MakeMaterial(new Color(0.1f, 1f, 0.3f), new Color(0.1f, 1f, 0.3f) * 3f);
        poleMat = MakeMaterial(new Color(0.15f, 0.15f, 0.16f), Color.black);
        lineMat = MakeMaterial(new Color(0.92f, 0.92f, 0.90f), Color.black);

        System.Random rng = new System.Random(layout.seed + 7);
        float cycle = (greenTime + yellowTime + allRedTime) * 2f;

        foreach (RoadNode node in layout.Network.nodes)
        {
            if (!node.hasTrafficLight) continue;
            offsets[node] = (float)rng.NextDouble() * cycle;   // Her kavşak farklı anda başlasın

            // Kavşağa gelen her yol için: direk + ışık başlığı + dur çizgisi
            foreach (RoadEdge e in node.edges)
                CreateApproach(node, e);
        }

        if (taxi != null) lastTaxiPos = taxi.position;
    }

    void Update()
    {
        CheckPlayer();

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

    // ---------- Diğer script'lerin kullandığı fonksiyonlar ----------

    // "travelDirection" yönünde giden biri "node" kavşağına girerken hangi ışığı görür?
    public SignalColor GetSignal(RoadNode node, Vector3 travelDirection)
    {
        return GetSignal(node, Mathf.Abs(travelDirection.z) > Mathf.Abs(travelDirection.x));
    }

    public SignalColor GetSignal(RoadNode node, bool northSouth)
    {
        if (node == null || !offsets.TryGetValue(node, out float offset)) return SignalColor.Green;   // Işıksız kavşak

        float halfCycle = greenTime + yellowTime + allRedTime;
        float t = (Time.time + offset) % (halfCycle * 2f);

        bool northSouthTurn = t < halfCycle;
        if (northSouth != northSouthTurn) return SignalColor.Red;

        float p = northSouthTurn ? t : t - halfCycle;
        if (p < greenTime) return SignalColor.Green;
        if (p < greenTime + yellowTime) return SignalColor.Yellow;
        return SignalColor.Red;
    }

    // Kavşağın, verilen yön boyunca yarı genişliği = kesişen yolun yarı genişliği
    public float IntersectionHalfSize(RoadNode node, Vector3 direction)
    {
        bool alongX = Mathf.Abs(direction.x) > 0.5f;
        return (alongX ? layout.xWidths[node.gridIndex.x] : layout.zWidths[node.gridIndex.y]) / 2f;
    }

    // ---------- Kırmızı ışık tespiti ----------

    // Oyuncu ışıklı bir kavşağa GİRDİĞİ anı yakalar ve o anda ışık kırmızı mı diye bakar.
    // Hangi şeritten girdiğinin önemi yok: sadece hareket yönüne bakılır.
    void CheckPlayer()
    {
        if (taxi == null || Time.deltaTime <= 0f) return;

        Vector3 pos = taxi.position;
        Vector3 delta = pos - lastTaxiPos;
        lastTaxiPos = pos;

        RoadNode node = layout.Network.NearestNode(pos);
        bool inside = Mathf.Abs(pos.x - node.position.x) < layout.xWidths[node.gridIndex.x] / 2f
                   && Mathf.Abs(pos.z - node.position.z) < layout.zWidths[node.gridIndex.y] / 2f;

        if (!inside) { playerIntersection = null; return; }
        if (playerIntersection == node) return;              // Zaten içerideydik
        playerIntersection = node;                           // Tam şu an girdik

        if (!node.hasTrafficLight) return;
        if (delta.magnitude / Time.deltaTime < minViolationSpeed) return;

        if (GetSignal(node, delta) == SignalColor.Red)
            OnRedLightRun?.Invoke();
    }

    // ---------- Görüntü ----------

    // "edge" yolundan "node" kavşağına gelen araçlar için direk, ışık başlığı ve dur çizgisi.
    void CreateApproach(RoadNode node, RoadEdge edge)
    {
        Vector3 forward = -edge.DirectionFrom(node);                 // Kavşağa doğru gidiş yönü
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        float crossHalf = IntersectionHalfSize(node, forward);

        // Direk: gelen aracın sağında, kavşaktan 2.5 m önce, kaldırımda
        Vector3 basePos = node.position + right * (edge.width / 2f + 0.5f) - forward * (crossHalf + 2.5f);
        float poleHeight = 3.2f;
        MakeCube("IsikDirek", basePos + Vector3.up * (0.2f + poleHeight / 2f),
                 Quaternion.identity, new Vector3(0.15f, poleHeight, 0.15f), poleMat, true);

        Transform holder = new GameObject("IsikBasligi").transform;
        holder.SetParent(transform, false);
        holder.position = basePos + Vector3.up * (0.2f + poleHeight);
        holder.rotation = Quaternion.LookRotation(-forward);

        GameObject box = MakeCube("Kasa", Vector3.zero, Quaternion.identity, new Vector3(0.5f, 1.3f, 0.35f), poleMat, false);
        box.transform.SetParent(holder, false);

        SignalHead h = new SignalHead { node = node, northSouth = Mathf.Abs(forward.z) > Mathf.Abs(forward.x) };
        h.red = MakeLamp(holder, 0.4f);
        h.yellow = MakeLamp(holder, 0f);
        h.green = MakeLamp(holder, -0.4f);
        heads.Add(h);

        // Dur çizgisi: gelen şeridin (yolun sağ yarısı) boyunca, direğin hemen önünde
        Vector3 lineCenter = node.position - forward * (crossHalf + 2.8f) + right * (edge.width / 4f) + Vector3.up * 0.01f;
        MakeCube("DurCizgisi", lineCenter, Quaternion.LookRotation(forward),
                 new Vector3(edge.width / 2f - 0.6f, 0.02f, 0.45f), lineMat, false);
    }

    Renderer MakeLamp(Transform holder, float y)
    {
        GameObject lamp = MakeCube("Lamba", Vector3.zero, Quaternion.identity, new Vector3(0.3f, 0.3f, 0.05f), offMat, false);
        lamp.transform.SetParent(holder, false);
        lamp.transform.localPosition = new Vector3(0f, y, 0.18f);
        return lamp.GetComponent<Renderer>();
    }

    GameObject MakeCube(string objName, Vector3 pos, Quaternion rot, Vector3 size, Material mat, bool keepCollider)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = objName;
        go.transform.SetParent(transform, false);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!keepCollider) Destroy(go.GetComponent<Collider>());
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
