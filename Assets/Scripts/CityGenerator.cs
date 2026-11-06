using UnityEngine;
using System.Collections.Generic;

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

    [Header("Sokak Lambaları")]
    public float lampHeight = 5f;
    public float lampRange = 20f;
    public float lampIntensity = 6f;
    public Color lampColor = new Color(1f, 0.78f, 0.45f);   // Sıcak sarı (sodyum lamba)

    // DayNightCycle bu ışıkları gece açıp gündüz kapatır
    [HideInInspector] public List<Light> streetLights = new List<Light>();
    [HideInInspector] public Material lampHeadMaterial;     // Lamba başlığının parlayan malzemesi
    [HideInInspector] public List<Material> buildingMaterials = new List<Material>(); // Gece pencereleri yakmak için

    Texture2D windowTexture;     // Duvar + pencere camı deseni (gündüz görünen)
    Texture2D windowLightMap;    // Hangi pencerelerin ışığı yanıyor (gece parlayan)

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

        CreateWindowTextures();

        Material pole = MakeMaterial(new Color(0.15f, 0.15f, 0.16f));
        lampHeadMaterial = MakeMaterial(Color.white);
        lampHeadMaterial.EnableKeyword("_EMISSION");   // Kendi kendine parlayabilsin

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
                GameObject curb = CreateBox("Kaldirim", center + Vector3.up * 0.1f,
                                            new Vector3(blockSize, 0.2f, blockSize), sidewalk);
                curb.AddComponent<Sidewalk>();   // "Ben kaldırımım" etiketi

                // Her bloğun bir köşesine (kavşağa bakan) bir sokak lambası
                Vector3 corner = center + new Vector3(-blockSize / 2f + 1f, 0.2f, -blockSize / 2f + 1f);
                CreateLamp(corner, pole);

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
                        CreateBox("Bina", pos, new Vector3(footprint, h, footprint), MakeBuildingMaterial(c, footprint, h));
                    }
                }
            }
        }
    }

    // Direk + parlayan başlık + gerçek ışık kaynağından oluşan bir sokak lambası
    void CreateLamp(Vector3 basePos, Material poleMat)
    {
        CreateBox("LambaDirek", basePos + Vector3.up * (lampHeight / 2f),
                  new Vector3(0.2f, lampHeight, 0.2f), poleMat);

        GameObject head = CreateBox("LambaBasi", basePos + Vector3.up * lampHeight,
                                    new Vector3(0.9f, 0.25f, 0.9f), lampHeadMaterial);
        head.GetComponent<Collider>().enabled = false;

        GameObject lightObj = new GameObject("LambaIsigi");
        lightObj.transform.SetParent(transform);
        lightObj.transform.position = basePos + Vector3.up * (lampHeight - 0.4f);
        Light l = lightObj.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = lampRange;
        l.intensity = lampIntensity;
        l.color = lampColor;
        l.shadows = LightShadows.None;   // 36 lamba gölge yaparsa oyun kasar
        l.enabled = false;
        streetLights.Add(l);
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

    // Pencereli bina malzemesi. Deseni binanın boyutuna göre tekrarlar (tiling),
    // böylece her kat ve pencere gerçek boyutunda görünür.
    Material MakeBuildingMaterial(Color color, float width, float height)
    {
        Material m = MakeMaterial(color);
        m.SetTexture("_BaseMap", windowTexture);
        m.SetTexture("_EmissionMap", windowLightMap);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", Color.black);   // Gündüz sönük, DayNightCycle gece yakar

        // Desen 8x8 pencere. Bir pencere ~3 m genişlikte, bir kat ~3.5 m yükseklikte.
        // Pencere ve kat sayısını TAM SAYIYA yuvarlıyoruz. Yoksa desen binanın kenarında
        // ortasından kesilir ve yarım pencereler (özellikle en altta) oluşur.
        int windowsAcross = Mathf.Max(1, Mathf.RoundToInt(width / 3f));
        int floors = Mathf.Max(1, Mathf.RoundToInt(height / 3.5f));
        m.SetTextureScale("_BaseMap", new Vector2(windowsAcross / 8f, floors / 8f));
        // Her binada desenin farklı bir yerinden başla ki aynı pencereler yanmasın
        m.SetTextureOffset("_BaseMap", new Vector2(Random.Range(0, 8) / 8f, Random.Range(0, 8) / 8f));

        buildingMaterials.Add(m);
        return m;
    }

    // Kodla iki resim (texture) çizer: pencereli duvar ve yanan pencereler.
    // Dışarıdan resim dosyası indirmeye gerek kalmaz.
    void CreateWindowTextures()
    {
        const int cells = 8, cellPx = 32, size = cells * cellPx;
        windowTexture = new Texture2D(size, size);
        windowLightMap = new Texture2D(size, size);
        System.Random rng = new System.Random(seed + 1);   // Şehrin rastgeleliğini bozmasın diye ayrı

        Color wall = Color.white;                          // Duvar: binanın kendi rengini alır
        Color glass = new Color(0.22f, 0.26f, 0.34f);      // Cam: koyu mavi-gri
        Color warm = new Color(1f, 0.82f, 0.5f);           // Sarı ampul
        Color cool = new Color(0.75f, 0.88f, 1f);          // Beyaz-mavi (televizyon, LED)

        for (int cx = 0; cx < cells; cx++)
        {
            for (int cy = 0; cy < cells; cy++)
            {
                // Pencerelerin %40'ı yanık olsun, bunların bir kısmı soğuk renkli
                Color lit = Color.black;
                if (rng.NextDouble() < 0.4) lit = rng.NextDouble() < 0.7 ? warm : cool;

                for (int px = 0; px < cellPx; px++)
                {
                    for (int py = 0; py < cellPx; py++)
                    {
                        bool isWindow = px >= 7 && px < 25 && py >= 8 && py < 26;
                        int x = cx * cellPx + px, y = cy * cellPx + py;
                        windowTexture.SetPixel(x, y, isWindow ? glass : wall);
                        windowLightMap.SetPixel(x, y, isWindow ? lit : Color.black);
                    }
                }
            }
        }
        windowTexture.Apply();
        windowLightMap.Apply();
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
