import json
import os
import re
import time
from deep_translator import GoogleTranslator

translations_dir = 'Aire/Translations'

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

# Keys that should NOT be translated (structural fields)
SKIP_KEYS = {'tab', 'type', 'action', 'imagePath', 'code', 'nativeName', 'flag'}
# Keys that contain nested arrays of strings that need translation
ARRAY_KEYS = {'links', 'cols', 'rows'}

def protect_placeholders(text):
    """Replace {0}, {1}, etc with placeholders that won't be translated."""
    placeholders = {}
    def replace(match):
        ph = f'__PLACEHOLDER_{len(placeholders)}__'
        placeholders[ph] = match.group(0)
        return ph
    # pattern for {0}, {1}, {2}, ... also maybe {{?}} but not needed
    protected = re.sub(r'\{(\d+)\}', replace, text)
    return protected, placeholders

def restore_placeholders(text, placeholders):
    for ph, original in placeholders.items():
        text = text.replace(ph, original)
    return text

def translate_text(text, src='en', dest='es'):
    try:
        translator = GoogleTranslator(source=src, target=dest)
        time.sleep(0.1)  # avoid rate limiting
        translated = translator.translate(text)
        return translated
    except Exception as e:
        print(f"    Translation error: {e}")
        return None

def translate_string(value, dest_code):
    """Translate a single string, preserving placeholders."""
    if not isinstance(value, str) or not value.strip():
        return value
    protected, placeholders = protect_placeholders(value)
    translated = translate_text(protected, 'en', dest_code)
    if translated is None:
        return value
    restored = restore_placeholders(translated, placeholders)
    return restored

def translate_object(obj, dest_code, en_obj=None):
    """
    Recursively translate string values in obj.
    If en_obj is provided, compare leaf strings to see if already translated.
    """
    if isinstance(obj, dict):
        new_obj = {}
        for key, val in obj.items():
            if key in SKIP_KEYS:
                new_obj[key] = val
                continue
            if key in ARRAY_KEYS:
                # handle nested arrays specially
                if isinstance(val, list):
                    new_obj[key] = translate_array(val, dest_code, en_obj.get(key) if en_obj else None)
                else:
                    new_obj[key] = val
                continue
            if isinstance(val, str):
                en_val = en_obj.get(key) if en_obj else None
                # If English value exists and matches current value (ignoring whitespace), translate
                if en_val is not None and val.strip() == en_val.strip():
                    new_obj[key] = translate_string(val, dest_code)
                else:
                    # Keep existing translation (maybe already translated)
                    new_obj[key] = val
            elif isinstance(val, dict):
                new_obj[key] = translate_object(val, dest_code, en_obj.get(key) if en_obj else None)
            elif isinstance(val, list):
                new_obj[key] = translate_array(val, dest_code, en_obj.get(key) if en_obj else None)
            else:
                new_obj[key] = val
        return new_obj
    else:
        return obj

def translate_array(arr, dest_code, en_arr=None):
    """Translate each element of array, which may be strings, dicts, or nested arrays."""
    if not arr:
        return arr
    new_arr = []
    for i, elem in enumerate(arr):
        en_elem = en_arr[i] if en_arr and i < len(en_arr) else None
        if isinstance(elem, str):
            if en_elem is not None and elem.strip() == en_elem.strip():
                new_arr.append(translate_string(elem, dest_code))
            else:
                new_arr.append(elem)
        elif isinstance(elem, dict):
            new_arr.append(translate_object(elem, dest_code, en_elem))
        elif isinstance(elem, list):
            new_arr.append(translate_array(elem, dest_code, en_elem))
        else:
            new_arr.append(elem)
    return new_arr

def main():
    en_path = os.path.join(translations_dir, 'en.json')
    with open(en_path, 'r', encoding='utf-8') as f:
        en_data = json.load(f)
    en_help = en_data.get('help', [])
    
    for lang_code, target in target_codes.items():
        if lang_code == 'es':
            print(f'Skipping Spanish (already translated)')
            continue
        path = os.path.join(translations_dir, f'{lang_code}.json')
        if not os.path.exists(path):
            print(f'{lang_code}: file not found')
            continue
        print(f'Processing {lang_code}...')
        with open(path, 'r', encoding='utf-8') as f:
            lang_data = json.load(f)
        lang_help = lang_data.get('help', [])
        
        # Ensure same length as English (should be after earlier fixes)
        if len(lang_help) != len(en_help):
            print(f'  WARNING: help length mismatch ({len(lang_help)} vs {len(en_help)}). Copying English structure.')
            # For safety, we'll use English as base and translate everything
            lang_help = en_help.copy()
        
        translated_help = []
        for idx, en_item in enumerate(en_help):
            lang_item = lang_help[idx] if idx < len(lang_help) else {}
            translated_item = translate_object(lang_item, target, en_item)
            translated_help.append(translated_item)
        
        lang_data['help'] = translated_help
        
        # Write back
        with open(path, 'w', encoding='utf-8', newline='\n') as f:
            json.dump(lang_data, f, ensure_ascii=False, indent=2)
        print(f'  Translated help for {lang_code}')
    
    print('Help translation completed.')

if __name__ == '__main__':
    main()