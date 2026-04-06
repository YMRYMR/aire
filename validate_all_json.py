import json
import os
import sys

def validate(filepath):
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            data = json.load(f)
        print(f'{filepath}: OK')
        return True
    except json.JSONDecodeError as e:
        print(f'{filepath}: Error at line {e.lineno} col {e.colno}: {e.msg}')
        # print snippet
        with open(filepath, 'r', encoding='utf-8') as f:
            lines = f.readlines()
            start = max(0, e.lineno - 2)
            end = min(len(lines), e.lineno + 2)
            for i in range(start, end):
                print(f'{i+1}: {lines[i]}', end='')
        return False

if __name__ == '__main__':
    translations_dir = 'Aire/Translations'
    all_ok = True
    for filename in os.listdir(translations_dir):
        if '.json' in filename:
            path = os.path.join(translations_dir, filename)
            if not validate(path):
                all_ok = False
    if all_ok:
        print('All JSON files are syntactically valid.')
    else:
        print('Some JSON files have syntax errors.')
        sys.exit(1)