# GNN Tabanlı Çığ Risk Puanlaması

Sınav sahnesinde kullanıcının durduğu noktanın **güvenli** (kaya arkası sığınak), **açık** veya
**tehlikeli** (ağaç dibi) olduğunu eğitilmiş bir **GraphSAGE** modeli belirler. Tree ve sığınak trigger
kutuları artık puanlamada kullanılmıyor, yalnızca eğitim etiketi olarak işe yarıyor (GNN puanlayıcı yoksa
veya model yüklenemezse eski bonus / ceza puanlaması otomatik devreye girer).

## Akış

1. **Graf** (`Assets/_Scripts/GNN/GnnGraphBuilder.cs`): Yamaç 0.5 m'lik hücrelere bölünür (sahne ölçeği 6.44).
   Yalnızca Terrain yüzeyi zemin sayılır; kaya / ağaç içindeki hücreler düğüm olmaz. Yürünebilir eğimdeki,
   arasında engel olmayan 8 komşu hücre kenarla bağlanır. Alan, ağaç / sığınak kutuları ve oyuncunun çevresi
   10 m genişletilerek bulunur (`GnnRiskPredictor > Site Area` ile elle verilebilir).
2. **Düğüm özellikleri** (8 adet, yalnızca geometriden):
   - arazi eğimi, en yakın ağaca ve büyük kayaya uzaklık (obje adlarından: `spruce`, `populus`, `stone` …)
   - **sığınak**: çığın geldiği yöne atılan 5 ışından kaçının bir kayaya çarptığı (kaya arkası = 1)
   - çığ yoluna yatay uzaklık (çığ topunun başlangıcı + Collider1..4 kutuları)
   - yerel yükselti (sırt / çukur), çevre açıklığı, baş üstü açıklık
3. **Etiket**: Tree tag'li trigger'ların içi `Danger`, SafeAreaController trigger'larının içi `Safe`, kalan yamaç `Exposed`.
4. **Eğitim** (`GNN/train_gnn.py`): 2 katmanlı GraphSAGE (mean aggregator) + softmax.
   Veri 2 m'lik uzamsal bloklara göre bölünür, sınıf dengesizliği için ağırlıklı cross-entropy kullanılır.
5. **Çalışma zamanı**: Yamaç sabit olduğu için graf Editor'de bir kez çıkarılıp
   `Assets/Resources/GNN/gnn_graph_<sahne>.json` olarak kaydedilir. Oyunda sahne taranmaz;
   `GnnRiskPredictor` modeli sahne açılınca **bir kez** çalıştırır.

## Puanlama (`GnnRiskScorer`)

Puan 100'den başlar, üç kalemden düşer. Çığ kullanıcıya ulaşınca (Collider4) 3 sn'lik pencere sonunda
puan **kesinleşir**, puanlama kapanır ve standart risk etiketi gösterilir
(75+ Güvenli, 50–74 Orta Riskli, 25–49 Riskli, 25 altı Yüksek Riskli).

| Kalem | Ceza |
|---|---|
| Çığ anında konum | `50 × P(Açık) + 60 × P(Tehlikeli)` (kaya arkası ≈ 0) |
| Korunma pozisyonu | Çığ anından itibaren 3 sn içinde iki el başta değilse 30 |
| Ağaç altında bekleme (çığ gelene kadar) | `30 × [0.4 × w_giriş + 0.6 × (1 − e^(−E / 5 s))]`, en fazla 30 |

| Senaryo | Puan | Etiket |
|---|---|---|
| Kaya arkasında, eller başta | 100 | Güvenli |
| Kayaya giderken ağaca 1 sn girdi | 85 | Güvenli |
| Son anda kayaya vardı (ağaçlar arasından 3 sn) | 80 | Güvenli |
| Kaya arkasında, eller başta değil | 70 | Orta Riskli |
| Kaya arkasında ama önce ağaç altında uzun bekledi | 70 | Orta Riskli |
| Açık alanda yakalandı, eller başta | 50 | Orta Riskli |
| Kaya arkasında, eller başta değil, ağaç altında uzun | 40 | Riskli |
| Açık alanda yakalandı, eller başta değil | 20 | Yüksek Riskli |
| Ağaç dibinde yakalandı, eller başta | 10 | Yüksek Riskli |
| Ağaç dibinde yakalandı, eller başta değil | 0 | Yüksek Riskli |

(Model tam emin varsayımıyla; bölge sınırlarında cezalar olasılıkla ağırlıklı çıkar.)

## Kurulum

```bash
# 0) Unity: StoneBig 1 sığınağına trigger BoxCollider ekle (StoneBig 6'daki gibi)
#    yoksa o kaya sığınak olarak etiketlenemez
# 1) Unity: VR-ASYS-CIG-Sinav sahnesini aç
#    Hierarchy > Create Empty > adı "GNN" > Add Component: GnnRiskPredictor ve GnnRiskScorer > sahneyi kaydet
#    Tools > GNN > Eğitim Verisini ve Grafı Dışa Aktar (Açık Sahne)
#      → GNN/data/VR-ASYS-CIG-Sinav_graph_{0..3}.json (eğitim)
#      → Assets/Resources/GNN/gnn_graph_VR-ASYS-CIG-Sinav.json (oyun)

# 2) Eğit
python3 -m venv GNN/.venv && source GNN/.venv/bin/activate
pip install -r GNN/requirements.txt
python3 GNN/train_gnn.py            # --onnx ile ONNX da üretir

# 3) Assets/Resources/GNN/gnn_weights.json oluşur → Unity otomatik yükler
```

Görsel kontrol için sahnedeki `GnnRiskPredictor` bileşenine sağ tık:
- **Grafı kur ve etiketleri göster**: eğitim etiketleri (yeşil / sarı / kırmızı)
- **Kayıtlı grafla modeli çalıştır**: model kararı (yeşil Güvenli, sarı Açık, kırmızı Tehlikeli)

Bu görüntüleri görmek için objenin seçili olması gerekir (gizmo).

Metrikler `GNN/report/metrics.json` dosyasına yazılır:
- 5-katlı uzamsal çapraz doğrulama (ortalama ± std)
- Sınıf bazlı P/R/F1 ve karışıklık matrisi
- 1-komşu toleranslı metrikler (etiket kutularının kenar hatalarını ayırmak için)

## Bu dalda düzeltilen hatalar

- Çığ anında kullanıcının konumu hiç puanlanmıyordu (açıkta yakalanmanın cezası yoktu).
- Sığınak (+40) ve el (+30) bonusları herhangi bir anda bir kez veriliyordu; çığ gelmeden sığınaktan çıkan da puanı koruyordu.
- Ağaç cezası (−20) her girişte sınırsız ve senaryo bittikten sonra da işliyordu.
- Statik durumlar (`isPlayerInside`, `isTreeNear`, `isAvalancheNear`, `IsAducationSceneFinished`) sahne yeniden başlatılınca sıfırlanmıyordu.
- `IsAducationSceneFinished` hiç true yapılmıyordu; senaryo bitince puan kesinleşmiyor, sonuç gösterilmiyordu.
- Eller kayaya girmeden önce baştaysa korunma pozisyonu sayılmıyordu.

Unity'de yapılması gereken: **StoneBig 1** kayasında SafeAreaController var ama trigger collider yok (iki sahnede de).

> Grid ayarları (`GnnGraphSettings`) veya özellik kodu değişirse veriyi yeniden dışa aktarıp
> modeli yeniden eğitin. Model, özellik sırası uyuşmazsa yüklenmeyi reddeder.
