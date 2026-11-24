# Changelog

## [0.1.1] - 2026-11-24
- Fixed: the character leaned ~20° forward while walking. Leg colliders are now frictionless
  (`ActiveRagdoll.frictionlessLegs`), so feet brushing the ground no longer drag the body.
  Average walking lean 17° → 7°, walking speed now reaches Move Speed.
- Fixed: `Knockout()` on a character standing still didn't make it fall. The upper body now gets a
  small stumble push (`ActiveRagdoll.knockoutStumble`). New overload `Knockout(duration, push)`.
- Balance Spring default 600 → 1000.
- README: English first, then Turkish.

## [0.1.0] - 2026-11-24
- İlk sürüm.
- ActiveRagdoll (joint gücü, denge, kalça yayı, bayılma/kalkma), Locomotion (yürüme, zıplama,
  prosedürel bacaklar), Arms + RagdollHand (uzanma, tutma), PlayerRagdollInput, RagdollCamera.
- Editor: RagdollBuilder penceresi (Tools > Active Ragdoll > Builder), mesh'ten collider ölçümü.
- Hazır karakter: Buddy (Blender kaynağı Art~ içinde).
