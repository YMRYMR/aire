import json
import os

translations_dir = 'Aire/Translations'
en_path = os.path.join(translations_dir, 'en.json')
with open(en_path, 'r', encoding='utf-8') as f:
    en_data = json.load(f)

en_strings = en_data.get('strings', {})
en_help = en_data.get('help', [])

languages = ['ar', 'de', 'es', 'fr', 'hi', 'it', 'ja', 'ko', 'pt-br', 'uk', 'zh']

print("Translation Completeness Analysis")
print("=================================")
print(f"Reference language: English (en) with {len(en_strings)} strings and {len(en_help)} help items")
print()

for lang in languages:
    path = os.path.join(translations_dir, f'{lang}.json')
    if not os.path.exists(path):
        print(f"{lang}: FILE NOT FOUND")
        continue
    with open(path, 'r', encoding='utf-8') as f:
        data = json.load(f)
    
    lang_strings = data.get('strings', {})
    lang_help = data.get('help', [])
    
    # Missing strings (keys not present)
    missing_keys = [key for key in en_strings if key not in lang_strings]
    # Identical strings (keys present but same value)
    identical_keys = [key for key in en_strings if key in lang_strings and en_strings[key] == lang_strings[key]]
    
    # Help analysis
    help_missing = False
    help_len_diff = 0
    if en_help:
        if not lang_help:
            help_missing = True
        else:
            help_len_diff = len(en_help) - len(lang_help)
    
    print(f"{lang}:")
    print(f"  Strings: {len(lang_strings)}/{len(en_strings)} present")
    print(f"    Missing: {len(missing_keys)}")
    if missing_keys:
        for key in missing_keys[:5]:
            print(f"      - {key}")
        if len(missing_keys) > 5:
            print(f"      ... and {len(missing_keys)-5} more")
    print(f"    Identical to English: {len(identical_keys)}")
    if identical_keys:
        for key in identical_keys[:5]:
            print(f"      - {key}")
        if len(identical_keys) > 5:
            print(f"      ... and {len(identical_keys)-5} more")
    
    # Help
    if help_missing:
        print(f"  Help: entire section missing")
    elif help_len_diff != 0:
        print(f"  Help: length mismatch ({help_len_diff} items missing)")
    else:
        print(f"  Help: complete ({len(lang_help)} items)")
    
    print()

print("Summary of identical strings per language:")
print("Language | Identical Count | Identical Percent")
print("-------- | --------------- | ----------------")
for lang in languages:
    path = os.path.join(translations_dir, f'{lang}.json')
    if not os.path.exists(path):
        continue
    with open(path, 'r', encoding='utf-8') as f:
        data = json.load(f)
    lang_strings = data.get('strings', {})
    identical = sum(1 for key in en_strings if key in lang_strings and en_strings[key] == lang_strings[key])
    percent = (identical / len(en_strings)) * 100 if len(en_strings) > 0 else 0
    print(f"{lang:8} | {identical:15} | {percent:.1f}%")