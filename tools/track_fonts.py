"""
Tracked font copies for Vigil.

The prototype sets negative letter-spacing on its big numbers and serif titles (docs/design/shell-and-tokens.md, type
ramp). Uno's Skia renderer ignores TextBlock.CharacterSpacing, so each tracked role gets a copy of its font with every
advance width reduced by the tracking (CSS letter-spacing adds the same amount after each glyph). Outlines, kerning
and vertical metrics are unchanged. Copies are renamed ("Vigil ... T50") so they never pass for the original family
(SIL OFL: modified versions take a new name).

Writes, per variant, the font file, a base copy and a .ttf.manifest that maps weights 400-600 to it (the same layout as
the original fonts, so the type ramp's FontWeight never picks a synthetic face).

Usage: python -I tools/track_fonts.py
"""
import json
import shutil
from pathlib import Path

from fontTools.ttLib import TTFont

FONTS = Path(__file__).resolve().parent.parent / 'Vigil' / 'Assets' / 'Fonts'

# (source file, base name of the copy, family name, tracking in 1/1000 em)
VARIANTS = [
    ('MartianMono_Medium.ttf', 'MartianMonoT50', 'Vigil Mono T50', -50),
    ('MartianMono_Medium.ttf', 'MartianMonoT30', 'Vigil Mono T30', -30),
    ('MartianMono_Medium.ttf', 'MartianMonoT20', 'Vigil Mono T20', -20),
    ('Newsreader_Regular.ttf', 'NewsreaderT20', 'Vigil Serif T20', -20),
    ('Newsreader_Regular.ttf', 'NewsreaderT15', 'Vigil Serif T15', -15),
    ('HankenGrotesk_Medium.ttf', 'HankenGroteskT20', 'Vigil Sans T20', -20),
]


def track(source: str, base: str, family: str, milli_em: int) -> None:
    font = TTFont(FONTS / source)
    delta = round(font['head'].unitsPerEm * milli_em / 1000)
    hmtx = font['hmtx']
    for glyph, (advance, lsb) in hmtx.metrics.items():
        if advance > 0:
            hmtx.metrics[glyph] = (max(0, advance + delta), lsb)
    if 'hhea' in font:
        font['hhea'].advanceWidthMax = max(a for a, _ in hmtx.metrics.values())
    postscript = family.replace(' ', '')
    for record in font['name'].names:
        if record.nameID in (1, 4, 16, 18):
            record.string = family
        elif record.nameID == 6:
            record.string = postscript
        elif record.nameID == 3:
            record.string = f'{postscript};tracked {milli_em}'
        elif record.nameID == 17:
            record.string = 'Regular'
    weighted = FONTS / f'{base}_Medium.ttf' if 'Mono' in base or 'Hanken' in base else FONTS / f'{base}_Regular.ttf'
    font.save(weighted)
    shutil.copyfile(weighted, FONTS / f'{base}.ttf')
    uri = f'ms-appx:///Assets/Fonts/{weighted.name}'
    manifest = {'fonts': [{'font_style': 'Normal', 'font_weight': w, 'font_stretch': 'Normal', 'family_name': uri} for w in (400, 500, 600)]}
    (FONTS / f'{base}.ttf.manifest').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    print(f'{base}: {delta:+d} units per glyph ({milli_em / 1000:+.3f} em)')


if __name__ == '__main__':
    for variant in VARIANTS:
        track(*variant)
