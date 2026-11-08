using UnityEngine;
using System.Collections.Generic;

// ŞEHRİN YOL AĞI (graph)
//
// Şehri artık "ızgara" olarak değil, bir AĞ olarak tanımlıyoruz:
// - Düğüm (node): Bir kavşak. Dünyadaki konumu var.
// - Kenar (edge): İki kavşağı bağlayan bir yol. Genişliği ve türü (ana cadde / ara sokak) var.
//
// Bu yapı ızgaradan daha genel: kavşaklar düzensiz aralıklarla durabilir, bazı yollar olmayabilir.
// İleride eğri yollu bir şehre geçmek istersek de aynı yapı işe yarar.
//
// Bu dosyadaki sınıflar MonoBehaviour değil: sahnede durmazlar, sadece veri tutarlar.

public class RoadNode
{
    public int id;
    public Vector3 position;
    public Vector2Int gridIndex;                          // Hangi sütun ve satırda (üretim sırasında)
    public List<RoadEdge> edges = new List<RoadEdge>();   // Bu kavşaktan çıkan yollar
    public bool hasTrafficLight;

    // Bu kavşağa bağlı bir ana cadde var mı?
    public int MainRoadCount
    {
        get
        {
            int count = 0;
            foreach (RoadEdge e in edges) if (e.isMain) count++;
            return count;
        }
    }
}

public class RoadEdge
{
    public int id;
    public RoadNode a, b;      // Yolun iki ucu
    public float width;
    public bool isMain;        // Ana cadde mi, ara sokak mı

    public float Length => Vector3.Distance(a.position, b.position);

    // Kavşağın bir ucundan bakınca yolun öbür ucu
    public RoadNode Other(RoadNode n) => n == a ? b : a;

    // "from" kavşağından yola girince gidilen yön (birim vektör)
    public Vector3 DirectionFrom(RoadNode from) => (Other(from).position - from.position).normalized;
}

// Bir yolun üzerindeki bir nokta: hangi yol, yolun neresi (0 = a ucu, 1 = b ucu)
public struct RoadPoint
{
    public RoadEdge edge;
    public float t;
    public Vector3 Position => Vector3.Lerp(edge.a.position, edge.b.position, t);
}

// Bulunan bir rota: toplam uzunluk ve üzerinden geçilen kavşaklar
public class Route
{
    public float length;
    public List<RoadNode> nodes = new List<RoadNode>();
}

public class RoadNetwork
{
    public List<RoadNode> nodes = new List<RoadNode>();
    public List<RoadEdge> edges = new List<RoadEdge>();

    public RoadNode AddNode(Vector3 position, Vector2Int gridIndex)
    {
        RoadNode n = new RoadNode { id = nodes.Count, position = position, gridIndex = gridIndex };
        nodes.Add(n);
        return n;
    }

    public RoadEdge AddEdge(RoadNode a, RoadNode b, float width, bool isMain)
    {
        RoadEdge e = new RoadEdge { id = edges.Count, a = a, b = b, width = width, isMain = isMain };
        edges.Add(e);
        a.edges.Add(e);
        b.edges.Add(e);
        return e;
    }

    public void RemoveEdge(RoadEdge e)
    {
        edges.Remove(e);
        e.a.edges.Remove(e);
        e.b.edges.Remove(e);
    }

    // İki kavşak arasında yol var mı? Varsa onu döndürür.
    public RoadEdge FindEdge(RoadNode a, RoadNode b)
    {
        foreach (RoadEdge e in a.edges) if (e.Other(a) == b) return e;
        return null;
    }

    // Bütün kavşaklar birbirine ulaşabiliyor mu? (Yol kapatırken şehrin bölünmediğini kontrol için)
    // BFS (Breadth-First Search, genişlik öncelikli arama): bir düğümden başlayıp
    // ulaşılabilen her düğümü katman katman ziyaret eder.
    public bool IsConnected()
    {
        if (nodes.Count == 0) return true;
        HashSet<RoadNode> visited = new HashSet<RoadNode>();
        Queue<RoadNode> queue = new Queue<RoadNode>();
        queue.Enqueue(nodes[0]);
        visited.Add(nodes[0]);

        while (queue.Count > 0)
        {
            RoadNode n = queue.Dequeue();
            foreach (RoadEdge e in n.edges)
            {
                RoadNode next = e.Other(n);
                if (visited.Add(next)) queue.Enqueue(next);   // Add, yeni eklendiyse true döner
            }
        }
        return visited.Count == nodes.Count;
    }

