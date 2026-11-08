using UnityEngine;
using System.Collections.Generic;

// Oyun başında şehrin farklı kavşaklarına trafik araçları yerleştirir.
// Araçlar bir "prefab"tan (hazır kalıp) kopyalanır.
public class TrafficSpawner : MonoBehaviour
{
    [Header("Bağlantılar (Inspector'dan sürükle)")]
    public TrafficCar carPrefab;     // Project'teki TrafficCar prefab'ı
    public CityGenerator city;
    public TrafficLightSystem lights;

    [Header("Ayarlar")]
    public int carCount = 8;         // Kaç araç olacak

    void Start()
    {
        float cell = city.blockSize + city.roadWidth;
        int half = city.blocksPerSide / 2;
        int minIndex = -half;
        int maxIndex = city.blocksPerSide - half;

        // Tüm kavşakların listesini çıkar (taksinin başladığı (0,0) hariç)
        List<Vector2Int> nodes = new List<Vector2Int>();
        for (int x = minIndex; x <= maxIndex; x++)
            for (int z = minIndex; z <= maxIndex; z++)
                if (!(x == 0 && z == 0)) nodes.Add(new Vector2Int(x, z));

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
            Vector3 pos = new Vector3(nodes[i].x * cell, 0f, nodes[i].y * cell);
            TrafficCar car = Instantiate(carPrefab, pos, Quaternion.identity, transform);
            car.name = "TrafficCar_" + i;

            // Prefab'lar sahnedeki objelere bağlantı tutamaz, o yüzden şehri burada veriyoruz.
            // Bu satır aracın Start()'ından önce çalışır, yani araç şehri hazır bulur.
            car.city = city;
            car.lights = lights;
        }
    }
}
