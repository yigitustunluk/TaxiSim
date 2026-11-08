using UnityEngine;
using System.Collections.Generic;

// Oyun başında yol ağının farklı kavşaklarına trafik araçları yerleştirir.
// Araçlar bir "prefab"tan (hazır kalıp) kopyalanır.
public class TrafficSpawner : MonoBehaviour
{
    [Header("Bağlantılar (Inspector'dan sürükle)")]
    public TrafficCar carPrefab;     // Project'teki TrafficCar prefab'ı
    public CityLayout layout;
    public TrafficLightSystem lights;

    [Header("Ayarlar")]
    public int carCount = 14;        // Şehir büyüdü, araç sayısı da arttı

    void Start()
    {
        // Bütün kavşakların listesi (taksinin başladığı, şehrin ortasındaki kavşak hariç)
        RoadNode taxiStart = layout.Network.NearestNode(Vector3.zero);
        List<RoadNode> nodes = new List<RoadNode>(layout.Network.nodes);
        nodes.Remove(taxiStart);

        // Listeyi karıştır ki araçlar her seferinde farklı yerlerde başlasın
        System.Random rng = new System.Random();
        for (int i = nodes.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (nodes[i], nodes[j]) = (nodes[j], nodes[i]);   // İki elemanın yerini değiştir
        }

        int count = Mathf.Min(carCount, nodes.Count);
        for (int i = 0; i < count; i++)
        {
            TrafficCar car = Instantiate(carPrefab, nodes[i].position, Quaternion.identity, transform);
            car.name = "TrafficCar_" + i;

            // Prefab'lar sahnedeki objelere bağlantı tutamaz, o yüzden burada veriyoruz.
            // Bu satırlar aracın Start()'ından önce çalışır.
            car.layout = layout;
            car.lights = lights;
        }
    }
}
