import json
import os
import shutil

def ensure_language_files():
    translations_dir = 'Aire/Translations'
    backups = ['de', 'fr', 'it']
    for code in backups:
        backup = os.path.join(translations_dir, f'{code}.json.backup')
        target = os.path.join(translations_dir, f'{code}.json')
        if os.path.exists(backup) and not os.path.exists(target):
            shutil.copy2(backup, target)
            print(f'Copied {backup} -> {target}')
        elif os.path.exists(target):
            print(f'{target} already exists')
        else:
            print(f'No backup for {code}')

if __name__ == '__main__':
    ensure_language_files()