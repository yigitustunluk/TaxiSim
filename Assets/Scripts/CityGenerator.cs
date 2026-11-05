using UnityEngine;

// Oyun başladığında ızgara (grid) şeklinde basit bir şehir oluşturur:
// asfalt zemin, kaldırımlı bloklar ve her blokta 4 bina.
// Yollar x=0 ve z=0 çizgilerinden geçer, yani taksi (0,0,0)'da bir kavşakta başlar.
public class CityGenerator : MonoBehaviour
{
    [Header("Şehir Boyutu")]
    public int blocksPerSide = 6;      // Her kenarda kaç blok olacak
    public float blockSize = 30f;      // Bir bloğun (kaldırım dahil) genişliği
    public float roadWidth = 12f;      // Yol genişliği

    [Header("Binalar")]
    public float minBuildingHeight = 6f;
    public float maxBuildingHeight = 35f;
    public int seed = 42;              // Aynı sayı = her seferinde aynı şehir

    void Start()
    {
        Generate();
    }

    void Generate()
    {
        Random.InitState(seed);

        Material asphalt  = MakeMaterial(new Color(0.18f, 0.18f, 0.20f));
        Material sidewalk = MakeMaterial(new Color(0.60f, 0.60f, 0.60f));

        float cell = blockSize + roadWidth;          // Bir blok + bir yol
        int half = blocksPerSide / 2;
        float citySize = blocksPerSide * cell + roadWidth;

        // Asfalt zemin: üst yüzeyi tam y=0'da olacak şekilde
        CreateBox("Asfalt", new Vector3(0f, -0.5f, 0f),
                  new Vector3(citySize + 40f, 1f, citySize + 40f), asphalt);

        for (int x = -half; x < blocksPerSide - half; x++)
        {
            for (int z = -half; z < blocksPerSide - half; z++)
            {
                // Bloğun merkezi: iki yol çizgisinin tam ortası
                Vector3 center = new Vector3(x * cell + cell / 2f, 0f, z * cell + cell / 2f);

                // Kaldırım: 20 cm yüksek. Taksi buna çarpınca durur, yani yolda kalır.
                CreateBox("Kaldirim", center + Vector3.up * 0.1f,
                          new Vector3(blockSize, 0.2f, blockSize), sidewalk);

                // Bloğu 2x2 parsele böl, her parsele bir bina koy
                float lot = blockSize / 2f;
                float footprint = lot - 3f;          // Binalar arasında boşluk kalsın
                for (int i = 0; i < 2; i++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        float h = Random.Range(minBuildingHeight, maxBuildingHeight);
                        Vector3 pos = center + new Vector3((i - 0.5f) * lot, 0.2f + h / 2f, (j - 0.5f) * lot);
                        Color c = Color.HSVToRGB(Random.Range(0f, 1f), 0.15f, Random.Range(0.55f, 0.85f));
                        CreateBox("Bina", pos, new Vector3(footprint, h, footprint), MakeMaterial(c));
                    }
                }
            }
        }
    }

    // Verilen konum ve boyutta, çarpışması olan bir küp oluşturur
    GameObject CreateBox(string name, Vector3 position, Vector3 size, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(transform);
        go.transform.position = position;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    // URP projelerinde çalışan basit renkli materyal
    Material MakeMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material m = new Material(shader);
        m.SetColor("_BaseColor", color);
        m.color = color;
        return m;
    }
}
