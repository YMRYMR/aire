import json
import os
import sys

def load_json(path):
    with open(path, 'r', encoding='utf-8') as f:
        return json.load(f)

def compare_keys(en_data, lang_data, lang_code):
    missing = []
    # compare strings
    en_strings = en_data.get('strings', {})
    lang_strings = lang_data.get('strings', {})
    for key in en_strings:
        if key not in lang_strings:
            missing.append(('strings', key))
    # compare help array
    en_help = en_data.get('help', [])
    lang_help = lang_data.get('help', [])
    if en_help and not lang_help:
        missing.append(('help', 'full help section missing'))
    elif lang_help and len(en_help) != len(lang_help):
        missing.append(('help', f'length mismatch: {len(en_help)} vs {len(lang_help)}'))
    # could do deeper per-item comparison but skip for now
    return missing

def main():
    translations_dir = 'Aire/Translations'
    en_path = os.path.join(translations_dir, 'en.json')
    en_data = load_json(en_path)
    
    languages = ['ar', 'es', 'hi', 'ja', 'ko', 'pt', 'uk', 'zh']
    # also consider de, fr, it but they are .backup
    for lang in languages:
        path = os.path.join(translations_dir, f'{lang}.json')
        if not os.path.exists(path):
            print(f'{lang}: file not found')
            continue
        lang_data = load_json(path)
        missing = compare_keys(en_data, lang_data, lang)
        if missing:
            print(f'{lang}: missing keys:')
            for category, key in missing:
                print(f'  {category}.{key}')
        else:
            print(f'{lang}: all keys present')

if __name__ == '__main__':
    main()