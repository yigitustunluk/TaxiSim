using UnityEngine;
using System.Collections.Generic;

// Oyun başladığında şehrin GÖRÜNTÜSÜNÜ oluşturur.
// - "Layout" alanı doluysa: CityLayout'un ürettiği yol ağına göre düzensiz bir şehir kurar
//   (farklı boyda bloklar, parklar, meydanlar, yol çizgileri).
// - Boşsa: eski, düzgün ızgara şehri kurar.
public class CityGenerator : MonoBehaviour
{
    [Header("Yeni Şehir Planı")]
    public CityLayout layout;                 // Doluysa şehir bu plana göre kurulur
    public Transform taxi;                    // Taksiyi oyun başında bir kavşağa koymak için
    [Range(0f, 1f)] public float parkRatio = 0.12f;   // Blokların ne kadarı park olsun
    public float lotSize = 16f;               // Bir bina parselinin yaklaşık boyu

    [Header("Şehir Boyutu")]
    public int blocksPerSide = 6;      // Her kenarda kaç blok olacak
    public float blockSize = 30f;      // Bir bloğun (kaldırım dahil) genişliği
    public float roadWidth = 12f;      // Yol genişliği

    [Header("Malzeme Şablonu")]
    // Emission'ı açık bir URP/Lit materyal. Bütün şehir malzemeleri bunun kopyası olur.
    // Neden gerekli? Build alırken Unity, projede hiçbir materyalin kullanmadığı özellikleri
    // (örneğin parlama/emission) siler. Bu şablon sayesinde Unity parlamanın kullanıldığını bilir.
    public Material baseMaterial;

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
        if (layout != null && layout.Network != null) GenerateFromLayout();
        else Generate();
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

    // =====================================================================
    //  YENİ ŞEHİR: CityLayout'un planına göre
    // =====================================================================

    Material sidewalkMat, grassMat, trunkMat, leavesMat, lineWhiteMat, lineYellowMat, poleMat;

