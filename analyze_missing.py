import json
import os

def load_json(path):
    with open(path, 'r', encoding='utf-8') as f:
        return json.load(f)

def compare(en_data, lang_data, lang_code):
    missing_strings = []
    en_strings = en_data.get('strings', {})
    lang_strings = lang_data.get('strings', {})
    for key in en_strings:
        if key not in lang_strings:
            missing_strings.append(key)
    
    en_help = en_data.get('help', [])
    lang_help = lang_data.get('help', [])
    help_missing = False
    help_len_diff = 0
    if en_help:
        if not lang_help:
            help_missing = True
        else:
            help_len_diff = len(en_help) - len(lang_help)
    
    return missing_strings, help_missing, help_len_diff

def main():
    translations_dir = 'Aire/Translations'
    en_path = os.path.join(translations_dir, 'en.json')
    en_data = load_json(en_path)
    
    languages = ['ar', 'de', 'es', 'fr', 'hi', 'it', 'ja', 'ko', 'pt-br', 'uk', 'zh']
    for lang in languages:
        path = os.path.join(translations_dir, f'{lang}.json')
        if not os.path.exists(path):
            print(f'{lang}: file not found')
            continue
        lang_data = load_json(path)
        missing_strings, help_missing, help_len_diff = compare(en_data, lang_data, lang)
        total_strings = len(en_data.get('strings', {}))
        print(f'{lang}:')
        print(f'  strings: {len(missing_strings)} missing out of {total_strings}')
        if missing_strings:
            # print first 5 missing keys
            for key in missing_strings[:5]:
                print(f'    - {key}')
            if len(missing_strings) > 5:
                print(f'    ... and {len(missing_strings)-5} more')
        if help_missing:
            print(f'  help: entire section missing')
        elif help_len_diff != 0:
            print(f'  help: length mismatch ({help_len_diff} items missing)')
        else:
            print(f'  help: complete')
        print()

if __name__ == '__main__':
    main()