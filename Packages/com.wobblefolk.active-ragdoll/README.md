# Wobblefolk Active Ragdoll

Animasyon klibi veya Animator kullanmadan, tamamen fizikle ayakta duran, yürüyen, koşan, zıplayan, kollarıyla
uzanıp tutan, eşya taşıyan ve kenarlara tırmanan bir **active ragdoll** karakter sistemi (Unity 6, PhysX).

- **Denge:** pelvis ve göğse PD (yay–sönüm) torku, zemin algılama, pelvisi ayakta durma yüksekliğinde tutan kaldırma kuvveti
- **Durumlar:** `Normal`, `Fallen` (düşmüş), `GettingUp` (kalkıyor), `Unconscious` (bayılmış); darbe ile düşme, kafaya darbe ile bayılma
- **Yürüme:** hıza bağlı prosedürel adım döngüsü (kalça / diz / bilek hedef açıları), koşma, öne eğilme, basamak çıkma, zıplama
- **Kollar:** sol/sağ bağımsız; basılı tutulan kol kameranın baktığı noktaya uzanır, aşağı uzanırken gövde eğilir
- **Tutma:** elin değdiği yüzeye kopabilir `ConfigurableJoint`; statik yüzeyler ve rigidbody'ler
- **Taşıma:** el sayısına göre kapasite, zorlanma (yavaşlama, zayıf denge, geriye yaslanma), aşırı yükte ellerin kayması
- **Tırmanma:** kenara asılma, yukarı çekilme ve kenarın üstüne geçme (basit versiyon)
- **Etkileşim:** hafif nesneleri itme (fizik), `Breakable` bileşeni
- **Araçlar:** Humanoid model için tek tıkla kurulum aracı, primitive örnek karakter, Playground örnek sahnesi
- **Tüm ayarlar** ScriptableObject profillerinde veya Inspector'da

---

