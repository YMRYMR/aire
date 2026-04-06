import json
import os
import random
import sys

def safe_print(text):
    try:
        print(text)
    except UnicodeEncodeError:
        # encode with replace for non-encodable characters
        encoded = text.encode(sys.stdout.encoding, errors='replace').decode(sys.stdout.encoding)
        print(encoded)

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
    # pick up to 5 random keys
    keys = list(en_strings.keys())
    random.shuffle(keys)
    selected = keys[:5]
    safe_print(f'\n--- {lang} ---')
    for key in selected:
        en = en_strings.get(key, '')
        trans = lang_strings.get(key, '')
        safe_print(f'{key}:')
        safe_print(f'  EN: {en}')
        safe_print(f'  {lang}: {trans}')
        # check placeholder mismatch
        import re
        en_placeholders = set(re.findall(r'\{(\d+)\}', en))
        trans_placeholders = set(re.findall(r'\{(\d+)\}', trans))
        if en_placeholders != trans_placeholders:
            safe_print(f'  WARNING: placeholder mismatch! en {en_placeholders} vs {trans_placeholders}')
        safe_print('')