    // ---------- EN KISA YOL: DIJKSTRA ALGORİTMASI ----------
    //
    // Fikir: Başlangıçtan her kavşağa olan en kısa mesafeyi adım adım bul.
    // 1. Başlangıcın mesafesi 0, diğer herkesin "sonsuz".
    // 2. Henüz kesinleşmemiş kavşaklardan mesafesi en küçük olanı seç. Onun mesafesi artık kesin.
    // 3. O kavşağın komşularına bak: "onun üzerinden gitmek daha kısa mı?" Kısaysa güncelle.
    // 4. Bütün kavşaklar kesinleşene kadar 2-3'ü tekrarla.
    //
    // prev[]: her kavşağa en kısa yoldan hangi kavşaktan gelindiği. Rotayı geriye doğru izlemek için.
    // (Büyük ağlarda 2. adım için "öncelik kuyruğu" kullanılır. 81 kavşakta basit döngü yeterli.)
    public void Dijkstra(RoadNode source, out float[] dist, out RoadNode[] prev)
    {
        int n = nodes.Count;
        dist = new float[n];
        prev = new RoadNode[n];
        bool[] done = new bool[n];
        for (int i = 0; i < n; i++) dist[i] = float.MaxValue;
        dist[source.id] = 0f;

        for (int step = 0; step < n; step++)
        {
            // 2. adım: kesinleşmemişler içinde en yakını bul
            int u = -1;
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
                if (!done[i] && dist[i] < best) { best = dist[i]; u = i; }
            if (u < 0) break;          // Kalanlara ulaşılamıyor
            done[u] = true;

            // 3. adım: komşuları güncelle
            foreach (RoadEdge e in nodes[u].edges)
            {
                RoadNode v = e.Other(nodes[u]);
                float candidate = dist[u] + e.Length;
                if (candidate < dist[v.id])
                {
                    dist[v.id] = candidate;
                    prev[v.id] = nodes[u];
                }
            }
        }
    }

    // Yol üzerindeki iki nokta arasındaki en kısa rota.
    // Başlangıç noktasından yolun iki ucundan birine, oradan Dijkstra ile hedef yolun
    // uçlarından birine, oradan hedefe gidilir. Dört kombinasyonun en kısası seçilir.
    public Route FindRoute(RoadPoint from, RoadPoint to)
    {
        if (from.edge == to.edge)
            return new Route { length = Mathf.Abs(from.t - to.t) * from.edge.Length };

        Route best = null;
        RoadNode[] starts = { from.edge.a, from.edge.b };
        float[] startCosts = { from.t * from.edge.Length, (1f - from.t) * from.edge.Length };
        RoadNode[] ends = { to.edge.a, to.edge.b };
        float[] endCosts = { to.t * to.edge.Length, (1f - to.t) * to.edge.Length };

        for (int s = 0; s < 2; s++)
        {
            Dijkstra(starts[s], out float[] dist, out RoadNode[] prev);
            for (int g = 0; g < 2; g++)
            {
                if (dist[ends[g].id] == float.MaxValue) continue;
                float total = startCosts[s] + dist[ends[g].id] + endCosts[g];
                if (best == null || total < best.length)
                    best = new Route { length = total, nodes = TracePath(prev, starts[s], ends[g]) };
            }
        }
        return best;
    }

    // prev[] dizisini hedeften başlangıca doğru izleyip rotadaki kavşakları çıkarır
    List<RoadNode> TracePath(RoadNode[] prev, RoadNode start, RoadNode end)
    {
        List<RoadNode> path = new List<RoadNode>();
        for (RoadNode n = end; n != null; n = n == start ? null : prev[n.id])
            path.Add(n);
        path.Reverse();
        return path;
    }

    // Dünyadaki bir noktaya en yakın kavşak
    public RoadNode NearestNode(Vector3 pos)
    {
        RoadNode best = null;
        float bestDist = float.MaxValue;
        foreach (RoadNode n in nodes)
        {
            float d = (n.position - pos).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = n; }
        }
        return best;
    }
}