## İçindekiler
1. [Kurulum](#kurulum)
2. [Hızlı başlangıç](#hızlı-başlangıç)
3. [Kontroller](#kontroller)
4. [Mimari](#mimari)
5. [Profiller](#profiller)
6. [Olaylar (UnityEvent)](#olaylar-unityevent)
7. [Koddan / NPC'den kullanım](#koddan--npcden-kullanım)
8. [Fizik ayarları ve titremeyi (jitter) önleme](#fizik-ayarları-ve-titremeyi-jitter-önleme)
9. [Neden ConfigurableJoint?](#neden-configurablejoint)
10. [Performans ve çoklu karakter](#performans-ve-çoklu-karakter)
11. [Humanoid kurulum aracı](#humanoid-kurulum-aracı)
12. [Bilinen sınırlamalar](#bilinen-sınırlamalar)

---

## Kurulum

**Gereksinimler:** Unity 6000.3+, `com.unity.inputsystem` 1.20+ (bağımlılık olarak gelir). Çekirdek kod render
pipeline'dan bağımsızdır; örnek sahnenin malzemeleri aktif pipeline'ın varsayılan shader'ını (bu projede URP Lit)
kullanır.

- **Bu repoda:** paket `Packages/com.wobblefolk.active-ragdoll` altında gömülü (embedded) olarak durur, ek bir şey gerekmez.
- **Başka projede:** klasörü o projenin `Packages/` klasörüne kopyalayın veya Package Manager > *Add package from disk* ile `package.json`'u seçin.

## Hızlı başlangıç

**A) Örnek sahne:** Package Manager > *Wobblefolk Active Ragdoll* > *Samples* > **Playground** > *Import*. Açılan
`Playground.unity` sahnesinde Play'e basın. Sahnede düz zemin, 15° rampa, merdiven, taşınacak kutular (2/8/20/40 kg)
ve variller, tırmanılacak 1.6 m'lik kenar, devrilen ve kırılan kuleler vardır.

**B) Kendi sahnenizde primitive karakter:** Hierarchy'de sağ tık > **Wobblefolk > Primitive Active Ragdoll (Player)**.
Karakter, kamera ve fizik ayarları tek seferde kurulur. Profiller yoksa `Assets/ActiveRagdoll/Profiles/` altında
oluşturulur. Sadece karakter (NPC) için: **Wobblefolk > Primitive Active Ragdoll**.

**C) Kendi Humanoid modeliniz:** **Tools > Wobblefolk > Build Active Ragdoll**. Ayrıntılar
[aşağıda](#humanoid-kurulum-aracı).

## Kontroller

| Eylem | Klavye / Fare | Gamepad |
|---|---|---|
| Yürü | WASD | Sol çubuk |
| Koş | Sol Shift | Sol çubuğa basma |
| Zıpla / kenara asılıyken tırman | Space | A (South) |
| Kamera | Fare | Sağ çubuk |
| Sol kol (uzan + tut) | Sol tık | Sol tetik |
| Sağ kol (uzan + tut) | Sağ tık | Sağ tetik |

Eylemler `RagdollPlayerInput` üzerinde Inspector'dan yeniden atanabilir ya da bir Input Actions asset'ine
bağlanabilir. Test için `RagdollDebugTools` bileşeninin ⋮ menüsünde **Push Forward / Push Sideways / Fall / Knock Out**
komutları vardır.

## Mimari

Her davranış ayrı bir bileşendir ve yalnızca `ActiveRagdoll` + `BalanceController` zorunludur. Diğerleri isteğe
bağlıdır; örneğin kolsuz bir NPC için `ArmController` hiç eklenmeyebilir.

```
ActiveRagdoll (kök)          kemik kaydı, profil uygulama, durum makinesi, olaylar
 ├─ BalanceController        dik durma (PD), ayakta durma yüksekliği, zemin, düşme/kalkma/bayılma
 ├─ LocomotionController     hareket kuvveti, adım döngüsü, eğilme, basamak, zıplama
 ├─ ArmController            uzanma, eğilme, tutma/bırakma, kopma
 ├─ ClimbController          asılma, yukarı çekilme, kenarın üstüne geçme
 ├─ CarryController          taşınan kütle, zorlanma, aşırı yükte kayma
 ├─ RagdollPlayerInput       Input System sürücüsü (ayrı asmdef; NPC'lerde gerekmez)
 └─ RagdollDebugTools        test komutları
Kemikler: RagdollBone (Rigidbody + ConfigurableJoint sarmalayıcı) × 15
Sahne:    RagdollPhysicsSettings (fizik adımı, solver), RagdollFollowCamera (kamerada)
```

**Çalışma sırası (FixedUpdate):** `ActiveRagdoll` (-100) → `BalanceController` (0) → `LocomotionController` (10) →
`ArmController` (20) → `CarryController` (30) → `ClimbController` (40). Diğer sistemler dengeyi doğrudan değiştirmez;
`BalanceController.SetModifier(kaynak, dengeAğırlığı, kaldırmaAğırlığı, ekKütle)` ile istekte bulunur ve tüm
istekler birleştirilir.

**Kemikler:** Pelvis, Chest, Head, (Upper/Lower)Arm, Hand, (Upper/Lower)Leg, Foot × sol/sağ = 15 kemik. Her eklem
uzayında **X ekseni = karakterin sağı**, **Y ekseni = karakterin yukarısı**. Pozitif X dönüşü aşağı sarkan uzuvları
geriye, göğüs ve kafayı öne büker. Bu sabit kural sayesinde aynı profiller farklı riglerde de çalışır.

## Profiller

Tüm sayısal ayarlar ScriptableObject profillerindedir (*Create > Wobblefolk > …*). Play modunda değiştirilen
değerler anında etkili olur (Unity bunları asset'e kaydeder).

| Profil | İçerik |
|---|---|
| `RagdollProfile` | Kemik başına kütle, eklem eksenleri, açı limitleri, kas yayı / sönümü / **azami torku**; solver iterasyonları, damping, interpolasyon, projeksiyon, kendi kendine çarpışma |
| `BalanceProfile` | Dik durma ve yön PD kazançları, ayakta durma yüksekliği ve yay/sönüm, zemin algılama, düşme açısı, darbe eşikleri, kalkma süresi ve eğrisi, bayılma |
| `LocomotionProfile` | Yürüme/koşma hızı, ivme/fren, havada kontrol, dönüş hızı, eğilme, adım uzunluğu ve tempo, kalça/diz/bilek açıları, kol sallama, ayak sürtünmesi, basamak, zıplama (coyote time, tampon) |
| `ArmProfile` | Uzanma gücü, nişan, öne eğilme, tutma yarıçapı, kopma kuvveti/torku, dönme kilidi, tutulan cismin fizik ayarları |
| `CarryProfile` | El başına rahat ve azami kütle, sürükleme katsayısı, zorlanma eğrisi, hız ve denge cezası, geriye yaslanma, aşırı yükte kayma |
| `ClimbProfile` | Tırmanılabilir tutamak kuralları, kenar arama, asılma, yukarı çekilme ve kenara geçme kuvvetleri, zaman aşımları |

## Olaylar (UnityEvent)

| Bileşen | Olaylar |
|---|---|
| `ActiveRagdoll` | `stateChanged(önceki, yeni)`, `fell`, `gettingUp`, `recovered`, `knockedOut`, `wokeUp` |
| `ArmController` | `grabbed(taraf, collider)`, `released(taraf, collider, koptuMu)` |
| `CarryController` | `strainChanged(0..1)`, `overloadSlipped` |
| `ClimbController` | `startedHanging`, `startedClimbing`, `finishedClimbing`, `failedClimbing` |
| `Breakable` | `broken(nokta, darbeHızı)` |

Kod tarafında aynı olayların C# karşılıkları da vardır: `ActiveRagdoll.StateChanged`, `ActiveRagdoll.BoneCollided`,
`ArmController.Grabbed`, `ArmController.Released`.

## Koddan / NPC'den kullanım

`RagdollPlayerInput` yalnızca bir sürücüdür; bir yapay zekâ aynı API'yi doğrudan kullanabilir:

```csharp
locomotion.MoveInput = (hedef - transform.position).normalized; // dünya yönü, büyüklük 0..1
locomotion.Sprint = true;
locomotion.RequestJump();

arms.HasAim = true;
arms.AimRay = new Ray(gozPozisyonu, (tutulacakNokta - gozPozisyonu).normalized);
arms.SetReach(BodySide.Right, true);           // uzan ve değdiğini tut
if (arms.IsHolding(BodySide.Right)) { /* ... */ }

climb.RequestClimb();                          // kenara asılıyken
balance.Knockout(3f);                          // 3 sn bayılt
balance.Fall();                                // yere düşür
```

## Fizik ayarları ve titremeyi (jitter) önleme

Active ragdoll'da en büyük sorun titremedir. Aşağıdaki değerler bu paketle test edilmiştir. Proje ayarlarını
değiştirmek istemiyorsanız sahneye `RagdollPhysicsSettings` ekleyin; ilk dört satırı oyun başlarken uygular.

| Ayar | Önerilen | Neden |
|---|---|---|
| Fixed Timestep | **0.01** (100 Hz) | Varsayılan 0.02'de uzun eklem zincirleri titrer ve yaylı kaslar kararsızlaşır |
| Maximum Allowed Timestep | 0.05 | Kare takılmasından sonra fizik adımlarının birikip "ölüm sarmalına" girmesini önler |
| Solver Iterations / Velocity Iterations | **12 / 4** (kemik başına, `RagdollProfile`) | Eklemler daha az esner, temaslar daha kararlı olur |
| Rigidbody Interpolation | **Interpolate** (görünen tüm kemiklerde) | 100 Hz fizik ile ekran kare hızı arasındaki farkı yumuşatır |
| Collision Detection | Continuous Speculative | Hızlı uzuvların ince nesnelerden geçmesini ucuza önler |
| Max Angular Velocity | 30 rad/s | Kararsız bir karede kemiklerin fırıl fırıl dönmesini sınırlar |
| Max Depenetration Velocity | 5 m/s kemikler, **2 m/s tutulan cisim** | Unity varsayılanı sınırsızdır; iç içe geçen cisimler birbirini fırlatır |
| Joint Projection | Açık, 0.05 m / 10° | Uzuvların birbirinden kopup uzamasını engeller |
| Enable Preprocessing | **Kapalı** | Uzun zincirlerde ve sert limitlerde titremeyi azaltır |
| Kas azami torku (`maxForce`) | İnsan ölçeğinde (kol 140, kalça 450 N·m) | Sınırsız kas, dokunduğu nesneyi fırlatan bir koç başına dönüşür |
| Kas sönümü | Kritik sönüme yakın, `damper ≈ 2·√(yay·atalet)` | Az sönüm salınım, fazla sönüm "su içinde" hissi verir |
| Bağlı gövdeler arası kütle oranı | 10:1'in altında | PhysX solver büyük oranlarda kararsızlaşır |

Kodda uyulan kurallar:
- **Hız asla doğrudan atanmaz** (`linearVelocity = …` yok); her hareket sınırlı bir kuvvet ya da ivme ile yapılır.
- **Tutulan cisimler algılayıcılarda yok sayılır** (zemin, basamak, nişan, kamera, darbe). Aksi halde karakter
  elinde tuttuğu kutunun "üstünde durup" kendini sonsuza kadar yukarı kaldırır.
- **Tutma eklemi transform pozuyla kurulur.** Unity, eklem eklenirken gövdeyi transform'una senkronize eder.
  Interpolasyonlu gövdelerde rigidbody pozuyla hesaplanan bağlantı noktası farklı çıkar ve eklem ilk adımda cismi fırlatır.
- **Kütle ölçekleme (`massScale`) varsayılan olarak kapalıdır;** momentumu korumadığı için ağır cisimleri yapay
  biçimde hafifletir. Gerekirse `ArmProfile > Max Solver Mass Ratio` ile açılabilir.

## Neden ConfigurableJoint?

| | ConfigurableJoint | CharacterJoint | HingeJoint |
|---|---|---|---|
| Serbestlik | 6 eksen, her biri ayrı kilitlenir veya sınırlanır | Asimetrik twist + simetrik iki swing | Tek eksen |
| Hedefe sürme (kas) | **Var** (`targetRotation` + slerp drive) | Yok (sadece limitler) | Motor / yay, tek eksende |
| Çalışma zamanında güç değişimi | Drive yay/sönüm/azami kuvvet | Yok | Sınırlı |
| Projeksiyon | Var | Var | Yok |
| Kurulum karmaşıklığı | Yüksek (eksen ve hedef matematiği) | Düşük | Düşük |
| Uygun olduğu yer | **Active ragdoll** | Pasif (ölü) ragdoll | Kapı, menteşe, diz gibi tek eksenli basit eklemler |

**Seçim:** Active ragdoll'un özü, her eklemi kasla hedef bir açıya sürmek ve bu kasın gücünü durumlara göre
değiştirmektir (düşünce gevşeme, uzanırken sertleşme). Bunu yalnızca `ConfigurableJoint` sağlar. Diz gibi tek
eksenli eklemler de aynı tipte tutuldu; böylece tüm kemikler tek bir kodla sürülür ve profil formatı tektir.
Karmaşık eksen matematiği `RagdollBone` içinde saklanır: `SetTargetJointRotation`, `SetTargetLocalRotation`,
`SetTargetWorldRotation`.

## Performans ve çoklu karakter

Karakter başına 15 rigidbody, 14 eklem (+ tutarken el başına 1) bulunur. FixedUpdate'te çalışan sorgular:

- **Zemin:** pelvisten 1 küre taraması, her ayaktan 1 ışın
- **Hareket halindeyken:** basamak için 2 ışın
- **Uzanırken:** nişan için 1 ışın, uzanan el başına 1 çakışma sorgusu
- **Taşırken:** tutulan cisim başına 1 ışın

Hiçbir bileşen her karede bellek ayırmaz; tüm sorgular `NonAlloc` sürümleri ve önceden ayrılmış dizilerle yapılır.

Çok sayıda karakter (NPC, multiplayer) için:
- **Asıl maliyet solver'dır.** Uzaktaki karakterler için `RagdollProfile > Solver Iterations` 6–8 / 2 yeterlidir. Ayrı bir "NPC" profili kullanın.
- **Kendi kendine çarpışma kapatma:** Her karakterin `Awake`'inde kendi collider çiftleri için `Physics.IgnoreCollision` çağrılır (15 kemik → 105 çift, tek seferlik). Yüzlerce karakter aynı anda doğuyorsa bunun yerine ragdoll'lara özel bir katman açıp Layer Collision Matrix'te o katmanın kendisiyle çarpışmasını kapatmak daha ucuzdur. Bu durumda ragdoll'lar birbirine de çarpmaz.
- **Uykuya geçme:** Duran karakterler kas kuvvetleri yüzünden uykuya geçmez. Görüş dışındaki NPC'leri devre dışı bırakmak (`SetActive(false)`) en büyük kazancı sağlar.
- **Multiplayer:** Fizik deterministik değildir. Karakteri sunucu veya sahibi simüle etmeli, diğer istemciler kemik pozlarını interpolasyonla almalıdır. Girdi API'si (`MoveInput`, `SetReach`, `AimRay`) ağ üzerinden kolayca taşınabilecek kadar küçüktür.

## Humanoid kurulum aracı

**Tools > Wobblefolk > Build Active Ragdoll**

1. Animation Type'ı **Humanoid** olan bir modeli sahneye koyun ve pencereye sürükleyin.
2. Profilleri seçin (varsayılanlar otomatik bulunur veya oluşturulur), collider oranlarını gerekirse değiştirin.
3. **Build Active Ragdoll**'a basın.

Araç şunları yapar:
- Avatar kemiklerini 15 ragdoll kemiğine eşler. Chest yoksa Spine kullanılır. Spine, Neck ve omuz gibi ara kemikler simüle edilmez; bağlı oldukları kemikle birlikte hareket eder.
- T-pozundaki kolları dinlenme pozuna indirir (*Arms Down Angle*). Profiller bu poza göre ayarlıdır.
- Kemik uzunluklarından boyutlanan collider'ları `RagdollCollider` adlı alt nesnelere koyar; böylece rigin kemik eksenleri ne olursa olsun collider'lar uzva hizalı olur.
- Eklem eksenlerini karakterin kendi sağ/yukarı yönünden hesaplar.
- Modelin pelvis yüksekliği `BalanceProfile`'daki ayakta durma yüksekliğinden belirgin şekilde farklıysa, bu model için profilin bir kopyasını oluşturur.
- Animator'ü kapatır; avatar için bileşen yerinde kalır.
- İsteğe bağlı olarak oyuncu girdisi, takip kamerası ve debug araçlarını ekler.

Aynı pencerede **Remove Active Ragdoll** eklenen her şeyi kaldırır ve Animator'ü tekrar açar. Ctrl+Z de çalışır.
Araç, eksenleri kasıtlı olarak tutarsız döndürülmüş bir Mixamo düzeninde iskelet üzerinde test edilmiştir: ayakta
durma, yürüme, uzanma ve taşıma çalışmıştır.

## Bilinen sınırlamalar

- **Tırmanma basittir.** Yukarı çekilme gövdeye uygulanan bir kuvvetle yapılır, kollar sadece tutar. Tutamak ya kinematik ya da 200 kg üstü bir cisim olmalıdır. Kenarın tutuştan en fazla 0.9 m yukarıda olması gerekir.
- **Taşırken kutu sallanır.** Yürürken taşınan kutu adımlarla birlikte bir miktar sallanır; dururken sakinleşir.
- **Kollar dinlenme pozunu korur.** Uzanmayan kollar dinlenme pozunda kalır, yürürken sallanır.
- **Breakable parçalanmayı kendisi yapmaz.** Olay fırlatır; isteğe bağlı olarak `brokenPrefab` ile hazır parçalanmış bir modeli yerine koyar.
- **Gerçek modelle test edilmedi.** Humanoid araç, gerçek bir FBX karakterle değil, kodla üretilmiş bir Humanoid iskeletle test edildi. Farklı oranlardaki modellerde collider oranlarının ayarlanması gerekebilir.
