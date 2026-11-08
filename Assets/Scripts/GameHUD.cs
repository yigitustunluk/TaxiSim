using UnityEngine;
using TMPro;
using System.Text;

// Ekrandaki oyun arayüzü (HUD = Heads-Up Display).
// Oyunun mantığını bilmez, sadece PassengerManager'dan bilgileri okuyup ekrana yazar.
// Bu ayrım önemli: oyun kuralları bir yerde, görüntü başka bir yerde.
//
// Yerleşim (her bilgi kendi köşesinde, tek bir kalabalık panel yerine):
// - Sol üst: görev kartı (ne yapacağım, nereye, taksimetre)
// - Üst orta: süre (sadece yolcu taşırken)
// - Sağ üst: kasa
// - Sağ alt: hız
// - Orta: kısa mesajlar
public class GameHUD : MonoBehaviour
{
    [Header("Bağlantılar (Inspector'dan sürükle)")]
    public PassengerManager passengers;
    public TaxiController taxi;
    public TMP_Text taskText;          // Sol üstteki görev kartının yazısı
    public TMP_Text timerText;         // Üst ortadaki süre
    public TMP_Text moneyText;         // Sağ üstteki kasa
    public TMP_Text speedText;         // Sağ alttaki hız göstergesi
    public TMP_Text messageText;       // Ortada kısa süre görünen mesaj

    [Header("Renkler")]
    public Color pickupColor = new Color(0.3f, 1f, 0.3f);    // Yeşil
    public Color dropoffColor = new Color(1f, 0.55f, 0f);    // Turuncu
    public Color warningColor = new Color(1f, 0.25f, 0.25f); // Kırmızı
    public Color mutedColor = new Color(0.65f, 0.68f, 0.75f); // Soluk gri: etiketler için

    StringBuilder sb = new StringBuilder();  // Yazıyı parça parça birleştirmek için

    void Update()
    {
        if (passengers == null) return;

        UpdateTaskCard();
        UpdateTimer();
        UpdateMoney();
        UpdateSpeed();
        UpdateMessage();
    }

    void UpdateTaskCard()
    {
        if (taskText == null) return;
        sb.Clear();

        // 1. satır: görev ve yolcunun tipi
        string task = passengers.IsCarryingPassenger
            ? Colored("YOLCUYU BIRAK", dropoffColor)
            : Colored("YOLCUYU AL", pickupColor);
        PassengerType type = passengers.CurrentType;
        if (type != null) task += "   " + Colored("<b>" + type.displayName + "</b>", type.color);
        sb.AppendLine("<b>" + task + "</b>");

        // 2. satır: mesafe
        sb.Append(Label("Mesafe") + passengers.DistanceToTarget().ToString("0") + " m");

        // Yolcu taşırken: taksimetre, memnuniyet, hız sınırı
        if (passengers.IsCarryingPassenger)
        {
            sb.AppendLine();
            sb.AppendLine(Label("Taksimetre") + passengers.CurrentFare.ToString("0") + " TL");
            sb.Append(Label("Memnuniyet") + "%" + passengers.Satisfaction.ToString("0"));

            float limit = passengers.SpeedLimitKmh;
            if (limit > 0f)
            {
                string limitLine = Label("Hız sınırı") + limit.ToString("0") + " km/s";
                sb.AppendLine();
                sb.Append(passengers.IsOverSpeedLimit ? Colored(limitLine, warningColor) : limitLine);
            }
        }

        taskText.text = sb.ToString();
    }

    void UpdateTimer()
    {
        if (timerText == null) return;

        timerText.enabled = passengers.IsCarryingPassenger;
        if (!timerText.enabled) return;

        float t = passengers.TimeLeft;
        if (t >= 0f)
        {
            int s = Mathf.CeilToInt(t);
            timerText.text = (s / 60) + ":" + (s % 60).ToString("00");   // 1:05 gibi
            timerText.color = t < 10f ? warningColor : Color.white;
        }
        else
        {
            timerText.text = "+" + Mathf.CeilToInt(-t) + " sn geç";
            timerText.color = warningColor;
        }
    }

    void UpdateMoney()
    {
        if (moneyText == null) return;
        moneyText.text = Colored("<size=50%>KASA</size>", mutedColor) + "\n" +
                         passengers.TotalMoney.ToString("0") + " TL";
    }

    void UpdateSpeed()
    {
        if (speedText == null || taxi == null) return;
        speedText.text = taxi.SpeedKmh.ToString("0") + "<size=40%> km/s</size>";
        speedText.color = passengers.IsOverSpeedLimit ? warningColor : Color.white;  // Sınır aşılınca kırmızı
    }

    void UpdateMessage()
    {
        if (messageText == null) return;
        messageText.enabled = passengers.HasMessage;
        if (passengers.HasMessage) messageText.text = passengers.Message;
    }

    // "Mesafe  " gibi soluk renkli bir etiket
    string Label(string text)
    {
        return Colored(text, mutedColor) + "  ";
    }

    // TextMeshPro'nun "rich text" özelliği: <color=#RRGGBB>yazı</color>
    string Colored(string text, Color color)
    {
        return "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + text + "</color>";
    }
}
