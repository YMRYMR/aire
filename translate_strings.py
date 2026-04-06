import json
import os
import re
import time
from deep_translator import GoogleTranslator

translations_dir = 'Aire/Translations'

# language mapping to Google Translator target codes
target_codes = {
    'ar': 'ar',
    'de': 'de',
    'es': 'es',
    'fr': 'fr',
    'hi': 'hi',
    'it': 'it',
    'ja': 'ja',
    'ko': 'ko',
    'pt-br': 'pt',
    'uk': 'uk',
    'zh': 'zh-cn',
}

def protect_placeholders(text):
    """Replace {0}, {1}, etc with placeholders that won't be translated."""
    placeholders = {}
    def replace(match):
        ph = f'__PLACEHOLDER_{len(placeholders)}__'
        placeholders[ph] = match.group(0)
        return ph
    # pattern for {0}, {1}, {2}, ... also maybe {{?}}
    protected = re.sub(r'\{(\d+)\}', replace, text)
    return protected, placeholders

def restore_placeholders(text, placeholders):
    for ph, original in placeholders.items():
        text = text.replace(ph, original)
    return text

def translate_text(text, src='en', dest='es'):
    try:
        translator = GoogleTranslator(source=src, target=dest)
        # GoogleTranslator has a limit of 5000 characters per request; text is short.
        # Add small delay to avoid rate limiting
        time.sleep(0.1)
        translated = translator.translate(text)
        return translated
    except Exception as e:
        print(f"  Translation error: {e}")
        return None

def translate_strings_for_lang(lang_code, en_strings, lang_strings):
    target = target_codes.get(lang_code)
    if not target:
        print(f"  No target code for {lang_code}, skipping")
        return lang_strings
    updated = {}
    for key, en_text in en_strings.items():
        if key in lang_strings and lang_strings[key].strip() != '':
            # Keep existing translation
            updated[key] = lang_strings[key]
            continue
        # Translate
        print(f"  Translating {key}")
        protected, placeholders = protect_placeholders(en_text)
        translated = translate_text(protected, 'en', target)
        if translated is None:
            print(f"    Failed, using English")
            updated[key] = en_text
        else:
            restored = restore_placeholders(translated, placeholders)
            updated[key] = restored
    return updated

def main():
    en_path = os.path.join(translations_dir, 'en.json')
    with open(en_path, 'r', encoding='utf-8') as f:
        en_data = json.load(f)
    en_strings = en_data.get('strings', {})
    
    for lang_code in target_codes.keys():
        path = os.path.join(translations_dir, f'{lang_code}.json')
        if not os.path.exists(path):
            print(f'{lang_code}: file not found')
            continue
        print(f'Processing {lang_code}...')
        with open(path, 'r', encoding='utf-8') as f:
            lang_data = json.load(f)
        lang_strings = lang_data.get('strings', {})
        updated_strings = translate_strings_for_lang(lang_code, en_strings, lang_strings)
        lang_data['strings'] = updated_strings
        # Write back
        with open(path, 'w', encoding='utf-8', newline='\n') as f:
            json.dump(lang_data, f, ensure_ascii=False, indent=2)
        print(f'  Updated {len(updated_strings)} strings')
    
    print('Translation completed.')

if __name__ == '__main__':
    main()