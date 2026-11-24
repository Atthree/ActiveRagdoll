# Active Ragdoll (Unity)

Gang Beasts / Human Fall Flat tarzı, **tamamen fizikle hareket eden** karakter kontrolcüsü.
Herhangi bir **Humanoid** karaktere tek tıkla kurulur. Animasyon dosyası gerekmez.

- ConfigurableJoint tabanlı aktif ragdoll (poz tutan joint yayları)
- Dik durma (PD denge torku) + kalça yayı ile ayakta durma
- Prosedürel yürüme (sinüs tabanlı bacak/kol salınımı), dönme, zıplama
- Kolları bakış yönüne uzatma ve tutma (kutu taşıma, kenara asılma)
- Sert çarpışmada / devrilince bayılma ve kendiliğinden kalkma
- Basit yörünge kamerası, Input System ile klavye + gamepad

Gereksinim: Unity 6000.0+, Input System.

## Kurulum

**Package Manager > + > Install package from git URL…**

```
https://github.com/Atthree/ActiveRagdoll.git
```

veya `Packages/manifest.json`:

```json
"com.atthree.active-ragdoll": "https://github.com/Atthree/ActiveRagdoll.git#v0.1.0"
```

Geliştirirken yerel klasörden bağlamak için: `"file:../../ActiveRagdoll"`.

## Hızlı başlangıç

1. Karakteri sahneye koy (ayakları y=0'da, dik). Zemine collider ekle.
2. **Tools > Active Ragdoll > Builder** → karakteri sürükle → **Build**.
3. Play.

| Tuş | Gamepad | İş |
|---|---|---|
| WASD | Sol stick | Yürü (kameraya göre) |
| Space | A | Zıpla |
| Sol tık | Sol tetik | Sol kolu uzat + tut |
| Sağ tık | Sağ tetik | Sağ kolu uzat + tut |
| Fare | Sağ stick | Kamera |
| Esc | – | İmleci serbest bırak |

## Mimari

```
PlayerRagdollInput ─┐            (veya kendi AI scriptin)
                    ▼
            RagdollCommands  (move, lookDirection, jump, grab)
                    │
     ┌──────────────┼──────────────────┐
     ▼              ▼                  ▼
ActiveRagdoll  ActiveRagdollLocomotion  ActiveRagdollArms
 güç/denge/     yürüme kuvveti,         kol pozları,
 yükseklik/     bacak animasyonu,       RagdollHand (tutma)
 bayılma        zıplama
     │
     ▼
RagdollBone (her kemikte: Rigidbody + ConfigurableJoint)
```

| Script | Görev |
|---|---|
| `ActiveRagdoll` | Joint güçleri, dik durma torku, kalça yayı, yere basma kontrolü, bayılma/kalkma |
| `ActiveRagdollLocomotion` | Kalçaya yatay kuvvet, dönme, zıplama, prosedürel bacak salınımı |
| `ActiveRagdollArms` | Kolları boşta sarkıtır/sallar, tutma tuşunda bakış yönüne uzatır |
| `RagdollHand` | Değdiği şeye FixedJoint ile yapışır |
| `RagdollBone` | Tek kemik; dinlenme pozuna göre hedef rotasyon verme |
| `ConfigurableJointExtensions` | `targetRotation`'ı joint uzayına çeviren yardımcı |
| `PlayerRagdollInput` | Input System → `RagdollCommands` |
| `RagdollCamera` | 3. şahıs yörünge kamerası |
| `RagdollBuilder` (Editor) | Kemik bulma, Rigidbody/Collider/Joint kurulumu |

### Kendi kontrolünü yazmak (ör. yapay zeka)

`PlayerRagdollInput`'u ekleme, `ActiveRagdoll.commands`'ı kendin doldur:

```csharp
var ragdoll = GetComponent<ActiveRagdoll>();
ragdoll.commands.move = (target.position - ragdoll.Hips.transform.position).normalized;
ragdoll.commands.jumpRequested = true;
ragdoll.Knockout(2f); // 2 saniye bayılt
```

## Ayar ipuçları

- **Karakter öne/arkaya yatıyor** → `ActiveRagdoll.balanceSpring` artır.
- **Ayaklar yere sürtüyor** → `standHeightOffset` artır (0.02–0.05).
- **Titreme** → `jointDamper` / `balanceDamper` artır, ya da `Time > Fixed Timestep` = 0.01.
- **Çok kolay bayılıyor** → `knockoutImpactSpeed` artır veya `autoKnockout` kapat.
- **Kollar çok güçsüz/güçlü** → `ActiveRagdollArms.reachStrength`.

## Lisans

MIT
