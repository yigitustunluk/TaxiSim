using UnityEngine;
using System.Collections.Generic;

// Şehrin PLANI: yol ağını üretir. Görüntü üretmez (o CityGenerator'ın işi).
// City objesine eklenir. Ağı Awake'te üretir, böylece diğer script'ler Start'ta hazır bulur.
//
// Üretim:
// 1. Düzensiz aralıklı dikey ve yatay "çizgiler" belirlenir (blok genişlikleri rastgele).
// 2. Bazı çizgiler geniş ana cadde, diğerleri dar ara sokak olur.
// 3. Çizgilerin kesiştiği her yer bir kavşak, komşu kavşaklar arası bir yol olur.
// 4. Bazı ara sokaklar kapatılır (yerlerine ileride park/meydan gelecek). Şehir bölünmesin
//    ve çıkmaz sokak oluşmasın diye her kapatma kontrol edilir.
public class CityLayout : MonoBehaviour
{
    [Header("Boyut")]
    public int linesPerAxis = 7;           // Her yönde kaç yol çizgisi (7 → 6x6 blok)
    public float minBlockSize = 22f;
    public float maxBlockSize = 46f;

    [Header("Yollar")]
    public float mainRoadWidth = 14f;
    public float sideRoadWidth = 9f;
    public int mainRoadsPerAxis = 2;       // Her yönde kaç ana cadde
    [Range(0f, 0.5f)] public float closedStreetRatio = 0.15f;   // Ara sokakların ne kadarı kapatılsın

    [Header("Trafik Işıkları")]
    [Tooltip("Ana caddenin dört yollu bir ara sokakla kesiştiği kavşakların ne kadarında ışık olsun. " +
             "İki ana caddenin kesiştiği kavşaklarda her zaman ışık var.")]
    [Range(0f, 1f)] public float sideLightChance = 0.35f;

    public int seed = 42;

    public RoadNetwork Network { get; private set; }

    // Sütun ve satır çizgilerinin konumları ve genişlikleri (görüntü üretirken lazım olacak)
    public float[] xLines { get; private set; }
    public float[] zLines { get; private set; }
    public float[] xWidths { get; private set; }
    public float[] zWidths { get; private set; }
    public bool[] xMain { get; private set; }     // Hangi sütun çizgileri ana cadde
    public bool[] zMain { get; private set; }

    RoadNode[,] grid;

    // Sütun i, satır j'deki kavşak
    public RoadNode GetNode(int i, int j) => grid[i, j];

    void Awake()
    {
        Generate();
    }

    void Generate()
    {
        System.Random rng = new System.Random(seed);
        Network = new RoadNetwork();

        // 1-2. Çizgiler ve hangilerinin ana cadde olduğu
        xMain = PickMainLines(rng);
        zMain = PickMainLines(rng);
        xWidths = Widths(xMain);
        zWidths = Widths(zMain);
        xLines = Positions(rng, xWidths);
        zLines = Positions(rng, zWidths);

        // 3. Kavşaklar
        int n = linesPerAxis;
        grid = new RoadNode[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                grid[i, j] = Network.AddNode(new Vector3(xLines[i], 0f, zLines[j]), new Vector2Int(i, j));

        // Yollar: Doğu-batı yönündeki bir yol, üzerinde durduğu satır çizgisinin (z) genişliğini alır.
        // Kuzey-güney yönündeki bir yol, sütun çizgisinin (x) genişliğini alır.
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                if (i + 1 < n) Network.AddEdge(grid[i, j], grid[i + 1, j], zWidths[j], zMain[j]);
                if (j + 1 < n) Network.AddEdge(grid[i, j], grid[i, j + 1], xWidths[i], xMain[i]);
            }
        }

        // 4. Bazı ara sokakları kapat
        CloseSomeStreets(rng);

        // 5. Hangi kavşaklarda trafik ışığı olacak
        PlaceTrafficLights(rng);

