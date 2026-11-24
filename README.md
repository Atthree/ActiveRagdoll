# Active Ragdoll

**Unity için fizik tabanlı, sallanan-yürüyen karakter kontrolcüsü** (Gang Beasts / Human Fall Flat tarzı).

> *Physics-driven active ragdoll character controller for Unity 6. Drop the included Buddy
> character into a scene, press Build, press Play. Docs are in Turkish.*

<p align="center"><img src="Documentation~/buddy.png" width="420" alt="Buddy"></p>

Karakter animasyonla değil, tamamen **fizikle** hareket eder: joint'ler pozu tutmaya çalışır,
denge kuvvetleri onu ayakta tutar, bir şey çarpınca devrilir ve kendi kendine kalkar.

- Hazır karakter **Buddy** pakete dahil
- Yürüme, dönme, zıplama
- Kolları uzatıp tutma: kutu taşıma, kenara asılma
- Devrilince / sert çarpışmada bayılma, sonra ayağa kalkma
- 3. şahıs kamera, klavye + gamepad

**Gereksinimler:** Unity 6 (6000.0+), Input System paketi.

---

## Adım adım kurulum

### 1. Paketi projeye ekle

1. Unity'de **Window > Package Manager**'ı aç.
2. Sol üstteki **+** düğmesine bas → **Install package from git URL…**
3. Şu adresi yapıştır ve **Install**'a bas:

   ```
   https://github.com/Atthree/ActiveRagdoll.git
   ```

Input System paketi yoksa otomatik kurulur. Unity *"Enable the new input backends?"* diye sorarsa
**Yes** de; editör yeniden başlar.

> Belirli bir sürümü kullanmak için adresin sonuna etiket ekle: `…/ActiveRagdoll.git#v0.1.0`

### 2. Sahneyi hazırla

1. **File > New Scene** → *Basic (URP)* veya *Standard* şablonu.
2. Zemin ekle: **GameObject > 3D Object > Plane**. Inspector'da **Scale**'i `5, 1, 5` yap.
   (Zeminin bir **Collider**'ı olmalı. Plane'de zaten var.)

### 3. Karakteri sahneye koy

1. **Project** penceresinde **Packages > Active Ragdoll > Characters** klasörünü aç.
2. **Buddy**'yi Hierarchy'ye ya da sahneye sürükle.
3. Inspector'da **Position** = `0, 0, 0`, **Rotation** = `0, 0, 0` olsun.
   Karakter dik durmalı ve ayakları zemine değmeli.

### 4. Ragdoll'u kur

1. Menüden **Tools > Active Ragdoll > Builder**'ı aç.
2. Hierarchy'deki **Buddy**'yi pencerenin **Karakter** alanına sürükle.
3. **Build**'e bas.

Builder şunları yapar:

- Her kemiğe Rigidbody, collider ve ConfigurableJoint ekler. Collider boyutları mesh'ten ölçülür.
- Buddy'nin üstüne `ActiveRagdoll`, `ActiveRagdollLocomotion`, `ActiveRagdollArms` ve
  `PlayerRagdollInput` bileşenlerini ekler.
- **Main Camera**'ya `RagdollCamera` ekleyip karakteri takip ettirir.

Hepsi **Ctrl+Z** ile geri alınabilir. Kurulumu silmek için pencerede **Remove** düğmesi var.

### 5. Oyna

**Play**'e bas.

| Klavye / Fare | Gamepad | İş |
|---|---|---|
| W A S D | Sol stick | Yürü (kameranın baktığı yöne göre) |
| Space | A | Zıpla |
| Sol tık (basılı tut) | Sol tetik | Sol kolu uzat, değdiğini tut |
| Sağ tık (basılı tut) | Sağ tetik | Sağ kolu uzat, değdiğini tut |
| Fare | Sağ stick | Kamerayı döndür |
| Esc | – | İmleci serbest bırak (oyun ekranına tıklayınca geri kilitlenir) |

Tutmayı denemek için sahneye birkaç küp koy ve onlara **Rigidbody** ekle.

---

## Ayarlar

Tüm ayarlar Buddy'yi seçince Inspector'da görünür. Play modundayken değiştirip sonucu anında
görebilirsin. Play'den çıkınca değişiklikler geri alınır; beğendiğin değerleri not et.

