using UnityEngine;
using TMPro;
using System.Text;

// Ekrandaki oyun arayüzü (HUD = Heads-Up Display).
// Oyunun mantığını bilmez, sadece PassengerManager'dan bilgileri okuyup ekrana yazar.
// Bu ayrım önemli: oyun kuralları bir yerde, görüntü başka bir yerde.
public class GameHUD : MonoBehaviour
{
    [Header("Bağlantılar (Inspector'dan sürükle)")]
    public PassengerManager passengers;
    public TaxiController taxi;
    public TMP_Text taskText;          // Sağ üstteki görev panelinin yazısı
    public TMP_Text speedText;         // Sağ alttaki hız göstergesi
    public TMP_Text messageText;       // Ortada kısa süre görünen mesaj

    [Header("Renkler")]
    public Color pickupColor = new Color(0.3f, 1f, 0.3f);    // Yeşil
    public Color dropoffColor = new Color(1f, 0.55f, 0f);    // Turuncu
    public Color warningColor = new Color(1f, 0.25f, 0.25f); // Kırmızı

    StringBuilder sb = new StringBuilder();  // Yazıyı parça parça birleştirmek için

    void Update()
    {
        if (passengers == null || taskText == null) return;

        sb.Clear();

        // 1. satır: görev (rengi göreve göre) ve yolcunun tipi
        string task = passengers.IsCarryingPassenger
            ? Colored("YOLCUYU BIRAK", dropoffColor)
            : Colored("YOLCUYU AL", pickupColor);
        PassengerType type = passengers.CurrentType;
        if (type != null) task += "  " + Colored("<b>" + type.displayName + "</b>", type.color);
        sb.AppendLine(task);

        sb.AppendLine("Mesafe: " + passengers.DistanceToTarget().ToString("0") + " m");

        // Sadece yolcu taşırken görünen satırlar
        if (passengers.IsCarryingPassenger)
        {
            float t = passengers.TimeLeft;
            if (t >= 0f)
            {
                string timeLine = "Süre: " + t.ToString("0") + " sn";
                sb.AppendLine(t < 10f ? Colored(timeLine, warningColor) : timeLine);
            }
            else
            {
                sb.AppendLine(Colored("GECİKTİN! " + (-t).ToString("0") + " sn", warningColor));
            }

            float limit = passengers.SpeedLimitKmh;
            if (limit > 0f)
            {
                string limitLine = "Hız sınırı: " + limit.ToString("0") + " km/s";
                sb.AppendLine(passengers.IsOverSpeedLimit ? Colored(limitLine, warningColor) : limitLine);
            }

            sb.AppendLine("Taksimetre: " + passengers.CurrentFare.ToString("0") + " TL");
            sb.AppendLine("Gidilen yol: " + (passengers.MeterDistance / 1000f).ToString("0.00") + " km");
            sb.AppendLine("Memnuniyet: %" + passengers.Satisfaction.ToString("0"));
        }

        sb.AppendLine();
        sb.AppendLine("<b>Kasa: " + passengers.TotalMoney.ToString("0") + " TL</b>");
        sb.Append("Yolculuk: " + passengers.CompletedRides);

        taskText.text = sb.ToString();

        // Hız göstergesi: büyük sayı, yanında küçük birim
        if (speedText != null && taxi != null)
        {
            speedText.text = taxi.SpeedKmh.ToString("0") + "<size=40%> km/s</size>";
            speedText.color = passengers.IsOverSpeedLimit ? warningColor : Color.white;  // Sınır aşılınca kırmızı
        }

        // Mesaj: sadece süresi dolmamışken görünür
        if (messageText != null)
        {
            messageText.enabled = passengers.HasMessage;
            if (passengers.HasMessage) messageText.text = passengers.Message;
        }
    }

    // TextMeshPro'nun "rich text" özelliği: <color=#RRGGBB>yazı</color>
    string Colored(string text, Color color)
    {
        return "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + text + "</color>";
    }
}
