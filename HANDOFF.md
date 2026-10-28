# HANDOFF — Vigil: Clover redesign audit
Updated: 2026-10-28 (America/Montreal)

## Where we are
The Clover redesign (visible name "Clover", see `docs/design/CLOVER.md`) was archived as one commit on
`archive/local-2026-10-28` and audited with `uno-audit`. Findings are in `docs/AUDIT-2026-10-27.md`.
Only one fix was applied: the WASM crypto advisories (`WindowsCompatibilityVersion` 10.0.14), on this branch
and on `main` (98c2ca4). `main` does not contain Clover. The repo now lives at `github.com/noknowscode/Vigil`.

## Last verified state
- Build: desktop 0 warnings, WASM 0 warnings; Android/iOS not built. Tests 68/68 (this branch), 50/50 (`main`).
- Runtime: not verified (no App MCP pass on Clover in this session).
- Git: this branch; `main` at 98c2ca4 pushed.
- Lint: gating 134 (CARD 21 · HEX 74 · BACKBAR 3 · CODEBEHIND 12 · OVERLAY 1 · RESPONSIVE 4 · ICON 19), none allowed.

## Guidance applied
| Skill / rule / decision | Applied at (file:line) |
|---|---|
| Uno.Sdk documented override for an implicit package version | `Vigil/Vigil.csproj:31` |
Read, not applied: uno-platform-agent, mvux, uno-navigation, uno-material, uno-toolkit, winui-xaml, dotnet-csharp (audit only).

## Next actions (in order)
1. Runtime pass (`uno-verify`): dark theme on board and record hero, French on record and parent preview, ruler rebuild rate.
2. Fix batch 1 from the audit: French strings, dark-theme inks.
3. Batch 2: remove dead `DoseModel`/`DosePage` or share one dose save path; test the drawer dose path with the `store-write` fault.
4. Batches 3 and 4: board layout out of code-behind; cards, hex, font sizes to Toolkit, tokens and type styles.

## Open questions
- Ship a serif font file, or accept the Georgia fallback off Windows?
- Should ParentPreview be localized now?
- Merge Clover to `main` at all, or keep it archived?
- 134 lint hits carry no `allow` reasons.

## Relaunch
```
git clone https://github.com/noknowscode/Vigil && cd Vigil && git checkout archive/local-2026-10-28
dotnet build Vigil/Vigil.csproj -p:VigilTargetFrameworks=net10.0-desktop ; dotnet test Vigil.Tests/Vigil.Tests.csproj
uno_app_start  projectPath=Vigil/Vigil.csproj  tfm=net10.0-desktop  args=--clover-preview
Strings: python tools/localize.py --write   (fails on missing French)
```
