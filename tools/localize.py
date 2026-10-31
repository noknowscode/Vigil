"""
Localization build step for Vigil.

1. Gives every XAML element with static user-facing text an x:Uid (kept if present).
2. Collects code strings from Loc.T("Key", "English") / Loc.F("Key", "English", ...) calls.
3. Writes Strings/en/Resources.resw and Strings/fr/Resources.resw. French values come from tools/fr.json
   (English -> French); any missing translation fails the run so nothing ships half translated.

Usage: python tools/localize.py           list English strings that have no French yet
       python tools/localize.py --write   add x:Uid attributes and write both .resw files
"""
import json
import re
import sys
from pathlib import Path
from xml.sax.saxutils import escape

TOOLS = Path(__file__).resolve().parent
ROOT = TOOLS.parent / 'Vigil'
PROPS = ['Text', 'Content', 'Header', 'Label', 'PlaceholderText', 'Title', 'OnContent', 'OffContent',
         'AutomationProperties.Name', 'SpokenName']
TAG = re.compile(r'<(?P<name>[A-Za-z_][\w:.]*)(?P<attrs>(?:\s+[\w:.]+="[^"]*")*)\s*(?P<end>/?)>', re.S)
ATTR = re.compile(r'([\w:.]+)="([^"]*)"')
LOC = re.compile(r'Loc\.(?:T|F)\("(\w+)",\s*"((?:[^"\\]|\\.)*)"')


def is_static(value: str) -> bool:
    v = value.strip()
    return bool(v) and not v.startswith('{') and re.search(r'[A-Za-zÀ-ÿ]', v) is not None


def xaml_files():
    for path in sorted(ROOT.rglob('*.xaml')):
        if path.name == 'App.xaml' or {'obj', 'bin', 'Styles', 'Themes'} & set(path.parts):
            continue
        yield path


def collect_xaml(write: bool) -> dict:
    entries = {}
    counter = {}
    for path in xaml_files():
        text = path.read_text(encoding='utf-8')
        used_uids = set(re.findall(r'x:Uid="([^"]+)"', text))
        base = path.stem.replace('Page', '').replace('View', '') or path.stem

        def replace(m):
            attrs = m.group('attrs')
            pairs = ATTR.findall(attrs)
            statics = [(k, v) for k, v in pairs if k in PROPS and is_static(v)]
            if not statics:
                return m.group(0)
            existing = dict(pairs).get('x:Uid')
            uid = existing
            if uid is None:
                counter[base] = counter.get(base, 0) + 1
                uid = f'{base}_{counter[base]}'
                while uid in used_uids:
                    counter[base] += 1
                    uid = f'{base}_{counter[base]}'
                used_uids.add(uid)
            for k, v in statics:
                key = f'{uid}.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name' if k == 'AutomationProperties.Name' else f'{uid}.{k}'
                entries[key] = v.replace('&amp;', '&').replace('&quot;', '"')
            if existing:
                return m.group(0)
            return f'<{m.group("name")} x:Uid="{uid}"{attrs}{" " if m.group("end") else ""}{m.group("end")}>'

        new = TAG.sub(replace, text)
        if write and new != text:
            path.write_text(new, encoding='utf-8')
    return entries


def collect_code() -> dict:
    code = {}
    for cs in sorted(ROOT.rglob('*.cs')):
        if {'obj', 'bin'} & set(cs.parts):
            continue
        for key, english in LOC.findall(cs.read_text(encoding='utf-8')):
            english = english.replace('\\"', '"').replace("\\'", "'")
            if code.get(key, english) != english:
                raise SystemExit(f'{key}: two English texts ({code[key]!r} vs {english!r})')
            code[key] = english
    return code


def resw(entries: dict) -> str:
    rows = '\n'.join(
        f'  <data name="{escape(k, {chr(34): "&quot;"})}" xml:space="preserve"><value>{escape(v)}</value></data>'
        for k, v in sorted(entries.items()))
    return ('<?xml version="1.0" encoding="utf-8"?>\n<root>\n'
            '  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>\n'
            '  <resheader name="version"><value>2.0</value></resheader>\n'
            '  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>\n'
            '  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>\n'
            f'{rows}\n</root>\n')


if __name__ == '__main__':
    write = '--write' in sys.argv
    xaml = collect_xaml(write)
    code = collect_code()
    all_en = {**xaml, **code}
    fr_path = TOOLS / 'fr.json'
    fr_map = json.loads(fr_path.read_text(encoding='utf-8')) if fr_path.exists() else {}
    missing = sorted({v for v in all_en.values() if v not in fr_map})
    print(f'{len(xaml)} XAML entries, {len(code)} code entries, {len(missing)} English strings without French', file=sys.stderr)
    if not write:
        print(json.dumps(missing, ensure_ascii=False, indent=1))
        sys.exit(0)
    if missing:
        print('Missing French for:', *missing, sep='\n  ', file=sys.stderr)
        sys.exit(1)
    (ROOT / 'Strings/en/Resources.resw').write_text(resw(all_en), encoding='utf-8')
    (ROOT / 'Strings/fr/Resources.resw').write_text(resw({k: fr_map[v] for k, v in all_en.items()}), encoding='utf-8')
    print(f'Wrote {len(all_en)} keys to en and fr.')
