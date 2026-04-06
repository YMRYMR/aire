import json
import os
import re
import time
from deep_translator import GoogleTranslator

translations_dir = 'Aire/Translations'

target_codes = {'de': 'de', 'it': 'it'}

def protect_placeholders(text):
    placeholders = {}
    def replace(match):
        ph = f'__PLACEHOLDER_{len(placeholders)}__'
        placeholders[ph] = match.group(0)
        return ph
    protected = re.sub(r'\{(\d+)\}', replace, text)
    return protected, placeholders

def restore_placeholders(text, placeholders):
    for ph, original in placeholders.items():
        text = text.replace(ph, original)
    return text

def translate_text(text, src='en', dest='es'):
    try:
        translator = GoogleTranslator(source=src, target=dest)
        time.sleep(0.2)  # slower to avoid rate limit
        translated = translator.translate(text)
        return translated
    except Exception as e:
        print(f"  Translation error: {e}")
        return None

def main():
    en_path = os.path.join(translations_dir, 'en.json')
    with open(en_path, 'r', encoding='utf-8') as f:
        en_data = json.load(f)
    en_strings = en_data.get('strings', {})
    
    for lang_code, target in target_codes.items():
        path = os.path.join(translations_dir, f'{lang_code}.json')
        print(f'Processing {lang_code}...')
        with open(path, 'r', encoding='utf-8') as f:
            lang_data = json.load(f)
        lang_strings = lang_data.get('strings', {})
        updated = {}
        translated_count = 0
        for key, en_text in en_strings.items():
            existing = lang_strings.get(key, '')
            if existing == en_text:
                # translate
                print(f'  Translating {key}')
                protected, placeholders = protect_placeholders(en_text)
                translated = translate_text(protected, 'en', target)
                if translated is None:
                    updated[key] = en_text
                else:
                    restored = restore_placeholders(translated, placeholders)
                    updated[key] = restored
                    translated_count += 1
            else:
                updated[key] = existing
        lang_data['strings'] = updated
        with open(path, 'w', encoding='utf-8', newline='\n') as f:
            json.dump(lang_data, f, ensure_ascii=False, indent=2)
        print(f'  Translated {translated_count} strings')
    
    print('Done.')

if __name__ == '__main__':
    main()