        Debug.Log("Yol ağı: " + Network.nodes.Count + " kavşak, " + Network.edges.Count + " yol");
    }

    // Gerçek şehirlerdeki gibi: her kavşakta değil, sadece önemli kavşaklarda ışık var.
    void PlaceTrafficLights(System.Random rng)
    {
        int count = 0;
        foreach (RoadNode node in Network.nodes)
        {
            bool mainAlongX = false, mainAlongZ = false;
            foreach (RoadEdge e in node.edges)
            {
                if (!e.isMain) continue;
                Vector3 d = e.DirectionFrom(node);
                if (Mathf.Abs(d.x) > 0.5f) mainAlongX = true; else mainAlongZ = true;
            }

            if (mainAlongX && mainAlongZ)
                node.hasTrafficLight = true;                    // İki ana cadde kesişiyor
            else if ((mainAlongX || mainAlongZ) && node.edges.Count == 4)
                node.hasTrafficLight = rng.NextDouble() < sideLightChance;

            if (node.hasTrafficLight) count++;
        }
        Debug.Log("Trafik ışıklı kavşak: " + count);
    }

    bool[] PickMainLines(System.Random rng)
    {
        bool[] main = new bool[linesPerAxis];
        // Kenarlar hariç iç çizgilerden seç, yan yana iki ana cadde olmasın
        List<int> candidates = new List<int>();
        for (int i = 1; i < linesPerAxis - 1; i++) candidates.Add(i);

        int picked = 0;
        while (picked < mainRoadsPerAxis && candidates.Count > 0)
        {
            int idx = candidates[rng.Next(candidates.Count)];
            main[idx] = true;
            picked++;
            candidates.Remove(idx);
            candidates.Remove(idx - 1);
            candidates.Remove(idx + 1);
        }
        return main;
    }

    float[] Widths(bool[] main)
    {
        float[] w = new float[main.Length];
        for (int i = 0; i < main.Length; i++) w[i] = main[i] ? mainRoadWidth : sideRoadWidth;
        return w;
    }

    // Çizgilerin konumu: her blok rastgele genişlikte, araya yolların yarı genişlikleri eklenir.
    // Sonra bütün şehir (0,0) etrafında ortalanır.
    float[] Positions(System.Random rng, float[] widths)
    {
        float[] p = new float[widths.Length];
        for (int i = 1; i < widths.Length; i++)
        {
            float block = Mathf.Lerp(minBlockSize, maxBlockSize, (float)rng.NextDouble());
            p[i] = p[i - 1] + widths[i - 1] / 2f + block + widths[i] / 2f;
        }
        float center = p[p.Length - 1] / 2f;
        for (int i = 0; i < p.Length; i++) p[i] -= center;
        return p;
    }

    void CloseSomeStreets(System.Random rng)
    {
        List<RoadEdge> sideStreets = new List<RoadEdge>();
        foreach (RoadEdge e in Network.edges) if (!e.isMain) sideStreets.Add(e);

        int toClose = Mathf.RoundToInt(sideStreets.Count * closedStreetRatio);
        int closed = 0, attempts = 0;

        while (closed < toClose && attempts < 500 && sideStreets.Count > 0)
        {
            attempts++;
            RoadEdge e = sideStreets[rng.Next(sideStreets.Count)];
            sideStreets.Remove(e);

            // Çıkmaz sokak oluşmasın: iki ucunda da en az 3 yol olmalı (kapatınca 2 kalır)
            if (e.a.edges.Count < 3 || e.b.edges.Count < 3) continue;

            Network.RemoveEdge(e);
            if (Network.IsConnected())
            {
                closed++;
            }
            else
            {
                // Şehri ikiye böldü, geri aç
                Network.AddEdge(e.a, e.b, e.width, e.isMain);
            }
        }
    }

    // Scene penceresinde ağı çiz (sadece oyun çalışırken; ağ Awake'te üretiliyor).
    // Ana caddeler sarı, ara sokaklar beyaz, kavşaklar küçük küreler.
    void OnDrawGizmos()
    {
        if (Network == null) return;
        foreach (RoadEdge e in Network.edges)
        {
            Gizmos.color = e.isMain ? Color.yellow : Color.white;
            Vector3 up = Vector3.up * 0.5f;
            Gizmos.DrawLine(e.a.position + up, e.b.position + up);
        }
        Gizmos.color = Color.cyan;
        foreach (RoadNode node in Network.nodes)
            Gizmos.DrawSphere(node.position + Vector3.up * 0.5f, 1.2f);
    }
}
