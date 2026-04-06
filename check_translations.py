import json
import os

translations_dir = 'Aire/Translations'
en_path = os.path.join(translations_dir, 'en.json')
with open(en_path, 'r', encoding='utf-8') as f:
    en_data = json.load(f)
en_strings = en_data.get('strings', {})

langs = ['ar', 'de', 'es', 'fr', 'hi', 'it', 'ja', 'ko', 'pt-br', 'uk', 'zh']
for lang in langs:
    path = os.path.join(translations_dir, f'{lang}.json')
    with open(path, 'r', encoding='utf-8') as f:
        data = json.load(f)
    lang_strings = data.get('strings', {})
    same = 0
    total = 0
    for key, en_val in en_strings.items():
        if key in lang_strings:
            total += 1
            if lang_strings[key] == en_val:
                same += 1
    print(f'{lang}: {same}/{total} strings identical to English')
    if same == total:
        print('  WARNING: all strings are English!')