    void GenerateFromLayout()
    {
        Random.InitState(seed);
        CreateWindowTextures();

        Material asphalt = MakeMaterial(new Color(0.18f, 0.18f, 0.20f));
        sidewalkMat = MakeMaterial(new Color(0.60f, 0.60f, 0.60f));
        grassMat = MakeMaterial(new Color(0.30f, 0.52f, 0.25f));
        trunkMat = MakeMaterial(new Color(0.35f, 0.24f, 0.15f));
        leavesMat = MakeMaterial(new Color(0.20f, 0.42f, 0.18f));
        lineWhiteMat = MakeMaterial(new Color(0.92f, 0.92f, 0.90f));
        lineYellowMat = MakeMaterial(new Color(0.95f, 0.75f, 0.10f));
        poleMat = MakeMaterial(new Color(0.15f, 0.15f, 0.16f));
        lampHeadMaterial = MakeMaterial(Color.white);
        lampHeadMaterial.EnableKeyword("_EMISSION");

        float[] xs = layout.xLines, zs = layout.zLines, xw = layout.xWidths, zw = layout.zWidths;
        int n = xs.Length;

        // Asfalt: bütün şehrin altında, kenarlarda 20 m pay
        float minX = xs[0] - xw[0] / 2f - 20f, maxX = xs[n - 1] + xw[n - 1] / 2f + 20f;
        float minZ = zs[0] - zw[0] / 2f - 20f, maxZ = zs[n - 1] + zw[n - 1] / 2f + 20f;
        CreateBox("Asfalt", new Vector3((minX + maxX) / 2f, -0.5f, (minZ + maxZ) / 2f),
                  new Vector3(maxX - minX, 1f, maxZ - minZ), asphalt);

        // 1) Kapatılmış yolları bul: plana göre yol olması gereken ama ağda olmayan yerler.
        //    Yerlerine meydan koyacağız. Bitişik bloklar da daha sık park olacak.
        HashSet<Vector2Int> nearClosed = new HashSet<Vector2Int>();
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                // Doğu-batı parçası: (i,j) → (i+1,j)
                if (i + 1 < n && layout.Network.FindEdge(layout.GetNode(i, j), layout.GetNode(i + 1, j)) == null)
                {
                    CreatePlaza(new Rect(xs[i] + xw[i] / 2f, zs[j] - zw[j] / 2f,
                                         (xs[i + 1] - xw[i + 1] / 2f) - (xs[i] + xw[i] / 2f), zw[j]));
                    nearClosed.Add(new Vector2Int(i, j - 1));
                    nearClosed.Add(new Vector2Int(i, j));
                }
                // Kuzey-güney parçası: (i,j) → (i,j+1)
                if (j + 1 < n && layout.Network.FindEdge(layout.GetNode(i, j), layout.GetNode(i, j + 1)) == null)
                {
                    CreatePlaza(new Rect(xs[i] - xw[i] / 2f, zs[j] + zw[j] / 2f,
                                         xw[i], (zs[j + 1] - zw[j + 1] / 2f) - (zs[j] + zw[j] / 2f)));
                    nearClosed.Add(new Vector2Int(i - 1, j));
                    nearClosed.Add(new Vector2Int(i, j));
                }
            }
        }

        // 2) Bloklar: dört yol çizgisinin arasında kalan her dikdörtgen
        for (int i = 0; i < n - 1; i++)
        {
            for (int j = 0; j < n - 1; j++)
            {
                Rect r = new Rect(xs[i] + xw[i] / 2f, zs[j] + zw[j] / 2f, 0f, 0f);
                r.xMax = xs[i + 1] - xw[i + 1] / 2f;
                r.yMax = zs[j + 1] - zw[j + 1] / 2f;

                GameObject curb = CreateBox("Kaldirim", new Vector3(r.center.x, 0.1f, r.center.y),
                                            new Vector3(r.width, 0.2f, r.height), sidewalkMat);
                curb.AddComponent<Sidewalk>();

                bool touchesMain = layout.xMain[i] || layout.xMain[i + 1] || layout.zMain[j] || layout.zMain[j + 1];
                float parkChance = nearClosed.Contains(new Vector2Int(i, j)) ? 0.35f : parkRatio;

                if (Random.value < parkChance) CreatePark(r);
                else CreateBuildings(r, touchesMain);

                CreateLamp(new Vector3(r.xMin + 1f, 0.2f, r.yMin + 1f), poleMat);
            }
        }

        // 3) Yol çizgileri
        foreach (RoadEdge e in layout.Network.edges) CreateRoadLines(e);

        // 4) Taksiyi şehrin ortasına en yakın kavşağa koy
        PlaceTaxi();
    }

    // Bloğu parsellere böl ve her parsele bir bina koy.
    // Ana caddeye bakan bloklarda yüksek binalar, ara sokaklarda alçak evler.
    // Büyük bloklarda ortadaki parseller boş kalır: avlu.
    void CreateBuildings(Rect r, bool touchesMain)
    {
        // Binalar kaldırımın kenarından en az 2.5 m içeride başlar. Bu şerit; trafik ışığı
        // direkleri, sokak lambaları ve yolcu işaretleri için boş kalır.
        // (Önceki sürümde tek parselli dar bloklarda bina kaldırımın kenarına taşabiliyordu.)
        const float edgeGap = 2.5f;
        Rect inner = new Rect(r.xMin + edgeGap, r.yMin + edgeGap, r.width - 2f * edgeGap, r.height - 2f * edgeGap);
        if (inner.width < 4f || inner.height < 4f) return;

        int nx = Mathf.Max(1, Mathf.RoundToInt(inner.width / lotSize));
        int nz = Mathf.Max(1, Mathf.RoundToInt(inner.height / lotSize));
        float lotW = inner.width / nx, lotD = inner.height / nz;

        for (int a = 0; a < nx; a++)
        {
            for (int b = 0; b < nz; b++)
            {
                bool interior = a > 0 && a < nx - 1 && b > 0 && b < nz - 1;
                if (interior) continue;              // Avlu

                // Komşu binalarla arada 2 m boşluk. Bloğun dış kenarında boşluk gerekmez.
                float w = lotW - ((a > 0 ? 1f : 0f) + (a < nx - 1 ? 1f : 0f));
                float d = lotD - ((b > 0 ? 1f : 0f) + (b < nz - 1 ? 1f : 0f));
                if (w < 4f || d < 4f) continue;

                float h = touchesMain ? Random.Range(14f, 45f) : Random.Range(5f, 14f);
                if (Random.value < 0.15f) h *= 0.5f;    // Ara sıra alçak bir bina, siluet düz olmasın

                // Parselin içinde, kenar boşluklarına göre konum
                float x0 = inner.xMin + lotW * a + (a > 0 ? 1f : 0f);
                float z0 = inner.yMin + lotD * b + (b > 0 ? 1f : 0f);
                Vector3 pos = new Vector3(x0 + w / 2f, 0.2f + h / 2f, z0 + d / 2f);

                Color c = Color.HSVToRGB(Random.Range(0f, 1f), 0.15f, Random.Range(0.55f, 0.85f));
                CreateBox("Bina", pos, new Vector3(w, h, d), MakeBuildingMaterial(c, Mathf.Max(w, d), h));

                // Yüksek binaların bir kısmının tepesine daha dar bir kat: basamaklı siluet
                if (h > 25f && Random.value < 0.4f)
                {
                    float h2 = Random.Range(4f, 10f);
                    Vector3 top = new Vector3(pos.x, 0.2f + h + h2 / 2f, pos.z);
                    CreateBox("BinaUst", top, new Vector3(w * 0.6f, h2, d * 0.6f),
                              MakeBuildingMaterial(c, w * 0.6f, h2));
                }
            }
        }
    }

    // Park: kaldırım halkası içinde çim ve rastgele ağaçlar
    void CreatePark(Rect r)
    {
        Rect g = new Rect(r.xMin + 2f, r.yMin + 2f, r.width - 4f, r.height - 4f);
        GameObject grass = CreateBox("Cim", new Vector3(g.center.x, 0.22f, g.center.y),
                                     new Vector3(g.width, 0.05f, g.height), grassMat);
        grass.AddComponent<Sidewalk>();   // Parkta sürmek de kaldırımda sürmek gibi cezalı

        int trees = Mathf.Max(2, Mathf.RoundToInt(g.width * g.height / 120f));
        for (int t = 0; t < trees; t++)
            CreateTree(new Vector3(Random.Range(g.xMin + 2f, g.xMax - 2f), 0.25f, Random.Range(g.yMin + 2f, g.yMax - 2f)));
    }

    // Meydan: kapatılmış yolun yeri. Kaldırımla kaplı, birkaç ağaç.
    void CreatePlaza(Rect r)
    {
        GameObject plaza = CreateBox("Meydan", new Vector3(r.center.x, 0.1f, r.center.y),
                                     new Vector3(r.width, 0.2f, r.height), sidewalkMat);
        plaza.AddComponent<Sidewalk>();
        if (r.width > 6f && r.height > 6f)
        {
            CreateTree(new Vector3(r.center.x - r.width * 0.25f, 0.2f, r.center.y - r.height * 0.25f));
            CreateTree(new Vector3(r.center.x + r.width * 0.25f, 0.2f, r.center.y + r.height * 0.25f));
        }
    }

    void CreateTree(Vector3 basePos)
    {
        float h = Random.Range(2.5f, 4f);
        CreateBox("Govde", basePos + Vector3.up * (h / 2f), new Vector3(0.35f, h, 0.35f), trunkMat);

        GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crown.name = "Yaprak";
        crown.transform.SetParent(transform);
        float s = Random.Range(2.5f, 4f);
        crown.transform.position = basePos + Vector3.up * (h + s * 0.35f);
        crown.transform.localScale = new Vector3(s, s * 0.9f, s);
        crown.GetComponent<Renderer>().sharedMaterial = leavesMat;
        Destroy(crown.GetComponent<Collider>());   // Sadece gövdeye çarpılsın
    }

    // Yolun ortasına çizgi: ana caddelerde sürekli çift sarı, ara sokaklarda kesik beyaz.
    // Çizgiler kavşakların içine girmez.
    void CreateRoadLines(RoadEdge e)
    {
        Vector3 dir = (e.b.position - e.a.position).normalized;
        bool alongX = Mathf.Abs(dir.x) > 0.5f;

        // Kavşağın bu yön boyunca yarı genişliği = kesişen yolun yarı genişliği
        float startGap = (alongX ? layout.xWidths[e.a.gridIndex.x] : layout.zWidths[e.a.gridIndex.y]) / 2f + 1f;
        float endGap = (alongX ? layout.xWidths[e.b.gridIndex.x] : layout.zWidths[e.b.gridIndex.y]) / 2f + 1f;

        Vector3 start = e.a.position + dir * startGap;
        float length = e.Length - startGap - endGap;
        if (length <= 0f) return;

        Quaternion rot = Quaternion.LookRotation(dir);
        Vector3 side = Vector3.Cross(Vector3.up, dir);

        if (e.isMain)
        {
            for (int k = -1; k <= 1; k += 2)
            {
                Vector3 c = start + dir * (length / 2f) + side * (0.15f * k) + Vector3.up * 0.01f;
                CreateLine(c, rot, new Vector3(0.12f, 0.02f, length), lineYellowMat);
            }
        }
        else
        {
            float dash = 3f, gap = 3f;
            for (float d = 0f; d + dash <= length; d += dash + gap)
            {
                Vector3 c = start + dir * (d + dash / 2f) + Vector3.up * 0.01f;
                CreateLine(c, rot, new Vector3(0.15f, 0.02f, dash), lineWhiteMat);
            }
        }
    }

    void CreateLine(Vector3 center, Quaternion rot, Vector3 size, Material mat)
    {
        GameObject go = CreateBox("Cizgi", center, size, mat);
        go.transform.rotation = rot;
        Destroy(go.GetComponent<Collider>());
    }

    void PlaceTaxi()
    {
        if (taxi == null) return;
        RoadNode start = layout.Network.NearestNode(Vector3.zero);
        Vector3 forward = start.edges.Count > 0 ? start.edges[0].DirectionFrom(start) : Vector3.forward;

        Vector3 pos = start.position + Vector3.up * 0.5f;
        Quaternion rot = Quaternion.LookRotation(forward);
        taxi.SetPositionAndRotation(pos, rot);
        Rigidbody rb = taxi.GetComponent<Rigidbody>();
        if (rb != null) { rb.position = pos; rb.rotation = rot; }
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
        Material m;
        if (baseMaterial != null)
        {
            m = new Material(baseMaterial);              // Şablonun kopyası
        }
        else
        {
            Debug.LogWarning("CityGenerator: Base Material boş! Build'de parlamalar çalışmayabilir.");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            m = new Material(shader);
        }
        m.SetColor("_BaseColor", color);
        m.color = color;
        m.SetColor("_EmissionColor", Color.black);       // Varsayılan: parlamasın
        return m;
    }
}