| Sorun / İstek | Bileşen → Alan |
|---|---|
| Daha hızlı / yavaş yürüsün | `ActiveRagdollLocomotion` → **Move Speed** |
| Daha yükseğe zıplasın | `ActiveRagdollLocomotion` → **Jump Speed** |
| Yürürken fazla öne eğiliyor | `ActiveRagdoll` → **Balance Spring** (artır) |
| Ayaklar yere sürtüyor | `ActiveRagdoll` → **Stand Height Offset** (0.02 → 0.05) |
| Çok kolay bayılıyor | `ActiveRagdoll` → **Knockout Impact Speed** (artır) veya **Auto Knockout** kapat |
| Daha gevşek / daha sert gövde | `ActiveRagdoll` → **Joint Spring** |
| Kollar tuttuğunu kaldıramıyor | `ActiveRagdollArms` → **Reach Strength** |
| Tutuş çok kolay kopuyor | El kemiklerindeki `RagdollHand` → **Break Force** |
| Kamera uzaklığı / hassasiyeti | Main Camera → `RagdollCamera` → **Distance**, **Mouse Sensitivity** |
| Titreme | **Edit > Project Settings > Time > Fixed Timestep** = `0.01` |

---

## Sorun giderme

- **Karakter Play'de hemen düşüyor / havada asılı kalıyor:** Zeminde collider olduğundan ve
  karakterin Play'den önce zemine bastığından emin ol (Position Y = zeminin yüksekliği).
- **Tuşlar çalışmıyor:** **Edit > Project Settings > Player > Active Input Handling** değeri
  *Input System Package (New)* ya da *Both* olmalı.
- **Builder "Bulunamayan kemikler" diyor:** Sahnedeki karakteri seçtiğinden emin ol (Project'teki
  asset'i değil). Kendi karakterini kullanıyorsan model **Humanoid** rig olarak import edilmeli.
- **Karakter pembe görünüyor:** Proje URP kullanıyorsa Buddy'yi seç ve materyalleri
  **Window > Rendering > Render Pipeline Converter** ile dönüştür.

---

## Nasıl çalışıyor?

```
PlayerRagdollInput ─┐         (ya da kendi yapay zeka scriptin)
                    ▼
            RagdollCommands   (hareket yönü, bakış yönü, zıpla, tut)
                    │
     ┌──────────────┼───────────────────┐
     ▼              ▼                   ▼
ActiveRagdoll   ActiveRagdollLocomotion  ActiveRagdollArms
 joint gücü,     yürüme kuvveti,          kol pozları,
 dik durma,      bacak salınımı,          RagdollHand (tutma)
 kalça yayı,     zıplama
 bayılma
     │
     ▼
RagdollBone  (her kemikte: Rigidbody + ConfigurableJoint)
```

- **Kalça yayı:** Kalça yerden belli bir yükseklikte bir yayla tutulur. Bacaklar ağırlık taşımaz,
  sadece görsel olarak adım atar. Human Fall Flat tarzı oyunlarda kullanılan sağlam yöntem budur.
- **Dik durma:** Kalçaya ve göğse, dik durup bakış yönüne dönmeleri için tork uygulanır (PD kontrolcü).
- **Bayılma:** Joint gücü sıfıra iner, karakter tam ragdoll olur. Süre bitince güç yavaşça geri
  gelir, denge ve kalça yayı karakteri ayağa kaldırır.

| Dosya | Görev |
|---|---|
| `Runtime/ActiveRagdoll.cs` | Joint gücü, denge torku, kalça yayı, yere basma, bayılma |
| `Runtime/ActiveRagdollLocomotion.cs` | Hareket kuvveti, dönme, zıplama, prosedürel bacak salınımı |
| `Runtime/ActiveRagdollArms.cs` | Kolları sarkıtma / sallama / uzatma |
| `Runtime/RagdollHand.cs` | Değdiği şeye FixedJoint ile yapışma |
| `Runtime/RagdollBone.cs` | Tek kemik: dinlenme pozuna göre hedef rotasyon |
| `Runtime/ConfigurableJointExtensions.cs` | `targetRotation`'ı joint uzayına çevirme |
| `Runtime/PlayerRagdollInput.cs` | Input System → `RagdollCommands` |
| `Runtime/RagdollCamera.cs` | 3. şahıs kamera |
| `Editor/RagdollBuilder.cs` | Kemik bulma ve otomatik kurulum |
| `Editor/SkinMeasure.cs` | Collider boyutlarını mesh'ten ölçme |

Koddan kontrol etmek istersen (`PlayerRagdollInput` yerine):

```csharp
var ragdoll = GetComponent<ActiveRagdoll>();
ragdoll.commands.move = Vector3.forward;   // dünya yönü, uzunluk 0..1
ragdoll.commands.jumpRequested = true;
ragdoll.Knockout(2f);                      // 2 saniye bayılt
```

---

## Buddy karakteri

Buddy, Blender'da scriptle üretildi (`Art~/make_buddy.py`). Kaynak dosya `Art~/Buddy.blend`.
Ölçüleri değiştirip yeniden üretmek için:

```
blender -b -P Art~/make_buddy.py -- Characters
```

## Lisans

[MIT](LICENSE.md)
