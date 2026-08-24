# Home story guides

`home_story_guides.tscn` is the single production entry point. Drop it once
into the home scene. It auto-finds Player, Workbench, Store and their UIs.

- F1 activates only when `f1_2/enemy_rocky_f1_2.defeated` is true. Mousy is
  placed next to Rubber, starts the existing `home鼠鼠.dtl` immediately, then
  the real workbench is highlighted. Mousy is retired after that home visit.
- F2 activates only when `f2_4/enemy_sharkk_f2_4.defeated` is true. The guide
  leads to the store, starts `Sharkk战后鼠鼠.dtl` on approach, then exposes the
  real store click area.
- Both guides persist under `__story_guides__`, survive save/load, and never
  infer F1 completion from player level 1 (new games also start at level 1).

All target IDs, text, Mousy scenes, offsets and guide behaviour are editable in
the Inspector on the packaged scene.
